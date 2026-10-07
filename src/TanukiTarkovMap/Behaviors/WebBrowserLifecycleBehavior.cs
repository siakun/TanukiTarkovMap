using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CefSharp;
using CefSharp.Wpf;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Models.Offline;
using TanukiTarkovMap.ViewModels;

namespace TanukiTarkovMap.Behaviors
{
    /**
    WebBrowserLifecycleBehavior - ChromiumWebBrowser와 WebBrowserViewModel의 수명 주기 연결

    Purpose: WebBrowserUserControl의 Code-behind 없이 브라우저 설정, ViewModel 연결과 F12 입력을 처리한다.
    Architecture: XAML의 ContentControl 안에 모드별 ChromiumWebBrowser를 만들고, 데이터와 이동 흐름은
    WebBrowserViewModel.SetBrowser()에 브라우저 인스턴스를 넘겨 ViewModel에서 처리한다.

    Core Functionality:
    - 브라우저 설정: CEF가 초기화되기 전에 WindowlessFrameRate를 60fps로 설정
    - ViewModel 연결: DataContext가 준비되거나 모드가 바뀌면 새 브라우저를 ViewModel에 전달
    - 저장 공간 분리: Online은 기존 프로필, Local은 자체 미니맵 파일을 응답하는 독립된 메모리 RequestContext 사용
    - 개발자 도구: 브라우저가 받은 F12 입력으로 CefSharp 개발자 도구 표시

    State Management:
    - _viewModel: 현재 연결한 ViewModel과 모드 변경 이벤트 구독
    - _browser, _localContext: 함께 만들고 해제할 브라우저와 로컬 저장 공간

    Method Flow:
      OnAttached -> UI 이벤트 구독 -> ConnectViewModel
      Loaded/DataContextChanged -> ConnectViewModel -> WebBrowserViewModel.SetBrowser
      BrowserModeChanged -> 이전 브라우저 해제 -> 새 저장 공간과 브라우저 연결
      KeyDown(F12) -> ChromiumWebBrowser.ShowDevTools

    Key Methods:
    - OnAttached(): 필요한 UI 이벤트 구독
    - ReplaceBrowser(): 요청 처리기와 저장 공간을 설정한 뒤 새 브라우저 연결

    Dependencies:
    - ChromiumWebBrowser: 설정과 키 입력을 처리할 WPF 브라우저 컨트롤
    - WebBrowserViewModel: 브라우저 이벤트, 준비 시점과 탐색 흐름 관리

    Design Rationale: 컨트롤 생성과 UI 입력은 Behavior에 두되, 시작 주소 선택과 탐색은
    ViewModel에 남겨 UI 수명 주기와 데이터 흐름을 섞지 않는다.

    Historical Context: 2026-08-19 이전에는 WebBrowserUserControl의 Loaded 처리기가 SetBrowser() 직후
    시작 주소를 열었다. 느린 머신에서는 CEF 초기화 전에 호출되어 요청이 사라졌고, 브라우저 생성과
    F12 처리도 Code-behind에 섞여 있었다.
    Known Limitations: DataContext가 WebBrowserViewModel인 현재 WebBrowserUserControl 구성에서 사용한다.
    Edge Cases: OnAttached 시 DataContext가 없으면 DataContextChanged나 Loaded에서 다시 연결한다.
    Critical Warnings: 이 Behavior에서 시작 URL을 열지 않는다. 준비 시점과 주소 원천은
    WebBrowserViewModel이 단독으로 관리해야 한다.

    Last Updated: 2026-08-19 | .NET 8.0 / CefSharp 141.0.110 | By 시작 맵 초기화 수정
    */
    public class WebBrowserLifecycleBehavior : Behavior<ContentControl>
    {
        private WebBrowserViewModel? _viewModel;
        private ChromiumWebBrowser? _browser;
        private IRequestContext? _localContext;

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.Loaded += OnBrowserLoaded;
            AssociatedObject.DataContextChanged += OnDataContextChanged;
            AssociatedObject.KeyDown += OnBrowserKeyDown;

            ConnectViewModel();
        }

        protected override void OnDetaching()
        {
            AssociatedObject.Loaded -= OnBrowserLoaded;
            AssociatedObject.DataContextChanged -= OnDataContextChanged;
            AssociatedObject.KeyDown -= OnBrowserKeyDown;
            DisconnectViewModel();

            base.OnDetaching();
        }

        private void OnBrowserLoaded(object sender, RoutedEventArgs e)
        {
            ConnectViewModel();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ConnectViewModel();
        }

        private void OnBrowserKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F12 && _browser?.IsDisposed == false)
            {
                _browser.ShowDevTools();
            }
        }

        private void ConnectViewModel()
        {
            var viewModel = AssociatedObject.DataContext as WebBrowserViewModel;
            if (ReferenceEquals(_viewModel, viewModel))
            {
                return;
            }

            DisconnectViewModel();
            _viewModel = viewModel;
            if (_viewModel == null) return;

            _viewModel.BrowserModeChanged += OnBrowserModeChanged;
            ReplaceBrowser();
        }

        private void OnBrowserModeChanged(object? sender, EventArgs e) => ReplaceBrowser();

        // INTENT
        // Local은 자체 미니맵 파일을 응답하는 메모리 RequestContext를 사용한다. Online의 프로필을 공유하거나
        // 초기화하지 않아 사이트의 쿠키와 설정을 보존한다. 앱의 맵/진영/최신 입력은 ViewModel이 복원한다.
        private void ReplaceBrowser()
        {
            ReleaseBrowser();
            if (_viewModel == null) return;

            if (_viewModel.IsLocalMapMode)
            {
                _localContext = LocalViewer.CreateRequestContext();
                // Behavior 해제 없이 앱이 종료되어도 CEF 종료 전에 네이티브 참조를 놓는다.
                Cef.AddDisposable(_localContext);
            }

            // 저장 공간은 CEF 생성 전에 정한다. Online의 null은 기존 전역 프로필을 사용하므로
            // 쿠키와 사이트 설정을 보존한다.
            _browser = new ChromiumWebBrowser
            {
                RequestContext = _localContext,
                Address = "about:blank",
            };
            _browser.BrowserSettings.WindowlessFrameRate = 60;
            Interaction.GetBehaviors(_browser).Add(new DuplicateMouseMoveFilterBehavior());

            _viewModel.SetBrowser(_browser);
            AssociatedObject.Content = _browser;
        }

        private void ReleaseBrowser()
        {
            _viewModel?.SetBrowser(null);
            AssociatedObject.Content = null;
            if (_browser != null)
            {
                Interaction.GetBehaviors(_browser).Clear();
                _browser.Dispose();
                _browser = null;
            }
            if (_localContext != null)
            {
                Cef.RemoveDisposable(_localContext);
                if (!_localContext.IsDisposed) _localContext.Dispose();
                _localContext = null;
            }
        }

        private void DisconnectViewModel()
        {
            if (_viewModel != null) _viewModel.BrowserModeChanged -= OnBrowserModeChanged;
            ReleaseBrowser();
            _viewModel = null;
        }
    }
}
