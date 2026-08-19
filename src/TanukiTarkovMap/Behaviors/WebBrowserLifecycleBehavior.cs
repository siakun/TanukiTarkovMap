using System.Windows;
using System.Windows.Input;
using CefSharp;
using CefSharp.Wpf;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.ViewModels;

namespace TanukiTarkovMap.Behaviors
{
    /**
    WebBrowserLifecycleBehavior - ChromiumWebBrowser와 WebBrowserViewModel의 수명 주기 연결

    Purpose: WebBrowserUserControl의 Code-behind 없이 브라우저 설정, ViewModel 연결과 F12 입력을 처리한다.
    Architecture: XAML의 ChromiumWebBrowser에 붙어 UI 이벤트를 받고, 데이터와 이동 흐름은
    WebBrowserViewModel.SetBrowser()에 브라우저 인스턴스를 넘겨 ViewModel에서 처리한다.

    Core Functionality:
    - 브라우저 설정: CEF가 초기화되기 전에 WindowlessFrameRate를 60fps로 설정
    - ViewModel 연결: DataContext가 준비되면 ChromiumWebBrowser를 WebBrowserViewModel에 한 번 전달
    - 개발자 도구: 브라우저가 받은 F12 입력으로 CefSharp 개발자 도구 표시

    State Management:
    - _browserConnected: 현재 Behavior가 브라우저를 ViewModel에 이미 전달했는지 기록

    Method Flow:
      OnAttached -> 브라우저 기본 설정 -> UI 이벤트 구독 -> ConnectViewModel
      Loaded/DataContextChanged -> ConnectViewModel -> WebBrowserViewModel.SetBrowser
      KeyDown(F12) -> ChromiumWebBrowser.ShowDevTools

    Key Methods:
    - OnAttached(): 브라우저 기본값을 적용하고 필요한 UI 이벤트를 구독
    - ConnectViewModel(): DataContext가 WebBrowserViewModel일 때 브라우저 인스턴스를 한 번 전달

    Dependencies:
    - ChromiumWebBrowser: 설정과 키 입력을 처리할 WPF 브라우저 컨트롤
    - WebBrowserViewModel: 브라우저 이벤트, 준비 시점과 탐색 흐름 관리

    Design Rationale: 컨트롤 생성과 UI 입력은 XAML/Behavior에 두되, 시작 주소 선택과 탐색은
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
    public class WebBrowserLifecycleBehavior : Behavior<ChromiumWebBrowser>
    {
        private bool _browserConnected;

        protected override void OnAttached()
        {
            base.OnAttached();

            // CefSharp.Wpf의 OSR 페인트 상한을 먼저 60fps로 두고,
            // 이후 MonitorRefreshRateBehavior가 현재 모니터 주사율로 갱신한다.
            AssociatedObject.BrowserSettings.WindowlessFrameRate = 60;

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
            _browserConnected = false;

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
            if (e.Key == Key.F12)
            {
                AssociatedObject.ShowDevTools();
            }
        }

        private void ConnectViewModel()
        {
            if (_browserConnected || AssociatedObject.DataContext is not WebBrowserViewModel viewModel)
            {
                return;
            }

            viewModel.SetBrowser(AssociatedObject);
            _browserConnected = true;
        }
    }
}
