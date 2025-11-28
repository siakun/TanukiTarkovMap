using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Behaviors;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.ViewModels;

namespace TanukiTarkovMap.Views
{
    /// <summary>
    /// 창 위치/크기 변경 이벤트 인자
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
        // 창 위치/크기 변경 이벤트
        public event EventHandler<WindowBoundsChangedEventArgs>? WindowBoundsChanged;

        private MainWindowViewModel _viewModel;
        private WindowBoundsService _windowBoundsService;
        private HotkeyService _hotkeyService;
        private TrayWindowBehavior? _trayBehavior;
        private bool _isClampingLocation = false; // 무한 루프 방지
        private bool _isInitializing = true; // 초기화 중 플래그
        private SettingsPage? _settingsPage; // 설정 페이지 재사용

        public MainWindow()
        {
            try
            {
                // DI 컨테이너에서 싱글톤 서비스 가져오기
                _windowBoundsService = ServiceLocator.WindowBoundsService;
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
                Closed += MainWindow_Closed;
                LocationChanged += MainWindow_LocationChanged;
                SizeChanged += MainWindow_SizeChanged;
                StateChanged += MainWindow_StateChanged;

                // ViewModel에 창 위치/크기 변경 이벤트 연결
                this.WindowBoundsChanged += _viewModel.OnWindowBoundsChanged;

                // Note: Activated, Deactivated, MouseEnter, MouseLeave 이벤트는
                // TopBarAnimationBehavior에서 처리합니다.
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 로딩 패널 강제 숨김 (디버그용)
            LoadingPanel.Visibility = Visibility.Collapsed;

            // 저장된 창 크기 다시 적용 (WPF가 자동으로 변경한 크기 복원)
            this.Width = _viewModel.CurrentWindowWidth;
            this.Height = _viewModel.CurrentWindowHeight;
            Logger.SimpleLog($"[MainWindow_Loaded] Restored window size: {this.Width}x{this.Height}");

            // 초기화 완료 - 이제부터 OnWindowBoundsChanged가 정상 동작
            _isInitializing = false;

            // TrayWindowBehavior 찾기
            var behaviors = Interaction.GetBehaviors(this);
            _trayBehavior = behaviors.OfType<TrayWindowBehavior>().FirstOrDefault();

            // IsAlwaysOnTop 설정 적용
            ApplyTopmostSettings();

            // ViewModel 간 초기 동기화 (Messenger 패턴으로 대체됨)
            SyncInitialViewModelState();

            // 핫키 서비스 초기화 (전역 단축키용)
            InitializeHotkeyService();

            // 설정 페이지 초기화
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
            _settingsPage = new SettingsPage();
            SettingsContentContainer.Child = _settingsPage;
        }

        // 시작 시 IsAlwaysOnTop 설정 적용 (더 이상 필요 없음 - ViewModel 바인딩으로 처리)
        private void ApplyTopmostSettings()
        {
            // ViewModel의 IsAlwaysOnTop과 IsTopmost 바인딩으로 자동 처리됨
            Logger.SimpleLog("[ApplyTopmostSettings] TopMost state managed by ViewModel binding");
        }

        // WindowState 변경 시 처리 (최대화/복원 시 여백 조정)
        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                // 최대화 시 화면 경계를 넘지 않도록 여백 추가
                var thickness = SystemParameters.WindowResizeBorderThickness;
                MainGrid.Margin = new Thickness(
                    thickness.Left,
                    thickness.Top,
                    thickness.Right,
                    thickness.Bottom
                );

                // 최대화 버튼 아이콘 변경
                MaximizeRestoreButton.Content = "❐";
            }
            else
            {
                // 일반 상태로 복원 시 여백 제거
                MainGrid.Margin = new Thickness(0);

                // 복원 버튼 아이콘 변경
                MaximizeRestoreButton.Content = "□";
            }
        }

        // 핫키 서비스 초기화 (전역 단축키용)
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

        // 핫키 설정 업데이트 (SettingsPage에서 호출)
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

        /// <summary>
        /// 창 위치 변경 시 ViewModel 즉시 업데이트 및 화면 경계 체크 (모니터 없는 영역 방지)
        /// </summary>
        private void MainWindow_LocationChanged(object? sender, EventArgs e)
        {
            if (_isClampingLocation) return;        // 무한 루프 방지
            if (_isInitializing) return;            // 초기화 중에는 무시

            try
            {
                _isClampingLocation = true;

                // 창 위치/크기 변경 이벤트 발생 (ViewModel에서 즉시 저장)
                WindowBoundsChanged?.Invoke(this, new WindowBoundsChangedEventArgs
                {
                    Bounds = new Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight)
                });
            }
            finally
            {
                _isClampingLocation = false;
            }
        }

        /// <summary>
        /// 창 크기 변경 시 이벤트 발생
        /// </summary>
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isClampingLocation) return;        // 무한 루프 방지
            if (_isInitializing) return;            // 초기화 중에는 무시

            // 창 위치/크기 변경 이벤트 발생 (ViewModel에서 즉시 저장)
            WindowBoundsChanged?.Invoke(this, new WindowBoundsChangedEventArgs
            {
                Bounds = new Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight)
            });
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            // ViewModel 이벤트는 Messenger 패턴으로 대체되어 별도 구독 해제 불필요
            // HotkeyService는 DI 컨테이너에서 관리되므로 여기서 Dispose하지 않음
        }
    }
}
