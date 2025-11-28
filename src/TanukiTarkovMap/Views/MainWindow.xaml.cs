using System.Windows;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Behaviors;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.ViewModels;

namespace TanukiTarkovMap.Views
{
    /// <summary>
    /// 창 위치/크기 변경 이벤트 인자 (WindowStateBehavior에서 사용)
    /// </summary>
    public class WindowBoundsChangedEventArgs : EventArgs
    {
        public Rect Bounds { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainWindowViewModel _viewModel;
        private HotkeyService _hotkeyService;
        private TrayWindowBehavior? _trayBehavior;

        public MainWindow()
        {
            // DI 컨테이너에서 싱글톤 서비스 가져오기
            _hotkeyService = ServiceLocator.HotkeyService;

            InitializeComponent();

            // XAML에서 ServiceLocator를 통해 설정된 DataContext 가져오기
            _viewModel = (MainWindowViewModel)DataContext;

            // InitializeComponent 직후 창 크기/위치 명시적 설정 (바인딩보다 먼저 적용)
            this.Width = _viewModel.CurrentWindowWidth;
            this.Height = _viewModel.CurrentWindowHeight;
            this.Left = _viewModel.CurrentWindowLeft;
            this.Top = _viewModel.CurrentWindowTop;
            Logger.SimpleLog($"[Constructor] Set window size explicitly: {this.Width}x{this.Height} at ({this.Left}, {this.Top})");

            // 윈도우 로드 완료 후 초기화
            Loaded += MainWindow_Loaded;

            // Note: StateChanged, LocationChanged, SizeChanged 이벤트는
            // WindowStateBehavior에서 처리합니다.
            // Activated, Deactivated, MouseEnter, MouseLeave 이벤트는
            // TopBarAnimationBehavior에서 처리합니다.
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 로딩 패널 강제 숨김 (디버그용)
            LoadingPanel.Visibility = Visibility.Collapsed;

            // 저장된 창 크기 다시 적용 (WPF가 자동으로 변경한 크기 복원)
            this.Width = _viewModel.CurrentWindowWidth;
            this.Height = _viewModel.CurrentWindowHeight;
            Logger.SimpleLog($"[MainWindow_Loaded] Restored window size: {this.Width}x{this.Height}");

            // TrayWindowBehavior 찾기
            var behaviors = Interaction.GetBehaviors(this);
            _trayBehavior = behaviors.OfType<TrayWindowBehavior>().FirstOrDefault();

            // ViewModel 간 초기 동기화 (Messenger 패턴으로 대체됨)
            SyncInitialViewModelState();

            // 핫키 서비스 초기화 (전역 단축키용)
            InitializeHotkeyService();

            // 설정 페이지는 XAML에서 직접 생성하도록 변경됨
            InitializeSettingsPage();
        }

        /// <summary>
        /// ViewModel 간 초기 상태 동기화 (Messenger 패턴으로 대체됨)
        /// </summary>
        private void SyncInitialViewModelState()
        {
            var webBrowserViewModel = WebBrowser.ViewModel;
            if (webBrowserViewModel == null)
            {
                Logger.SimpleLog("[MainWindow] WebBrowserViewModel is null");
                return;
            }

            // 초기 값 동기화 (Messenger는 구독 후 발송된 메시지만 수신하므로 초기값은 직접 설정)
            webBrowserViewModel.HideWebElements = _viewModel.HideWebElements;
            webBrowserViewModel.ZoomLevel = _viewModel.SelectedZoomLevel;

            Logger.SimpleLog("[MainWindow] Initial ViewModel state synchronized");
        }

        /// <summary>
        /// 설정 페이지 초기화 (재사용을 위해 한 번만 생성)
        /// </summary>
        private void InitializeSettingsPage()
        {
            // TODO: XAML에서 직접 생성으로 변경 예정
            var settingsPage = new SettingsPage();
            SettingsContentContainer.Child = settingsPage;
        }

        /// <summary>
        /// 핫키 서비스 초기화 (전역 단축키용)
        /// </summary>
        private void InitializeHotkeyService()
        {
            try
            {
                // HotkeyService 초기화 (Window와 TrayBehavior의 ToggleVisibility 전달)
                _hotkeyService.Initialize(this, () =>
                {
                    _trayBehavior?.ToggleVisibility();
                });

                // 현재 설정으로 핫키 등록
                _hotkeyService.RegisterHotkey(_viewModel.HotkeyEnabled, _viewModel.HotkeyKey);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize HotkeyService", ex);
            }
        }

        /// <summary>
        /// 핫키 설정 업데이트 (SettingsPage에서 호출)
        /// </summary>
        public void UpdateHotkeySettings()
        {
            try
            {
                // ViewModel의 설정 다시 로드
                _viewModel.LoadSettings();

                // HotkeyService를 통해 핫키 업데이트
                _hotkeyService.UpdateHotkey(_viewModel.HotkeyEnabled, _viewModel.HotkeyKey);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to update hotkey settings", ex);
            }
        }
    }
}
