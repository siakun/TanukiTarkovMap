using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using TanukiTarkovMap.Models.Constants;
using TanukiTarkovMap.Models.Data;
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
        public bool IsPipMode { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // 창 위치/크기 변경 이벤트
        public event EventHandler<WindowBoundsChangedEventArgs>? WindowBoundsChanged;

        private WebView2 _webView;
        private MainWindowViewModel _viewModel;
        private PipService _pipService;
        private WindowBoundsService _windowBoundsService;
        private HotkeyManager _hotkeyManager;
        private bool _isClampingLocation = false; // 무한 루프 방지
        private bool _isInitializing = true; // 초기화 중 플래그
        private SettingsPage _settingsPage; // 설정 페이지 재사용

        public MainWindow()
        {
            try
            {
                // 서비스 초기화
                _pipService = new PipService();
                _windowBoundsService = new WindowBoundsService();

                // ViewModel 초기화 (서비스 주입)
                _viewModel = new MainWindowViewModel(_pipService, _windowBoundsService);

                // DataContext를 InitializeComponent 전에 설정하여 바인딩이 즉시 작동하도록 함
                DataContext = _viewModel;

                InitializeComponent();

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
            }
            catch (Exception)
            {
                throw;
            }
        }

        // 페이지 제목 업데이트
        private static void UpdateWindowTitle(Window window, string title)
        {
            if (!string.IsNullOrEmpty(title))
            {
                // "Tarkov Pilot"를 "Tarkov Client"로 변경
                string displayTitle = title.Replace("Tarkov Pilot", "Tarkov Client");
                window.Title = displayTitle;
            }
        }

        // Tarkov Market Map 방향 표시기 추가 (탭별)
        private static async Task AddDirectionIndicators(WebView2 webView)
        {
            try
            {
                await Task.Delay(2000); // 페이지 로딩 완료 대기
                await webView.CoreWebView2.ExecuteScriptAsync(
                    JavaScriptConstants.ADD_DIRECTION_INDICATORS_SCRIPT
                );
            }
            catch (Exception)
            {
                // 에러 처리
            }
        }

        // 불필요한 UI 요소 제거 (탭별)
        private static async Task RemoveUnwantedElements(WebView2 webView)
        {
            try
            {
                await webView.CoreWebView2.ExecuteScriptAsync(
                    JavaScriptConstants.REMOVE_UNWANTED_ELEMENTS_SCRIPT
                );
            }
            catch (Exception)
            {
                // 에러 처리
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 로딩 패널 강제 숨김 (디버그용)
            LoadingPanel.Visibility = Visibility.Collapsed;

            // 저장된 창 크기 다시 적용 (WPF가 자동으로 변경한 크기 복원)
            this.Width = _viewModel.CurrentWindowWidth;
            this.Height = _viewModel.CurrentWindowHeight;
            Logger.SimpleLog($"[MainWindow_Loaded] Restored window size: {this.Width}x{this.Height}");

            // 초기화 완료 - 이제부터 OnWindowBoundsChanged가 정상 동작
            _isInitializing = false;

            await InitializeWebView();

            // ViewModel이 PIP 모드 변경을 처리하도록 PropertyChanged 구독
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // 핫키 매니저 초기화 (전역 단축키용)
            InitializeHotkeyManager();

            // 설정 페이지 초기화
            InitializeSettingsPage();
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
        private async void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.IsPipMode):
                    await HandlePipModeChanged();
                    break;
                case nameof(MainWindowViewModel.CurrentMap):
                    await HandleMapChanged();
                    break;
                case nameof(MainWindowViewModel.SelectedMapInfo):
                    await HandleSelectedMapChanged();
                    break;
                case nameof(MainWindowViewModel.PipHideWebElements):
                    await HandlePipHideWebElementsChanged();
                    break;
            }
        }

        /// <summary>
        /// PIP 모드 변경 처리
        /// </summary>
        private async Task HandlePipModeChanged()
        {
            if (_viewModel.IsPipMode)
            {
                // PIP 모드 시작 시 현재 화면 저장 (LocationChanged에서 경계 체크에 사용)
                var windowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                _windowBoundsService.SavePipModeScreen(windowHandle);

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

                Logger.SimpleLog($"[PIP Entry] Position validated: ({validatedPosition.X}, {validatedPosition.Y})");

                // PIP 모드 진입 시 JavaScript 적용
                if (_webView != null)
                {
                    await _pipService.ApplyPipModeJavaScriptAsync(_webView, _viewModel.CurrentMap, _viewModel.PipHideWebElements);
                }

                // Topmost 설정 (Win32 API)
                WindowTopmost.SetTopmost(this);
            }
            else
            {
                // PIP 모드 종료 시 화면 정보 초기화
                _windowBoundsService.ClearPipModeScreen();

                // 일반 모드 복원 시 UI 요소 숨김 설정 반영
                if (_webView != null)
                {
                    string mapId = _viewModel.CurrentMap ?? "default";
                    await _pipService.ApplyPipModeJavaScriptAsync(_webView, mapId, _viewModel.PipHideWebElements);
                    Logger.SimpleLog($"[ExitPipMode] Applied UI visibility setting: mapId={mapId}, hideElements={_viewModel.PipHideWebElements}");
                }

                // Topmost 해제 (Win32 API)
                WindowTopmost.RemoveTopmost(this);
            }
        }

        /// <summary>
        /// 맵 변경 처리
        /// </summary>
        private async Task HandleMapChanged()
        {
            if (_viewModel.IsPipMode && !string.IsNullOrEmpty(_viewModel.CurrentMap))
            {
                if (_webView != null)
                {
                    await _pipService.ApplyPipModeJavaScriptAsync(_webView, _viewModel.CurrentMap, _viewModel.PipHideWebElements);
                }
            }
        }

        /// <summary>
        /// PIP 모드 UI 요소 숨김 설정 변경 처리
        /// </summary>
        private async Task HandlePipHideWebElementsChanged()
        {
            Logger.SimpleLog($"[HandlePipHideWebElementsChanged] PipHideWebElements changed to: {_viewModel.PipHideWebElements}");

            // PIP 모드 여부와 관계없이 UI 요소 숨김/복원 JavaScript 적용
            if (_webView?.CoreWebView2 != null)
            {
                // CurrentMap이 null이어도 UI 요소 숨김/복원은 가능
                string mapId = _viewModel.CurrentMap ?? "default";
                Logger.SimpleLog($"[HandlePipHideWebElementsChanged] Applying UI elements visibility change (mapId: {mapId})");

                try
                {
                    await _pipService.ApplyPipModeJavaScriptAsync(_webView, mapId, _viewModel.PipHideWebElements);
                }
                catch (Exception ex)
                {
                    Logger.Error("[HandlePipHideWebElementsChanged] Error applying UI visibility change", ex);
                }
            }
            else
            {
                Logger.SimpleLog("[HandlePipHideWebElementsChanged] WebView2 not ready, skipping");
            }
        }

        /// <summary>
        /// 맵 선택 드롭다운 변경 처리
        /// </summary>
        private async Task HandleSelectedMapChanged()
        {
            if (_viewModel.SelectedMapInfo != null)
            {
                // CurrentMap을 MapId로 업데이트 (PIP 모드 JavaScript가 올바른 transform을 사용하도록)
                _viewModel.CurrentMap = _viewModel.SelectedMapInfo.MapId;
                Logger.SimpleLog($"[HandleSelectedMapChanged] CurrentMap set to: {_viewModel.CurrentMap}");

                try
                {
                    if (_webView?.CoreWebView2 != null)
                    {
                        Logger.SimpleLog($"[HandleSelectedMapChanged] Navigating to: {_viewModel.SelectedMapInfo.Url}");
                        _webView.CoreWebView2.Navigate(_viewModel.SelectedMapInfo.Url);
                    }
                }
                catch (ObjectDisposedException)
                {
                    Logger.SimpleLog("[HandleSelectedMapChanged] WebView2 already disposed, skipping navigation");
                }
                catch (Exception ex)
                {
                    Logger.Error("[HandleSelectedMapChanged] Navigation error", ex);
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// MainWindow 내에서의 키 입력 처리 (디버그 모드용)
        /// </summary>
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // 설정에서 핫키가 활성화되어 있고, 해당 키가 눌렸을 때
                if (_viewModel.PipHotkeyEnabled)
                {
                    // F11 또는 설정된 키 확인
                    if ((_viewModel.PipHotkeyKey == "F11" && e.Key == Key.F11) ||
                        (_viewModel.PipHotkeyKey == "Home" && e.Key == Key.Home) ||
                        (_viewModel.PipHotkeyKey == "F12" && e.Key == Key.F12) ||
                        (_viewModel.PipHotkeyKey == "F10" && e.Key == Key.F10) ||
                        (_viewModel.PipHotkeyKey == "F9" && e.Key == Key.F9))
                    {
                        Logger.SimpleLog($"MainWindow KeyDown detected: {e.Key}");

                        // PIP 모드 토글 Command 실행
                        _viewModel.TogglePipModeCommand.Execute(null);

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

        // WebView2 초기화 (단일 WebView)
        private async Task InitializeWebView()
        {
            try
            {
                Logger.SimpleLog("InitializeWebView: Start");

                // WebView2 생성
                _webView = new WebView2
                {
                    DefaultBackgroundColor = System.Drawing.Color.FromArgb(26, 26, 26)
                };

                // WebViewContainer에 추가
                WebViewContainer.Child = _webView;

                // UserDataFolder 설정
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TarkovClient",
                    "WebView2"
                );
                Logger.SimpleLog($"InitializeWebView: UserDataFolder = {userDataFolder}");

                // CoreWebView2 환경 생성
                Logger.SimpleLog("InitializeWebView: Creating CoreWebView2Environment");
                var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                Logger.SimpleLog("InitializeWebView: Environment created");

                // CoreWebView2 초기화
                Logger.SimpleLog("InitializeWebView: Calling EnsureCoreWebView2Async");
                await _webView.EnsureCoreWebView2Async(environment);
                Logger.SimpleLog("InitializeWebView: CoreWebView2 initialized");

                // WebView2 설정
                ConfigureWebView2Settings(_webView);
                Logger.SimpleLog("InitializeWebView: Settings configured");

                // 이벤트 핸들러 등록
                _webView.NavigationCompleted += WebView_NavigationCompleted;
                Logger.SimpleLog("InitializeWebView: Event handlers registered");

                // URL 로드
                _webView.Source = new Uri(App.WebsiteUrl);
            }
            catch (Exception ex)
            {
                Logger.Error("InitializeWebView failed", ex);
                throw;
            }
        }

        // WebView2 설정 구성
        private static void ConfigureWebView2Settings(WebView2 webView)
        {
            var settings = webView.CoreWebView2.Settings;

            settings.IsScriptEnabled = true;
            settings.AreDefaultScriptDialogsEnabled = false;
            settings.IsWebMessageEnabled = true;
            settings.AreDevToolsEnabled = false;
            settings.AreDefaultContextMenusEnabled = false;

            settings.IsZoomControlEnabled = true;
            settings.IsPasswordAutosaveEnabled = false;
            settings.IsGeneralAutofillEnabled = false;
        }

        // 페이지 로딩 완료 시 처리
        private async void WebView_NavigationCompleted(
            object sender,
            CoreWebView2NavigationCompletedEventArgs e
        )
        {
            if (!e.IsSuccess)
                return;

            var webView = sender as WebView2;
            if (webView?.CoreWebView2 == null)
                return;

            try
            {
                // 페이지 제목 가져오기 및 업데이트
                var title = await webView.CoreWebView2.ExecuteScriptAsync("document.title");
                UpdateWindowTitle(this, title?.Trim('"'));

                // WebSocket 통신을 위한 메시지 핸들러 등록
                webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                // 기본 작업들
                await RemoveUnwantedElements(webView);

                // Tarkov Market 전용 처리
                if (webView.Source?.ToString().Contains("tarkov-market.com") == true)
                {
                    // 방향 표시기 추가
                    await AddDirectionIndicators(webView);

                    // UI 요소 숨김 설정 적용 (항상 적용)
                    string mapId = _viewModel.CurrentMap ?? "default";
                    await _pipService.ApplyPipModeJavaScriptAsync(webView, mapId, _viewModel.PipHideWebElements);
                    Logger.SimpleLog($"[WebView_NavigationCompleted] Applied UI visibility setting: mapId={mapId}, hideElements={_viewModel.PipHideWebElements}");

                    // "/pilot" 페이지에서 Connected 상태 감지 시작
                    if (webView.Source?.ToString().Contains("/pilot") == true)
                    {
                        await webView.CoreWebView2.ExecuteScriptAsync(
                            JavaScriptConstants.DETECT_CONNECTION_STATUS
                        );
                        Logger.SimpleLog("[Navigation] Connection detection script injected");
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // WebView2가 이미 dispose된 경우 무시
                Logger.SimpleLog("[WebView_NavigationCompleted] WebView2 already disposed, skipping");
            }
            catch (Exception ex)
            {
                Logger.Error("[WebView_NavigationCompleted] Unexpected error", ex);
            }
        }

        // WebSocket 메시지 수신 처리 (맵 정보 수신)
        private void CoreWebView2_WebMessageReceived(
            object sender,
            CoreWebView2WebMessageReceivedEventArgs e
        )
        {
            try
            {
                var message = e.TryGetWebMessageAsString();
                if (!string.IsNullOrEmpty(message))
                {
                    // 맵 정보 파싱 (예: "map:customs_preset")
                    if (message.StartsWith("map:"))
                    {
                        var mapName = message.Substring(4);
                        Logger.SimpleLog($"Map received from WebSocket: {mapName}");

                        // ViewModel의 CurrentMap 업데이트
                        Dispatcher.Invoke(() =>
                        {
                            _viewModel.CurrentMap = mapName;
                        });
                    }
                    // JSON 메시지 처리 (JavaScript에서 보낸 메시지)
                    else if (message.StartsWith("{"))
                    {
                        try
                        {
                            var json = System.Text.Json.JsonDocument.Parse(message);
                            var messageType = json.RootElement.GetProperty("type").GetString();

                            if (messageType == "pilot-connected")
                            {
                                Logger.SimpleLog("[CoreWebView2_WebMessageReceived] Pilot connected detected!");

                                Dispatcher.Invoke(() =>
                                {
                                    if (_viewModel.SelectedMapInfo == null)
                                    {
                                        // 맵이 선택되어 있지 않으면 기본 맵으로 이동
                                        var defaultMap = App.AvailableMaps.FirstOrDefault();
                                        if (defaultMap != null)
                                        {
                                            _viewModel.SelectedMapInfo = defaultMap;
                                            Logger.SimpleLog($"[CoreWebView2_WebMessageReceived] Auto-navigating to default map: {defaultMap.DisplayName}");
                                        }
                                    }
                                    else
                                    {
                                        // 맵이 이미 선택되어 있으면 해당 맵으로 네비게이션 수행
                                        Logger.SimpleLog($"[CoreWebView2_WebMessageReceived] Navigating to selected map: {_viewModel.SelectedMapInfo.DisplayName}");

                                        if (_webView?.CoreWebView2 != null)
                                        {
                                            _webView.CoreWebView2.Navigate(_viewModel.SelectedMapInfo.Url);
                                        }
                                    }
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("[CoreWebView2_WebMessageReceived] JSON parsing error", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("[CoreWebView2_WebMessageReceived] Error", ex);
            }
        }

        // 설정 버튼 클릭 (토글)
        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            // 설정 오버레이 토글
            if (SettingsOverlay.Visibility == Visibility.Collapsed)
            {
                // 설정 열기 - WebView 숨김 (Airspace 문제 해결)
                WebViewContainer.Visibility = Visibility.Collapsed;
                SettingsOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                // 설정 닫기 - WebView 복원
                SettingsOverlay.Visibility = Visibility.Collapsed;
                WebViewContainer.Visibility = Visibility.Visible;
            }
        }

        // 설정 닫기 버튼 클릭
        private void CloseSettings_Click(object sender, RoutedEventArgs e)
        {
            // 설정 닫기 - WebView 복원
            SettingsOverlay.Visibility = Visibility.Collapsed;
            WebViewContainer.Visibility = Visibility.Visible;
        }

        // 타이틀바 드래그로 창 이동
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                // 더블클릭 시 최대화/복원
                if (e.ClickCount == 2)
                {
                    MaximizeRestore_Click(sender, e);
                }
                // 단일 클릭 시 드래그 이동
                else
                {
                    PInvoke.ReleaseCapture();
                    PInvoke.SendMessage(
                        new System.Windows.Interop.WindowInteropHelper(this).Handle,
                        PInvoke.WM_NCLBUTTONDOWN,
                        PInvoke.HT_CAPTION,
                        0
                    );
                }
            }
        }

        // 최소화 버튼
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        // 최대화/복원 버튼
        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
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

        // 닫기 버튼
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // 핫키 매니저 초기화 (전역 단축키용)
        private void InitializeHotkeyManager()
        {
            try
            {
                _hotkeyManager = new HotkeyManager(this);

                // PIP 핫키 등록
                if (_viewModel.PipHotkeyEnabled && !string.IsNullOrEmpty(_viewModel.PipHotkeyKey))
                {
                    _hotkeyManager.RegisterHotkey(_viewModel.PipHotkeyKey, () =>
                    {
                        Logger.SimpleLog("Global hotkey triggered");
                        Dispatcher.Invoke(() =>
                        {
                            _viewModel.TogglePipModeCommand.Execute(null);
                        });
                    });

                    Logger.SimpleLog($"Hotkey registered: {_viewModel.PipHotkeyKey}");
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
        /// 창 위치 변경 시 ViewModel 즉시 업데이트 및 PIP 모드에서 화면 경계 체크
        /// </summary>
        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            if (_isClampingLocation) return;        // 무한 루프 방지
            if (_isInitializing) return;            // 초기화 중에는 무시

            try
            {
                _isClampingLocation = true;

                // PIP 모드에서는 화면 경계 체크 수행
                if (_viewModel.IsPipMode)
                {
                    var dpiScale = VisualTreeHelper.GetDpi(this);

                    // WindowBoundsService를 사용하여 창 위치 체크 및 조정
                    var newPosition = _windowBoundsService.ClampWindowPosition(
                        this.Left,
                        this.Top,
                        this.ActualWidth,
                        this.ActualHeight,
                        dpiScale.DpiScaleX,
                        dpiScale.DpiScaleY
                    );

                    // 조정이 필요한 경우 위치 업데이트
                    if (newPosition.HasValue)
                    {
                        this.Left = newPosition.Value.X;
                        this.Top = newPosition.Value.Y;
                    }
                }

                // 창 위치/크기 변경 이벤트 발생 (ViewModel에서 즉시 저장)
                WindowBoundsChanged?.Invoke(this, new WindowBoundsChangedEventArgs
                {
                    Bounds = new Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight),
                    IsPipMode = _viewModel.IsPipMode
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
                IsPipMode = _viewModel.IsPipMode
            });
        }

        // PIP 모드에서 창 드래그 이동 처리
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // PIP 모드일 때만 드래그 가능
            if (_viewModel.IsPipMode && e.ButtonState == MouseButtonState.Pressed)
            {
                // WebView2 영역 외부를 클릭한 경우만 드래그
                // (WebView2 내부에서는 맵 상호작용을 위해 드래그 비활성화)
                var position = e.GetPosition(this);
                var hitElement = this.InputHitTest(position) as DependencyObject;

                // WebView2가 아닌 경우에만 드래그 허용
                bool isWebView2 = false;
                while (hitElement != null)
                {
                    if (hitElement is WebView2)
                    {
                        isWebView2 = true;
                        break;
                    }
                    hitElement = System.Windows.Media.VisualTreeHelper.GetParent(hitElement);
                }

                if (!isWebView2)
                {
                    PInvoke.ReleaseCapture();
                    PInvoke.SendMessage(new System.Windows.Interop.WindowInteropHelper(this).Handle,
                               PInvoke.WM_NCLBUTTONDOWN, PInvoke.HT_CAPTION, 0);
                }
            }
        }

        // 창 닫기 시 정리
        private void MainWindow_Closed(object sender, EventArgs e)
        {
            // ViewModel 이벤트 구독 해제
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            // 핫키 매니저 정리
            _hotkeyManager?.Dispose();

            // WebView2 정리
            _webView?.Dispose();
        }
    }
}