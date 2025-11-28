using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        public bool IsCompactMode { get; set; }
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
        private HotkeyManager? _hotkeyManager;
        private bool _isClampingLocation = false; // 무한 루프 방지
        private bool _isInitializing = true; // 초기화 중 플래그
        private SettingsPage? _settingsPage; // 설정 페이지 재사용

        public MainWindow()
        {
            try
            {
                // DI 컨테이너에서 싱글톤 서비스 가져오기
                _windowBoundsService = ServiceLocator.WindowBoundsService;

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

                // 키보드 이벤트 핸들러 추가 (디버그 모드용)
                this.PreviewKeyDown += MainWindow_PreviewKeyDown;

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

            // IsAlwaysOnTop 설정 적용
            ApplyTopmostSettings();

            // WebBrowser ViewModel과 MainWindow ViewModel 연결
            ConnectWebBrowserViewModel();

            // ViewModel이 PIP 모드 변경을 처리하도록 PropertyChanged 구독
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // 핫키 매니저 초기화 (전역 단축키용)
            InitializeHotkeyManager();

            // 설정 페이지 초기화
            InitializeSettingsPage();
        }

        /// <summary>
        /// WebBrowser ViewModel과 MainWindow ViewModel 연결
        /// </summary>
        private void ConnectWebBrowserViewModel()
        {
            var webBrowserViewModel = WebBrowser.ViewModel;
            if (webBrowserViewModel == null)
            {
                Logger.SimpleLog("[MainWindow] WebBrowserViewModel is null");
                return;
            }

            // ViewModel 간 속성 동기화
            webBrowserViewModel.HideWebElements = _viewModel.HideWebElements;
            webBrowserViewModel.ZoomLevel = _viewModel.SelectedZoomLevel;
            webBrowserViewModel.IsCompactMode = _viewModel.IsCompactMode;

            // 맵 수신 이벤트 연결
            webBrowserViewModel.MapReceived += (s, mapName) =>
            {
                _viewModel.CurrentMap = mapName;
            };

            // Pilot 연결 이벤트 연결
            webBrowserViewModel.PilotConnected += (s, e) =>
            {
                if (_viewModel.SelectedMapInfo == null)
                {
                    // 맵이 선택되어 있지 않으면 기본 맵으로 이동
                    var defaultMap = App.AvailableMaps.FirstOrDefault();
                    if (defaultMap != null)
                    {
                        _viewModel.SelectedMapInfo = defaultMap;
                        Logger.SimpleLog($"[MainWindow] Auto-navigating to default map: {defaultMap.DisplayName}");
                    }
                }
                else
                {
                    // 맵이 이미 선택되어 있으면 해당 맵으로 네비게이션 수행
                    webBrowserViewModel.NavigateToMap(_viewModel.SelectedMapInfo);
                }
            };

            Logger.SimpleLog("[MainWindow] WebBrowserViewModel connected");
        }

        /// <summary>
        /// 설정 페이지 초기화 (재사용을 위해 한 번만 생성)
        /// </summary>
        private void InitializeSettingsPage()
        {
            _settingsPage = new SettingsPage();
            SettingsContentContainer.Child = _settingsPage;
        }

        /// <summary>
        /// ViewModel 프로퍼티 변경 처리
        /// </summary>
        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            var webBrowserViewModel = WebBrowser.ViewModel;
            if (webBrowserViewModel == null)
                return;

            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.IsCompactMode):
                    webBrowserViewModel.IsCompactMode = _viewModel.IsCompactMode;
                    HandleCompactModeChanged();
                    break;

                case nameof(MainWindowViewModel.SelectedMapInfo):
                    if (_viewModel.SelectedMapInfo != null)
                    {
                        _viewModel.CurrentMap = _viewModel.SelectedMapInfo.MapId;
                        webBrowserViewModel.NavigateToMap(_viewModel.SelectedMapInfo);
                    }
                    break;

                case nameof(MainWindowViewModel.HideWebElements):
                    webBrowserViewModel.HideWebElements = _viewModel.HideWebElements;
                    break;

                case nameof(MainWindowViewModel.SelectedZoomLevel):
                    webBrowserViewModel.ZoomLevel = _viewModel.SelectedZoomLevel;
                    break;
            }
        }

        /// <summary>
        /// Compact 모드 변경 처리
        /// </summary>
        private void HandleCompactModeChanged()
        {
            if (_viewModel.IsCompactMode)
            {
                // Compact 모드 시작 시 현재 화면 저장 (LocationChanged에서 경계 체크에 사용)
                var windowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                _windowBoundsService.SaveCompactModeScreen(windowHandle);

                // 창 위치를 화면 내부로 보정
                var dpiInfo = VisualTreeHelper.GetDpi(this);
                var validatedPosition = _windowBoundsService.EnsureWindowWithinScreen(
                    _viewModel.CurrentWindowLeft,
                    _viewModel.CurrentWindowTop,
                    _viewModel.CurrentWindowWidth,
                    _viewModel.CurrentWindowHeight,
                    dpiInfo.DpiScaleX,
                    dpiInfo.DpiScaleY
                );

                // 검증된 위치 반영
                _viewModel.CurrentWindowLeft = validatedPosition.X;
                _viewModel.CurrentWindowTop = validatedPosition.Y;
            }
            else
            {
                // Compact 모드 종료 시 화면 정보 초기화
                _windowBoundsService.ClearCompactModeScreen();
                Logger.SimpleLog("[Compact Exit] TopMost managed by ViewModel binding");
            }
        }

        /// <summary>
        /// MainWindow 내에서의 키 입력 처리 (디버그 모드용)
        /// </summary>
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // 설정에서 핫키가 활성화되어 있고, 해당 키가 눌렸을 때
                if (_viewModel.HotkeyEnabled)
                {
                    // F11 또는 설정된 키 확인
                    if ((_viewModel.HotkeyKey == "F11" && e.Key == Key.F11) ||
                        (_viewModel.HotkeyKey == "Home" && e.Key == Key.Home) ||
                        (_viewModel.HotkeyKey == "F12" && e.Key == Key.F12) ||
                        (_viewModel.HotkeyKey == "F10" && e.Key == Key.F10) ||
                        (_viewModel.HotkeyKey == "F9" && e.Key == Key.F9))
                    {
                        Logger.SimpleLog($"MainWindow KeyDown detected: {e.Key}");

                        // 트레이로 숨기기/열기 토글
                        if (this.IsVisible)
                        {
                            HideWindowToTray();
                        }
                        else
                        {
                            ShowWindowFromTray();
                        }

                        // 이벤트 처리 완료 표시
                        e.Handled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow_PreviewKeyDown error", ex);
            }
        }

        // 시작 시 IsAlwaysOnTop 설정 적용 (더 이상 필요 없음 - ViewModel 바인딩으로 처리)
        private void ApplyTopmostSettings()
        {
            // ViewModel의 IsAlwaysOnTop과 IsTopmost 바인딩으로 자동 처리됨
            Logger.SimpleLog("[ApplyTopmostSettings] TopMost state managed by ViewModel binding");
        }

        // 트레이에서 창 복원 (포커스를 가져가지 않음 - 게임 플레이 끊김 방지)
        private void ShowWindowFromTray()
        {
            try
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;

                // 1. WPF Show() 호출하여 레이아웃 활성화 (이것이 없으면 창이 표시되지 않음)
                this.Show();
                this.WindowState = WindowState.Normal;

                // 2. 즉시 ShowWindow를 SW_SHOWNOACTIVATE로 호출하여 포커스 제거
                PInvoke.ShowWindow(handle, PInvoke.SW_SHOWNOACTIVATE);

                // 3. SetWindowPos로 TopMost 설정 (SWP_NOACTIVATE 플래그로 포커스 가져가지 않음)
                if (_viewModel.IsAlwaysOnTop || _viewModel.IsCompactMode)
                {
                    PInvoke.SetWindowPos(
                        handle,
                        PInvoke.HWND_TOPMOST,
                        0, 0, 0, 0,
                        PInvoke.SWP_NOMOVE | PInvoke.SWP_NOSIZE | PInvoke.SWP_NOACTIVATE
                    );
                    Logger.SimpleLog("[ShowWindowFromTray] TopMost set without stealing focus");
                }

                // 4. 핀 모드가 활성화된 경우 TopBar를 숨긴 상태로 시작
                //    TopBarAnimationBehavior가 Activated 이벤트에서 애니메이션을 처리함
                if (_viewModel.IsAlwaysOnTop)
                {
                    // 즉시 TopBar 숨김 (애니메이션 없이)
                    TopBarTransform.Y = -20;
                    WebViewContainer.Margin = new Thickness(0, 0, 0, 0);
                }

                Logger.SimpleLog("[ShowWindowFromTray] Window shown without stealing focus");
            }
            catch (Exception ex)
            {
                Logger.Error("[ShowWindowFromTray] Failed to show window", ex);
            }
        }

        // 창을 트레이로 숨김
        private void HideWindowToTray()
        {
            try
            {
                this.Hide();
                Logger.SimpleLog("[HideWindowToTray] Window hidden to tray");
            }
            catch (Exception ex)
            {
                Logger.Error("[HideWindowToTray] Failed to hide window", ex);
            }
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

        // 핫키 매니저 초기화 (전역 단축키용)
        private void InitializeHotkeyManager()
        {
            try
            {
                _hotkeyManager = new HotkeyManager(this);

                // 핫키 등록
                if (_viewModel.HotkeyEnabled && !string.IsNullOrEmpty(_viewModel.HotkeyKey))
                {
                    _hotkeyManager.RegisterHotkey(_viewModel.HotkeyKey, () =>
                    {
                        Logger.SimpleLog("Global hotkey triggered - Toggle tray visibility");

                        Dispatcher.Invoke(() =>
                        {
                            if (this.IsVisible)
                            {
                                HideWindowToTray();
                            }
                            else
                            {
                                ShowWindowFromTray();
                            }
                        });
                    });

                    Logger.SimpleLog($"Hotkey registered: {_viewModel.HotkeyKey}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize HotkeyManager", ex);
            }
        }

        // 핫키 설정 업데이트 (SettingsPage에서 호출)
        public void UpdateHotkeySettings()
        {
            try
            {
                // 기존 핫키 매니저 정리
                _hotkeyManager?.Dispose();

                // ViewModel의 설정 다시 로드
                _viewModel.LoadSettings();

                // 핫키 매니저 재초기화
                InitializeHotkeyManager();

                Logger.SimpleLog("Hotkey settings updated");
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
                    Bounds = new Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight),
                    IsCompactMode = _viewModel.IsCompactMode
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
                Bounds = new Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight),
                IsCompactMode = _viewModel.IsCompactMode
            });
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            // ViewModel 이벤트 구독 해제
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            // 핫키 매니저 정리
            _hotkeyManager?.Dispose();
        }
    }
}
