using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using TanukiTarkovMap.Models.Data;
using TanukiTarkovMap.Models.JavaScript;
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

        private WebView2 _webView;
        private MainWindowViewModel _viewModel;
        private WebViewUIService _webViewUIService;
        private WindowBoundsService _windowBoundsService;
        private HotkeyManager _hotkeyManager;
        private bool _isClampingLocation = false; // 무한 루프 방지
        private bool _isInitializing = true; // 초기화 중 플래그
        private SettingsPage _settingsPage; // 설정 페이지 재사용

        public MainWindow()
        {
            try
            {
                // DI 컨테이너에서 싱글톤 서비스 가져오기
                _webViewUIService = ServiceLocator.WebViewUIService;
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
                    MapMarkers.ADD_DIRECTION_INDICATORS_SCRIPT
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
                    UICustomization.REMOVE_UNWANTED_ELEMENTS_SCRIPT
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

            // IsAlwaysOnTop 설정 적용
            ApplyTopmostSettings();

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
                case nameof(MainWindowViewModel.IsCompactMode):
                    await HandleCompactModeChanged();
                    break;
                case nameof(MainWindowViewModel.CurrentMap):
                    await HandleMapChanged();
                    break;
                case nameof(MainWindowViewModel.SelectedMapInfo):
                    await HandleSelectedMapChanged();
                    break;
                case nameof(MainWindowViewModel.HideWebElements):
                    await HandleHideWebElementsChanged();
                    break;
                case nameof(MainWindowViewModel.SelectedZoomLevel):
                    HandleZoomLevelChanged();
                    break;
            }
        }

        /// <summary>
        /// Compact 모드 변경 처리
        /// </summary>
        private async Task HandleCompactModeChanged()
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

                // Logger.SimpleLog($"[Compact Entry] Position validated: ({validatedPosition.X}, {validatedPosition.Y})");

                // Compact 모드 진입 시 JavaScript 적용
                if (_webView != null)
                {
                    await _webViewUIService.ApplyUIVisibilityAsync(_webView, _viewModel.CurrentMap, _viewModel.HideWebElements);
                }

                // Topmost는 ViewModel의 IsTopmost 바인딩으로 자동 처리됨
                // Logger.SimpleLog("[Compact Entry] TopMost managed by ViewModel binding");
            }
            else
            {
                // Compact 모드 종료 시 화면 정보 초기화
                _windowBoundsService.ClearCompactModeScreen();

                // 일반 모드 복원 시 UI 요소 숨김 설정 반영
                if (_webView != null)
                {
                    string mapId = _viewModel.CurrentMap ?? "default";
                    await _webViewUIService.ApplyUIVisibilityAsync(_webView, mapId, _viewModel.HideWebElements);
                    Logger.SimpleLog($"[ExitCompactMode] Applied UI visibility setting: mapId={mapId}, hideElements={_viewModel.HideWebElements}");
                }

                // Topmost는 ViewModel의 IsTopmost 바인딩으로 자동 처리됨 (핀 설정에 따라)
                Logger.SimpleLog("[Compact Exit] TopMost managed by ViewModel binding");
            }
        }

        /// <summary>
        /// 맵 변경 처리
        /// </summary>
        private async Task HandleMapChanged()
        {
            if (_viewModel.IsCompactMode && !string.IsNullOrEmpty(_viewModel.CurrentMap))
            {
                if (_webView != null)
                {
                    await _webViewUIService.ApplyUIVisibilityAsync(_webView, _viewModel.CurrentMap, _viewModel.HideWebElements);
                }
            }
        }

        /// <summary>
        /// UI 요소 숨김 설정 변경 처리
        /// </summary>
        private async Task HandleHideWebElementsChanged()
        {
            Logger.SimpleLog($"[HandleHideWebElementsChanged] HideWebElements changed to: {_viewModel.HideWebElements}");

            // Compact 모드 여부와 관계없이 UI 요소 숨김/복원 JavaScript 적용
            if (_webView?.CoreWebView2 != null)
            {
                // CurrentMap이 null이어도 UI 요소 숨김/복원은 가능
                string mapId = _viewModel.CurrentMap ?? "default";
                Logger.SimpleLog($"[HandleHideWebElementsChanged] Applying UI elements visibility change (mapId: {mapId})");

                try
                {
                    await _webViewUIService.ApplyUIVisibilityAsync(_webView, mapId, _viewModel.HideWebElements);
                }
                catch (Exception ex)
                {
                    Logger.Error("[HandleHideWebElementsChanged] Error applying UI visibility change", ex);
                }
            }
            else
            {
                Logger.SimpleLog("[HandleHideWebElementsChanged] WebView2 not ready, skipping");
            }
        }

        /// <summary>
        /// WebView 배율 변경 처리
        /// </summary>
        private void HandleZoomLevelChanged()
        {
            if (_webView?.CoreWebView2 != null)
            {
                try
                {
                    // ZoomFactor는 백분율을 소수로 변환 (100% = 1.0)
                    double zoomFactor = _viewModel.SelectedZoomLevel / 100.0;
                    _webView.ZoomFactor = zoomFactor;
                    Logger.SimpleLog($"[HandleZoomLevelChanged] Zoom level changed to: {_viewModel.SelectedZoomLevel}% (ZoomFactor: {zoomFactor})");
                }
                catch (Exception ex)
                {
                    Logger.Error("[HandleZoomLevelChanged] Error applying zoom level", ex);
                }
            }
            else
            {
                Logger.SimpleLog("[HandleZoomLevelChanged] WebView2 not ready, will apply zoom when initialized");
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

        // WebView2 초기화 (단일 WebView)
        private async Task InitializeWebView()
        {
            try
            {
                Logger.SimpleLog("InitializeWebView: Start");

                // WebView2 생성
                _webView = new WebView2
                {
                    DefaultBackgroundColor = System.Drawing.Color.FromArgb(26, 26, 26),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Width = double.NaN,  // 자동 크기 조정
                    Height = double.NaN  // 자동 크기 조정
                };

                // WebViewContainer에 추가
                WebViewContainer.Child = _webView;

                // WebViewContainer SizeChanged 이벤트 핸들러 등록 (둥근 모서리 클리핑용)
                WebViewContainer.SizeChanged += WebViewContainer_SizeChanged;

                // 초기 클리핑 영역 설정
                ApplyWebViewClipping();

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

                // CoreWebView2 초기화 완료 후 클리핑 재적용
                ApplyWebViewClipping();
                Logger.SimpleLog("InitializeWebView: Clipping applied after CoreWebView2 init");

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
            settings.AreDevToolsEnabled = true;
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

                // 저장된 배율 적용
                double zoomFactor = _viewModel.SelectedZoomLevel / 100.0;
                webView.ZoomFactor = zoomFactor;
                Logger.SimpleLog($"[WebView_NavigationCompleted] Applied initial zoom: {_viewModel.SelectedZoomLevel}% (ZoomFactor: {zoomFactor})");

                // 기본 작업들
                await RemoveUnwantedElements(webView);

                // 웹 페이지 마진/패딩 제거
                await webView.CoreWebView2.ExecuteScriptAsync(
                    PageLayout.REMOVE_PAGE_MARGINS_SCRIPT
                );

                // Tarkov Market 전용 처리
                if (webView.Source?.ToString().Contains("tarkov-market.com") == true)
                {
                    // 방향 표시기 추가
                    await AddDirectionIndicators(webView);

                    // UI 요소 숨김 설정 적용 (항상 적용)
                    string mapId = _viewModel.CurrentMap ?? "default";
                    await _webViewUIService.ApplyUIVisibilityAsync(webView, mapId, _viewModel.HideWebElements);
                    Logger.SimpleLog($"[WebView_NavigationCompleted] Applied UI visibility setting: mapId={mapId}, hideElements={_viewModel.HideWebElements}");

                    // "/pilot" 페이지에서 Connected 상태 감지 시작
                    if (webView.Source?.ToString().Contains("/pilot") == true)
                    {
                        await webView.CoreWebView2.ExecuteScriptAsync(
                            ConnectionDetector.DETECT_CONNECTION_STATUS
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
                            else if (messageType == "margins-removed")
                            {
                                Logger.SimpleLog("[CoreWebView2_WebMessageReceived] Margins removed, triggering WebView resize");

                                Dispatcher.Invoke(() =>
                                {
                                    // WebView를 임시로 리사이즈하여 맵 재렌더링 강제
                                    TriggerWebViewResize();
                                });
                            }
                            else if (messageType == "ui-elements-removed")
                            {
                                Logger.SimpleLog("[CoreWebView2_WebMessageReceived] UI elements removed, triggering WebView resize");

                                Dispatcher.Invoke(() =>
                                {
                                    // WebView를 임시로 리사이즈하여 맵 재렌더링 강제
                                    TriggerWebViewResize();
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

        // Note: Settings_Click, CloseSettings_Click, PinToggle_Click은
        // ViewModel의 Command로 대체되었습니다.
        // (ToggleSettingsCommand, CloseSettingsCommand, TogglePinModeCommand)

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
                Models.Utils.PInvoke.ShowWindow(handle, Models.Utils.PInvoke.SW_SHOWNOACTIVATE);

                // 3. SetWindowPos로 TopMost 설정 (SWP_NOACTIVATE 플래그로 포커스 가져가지 않음)
                if (_viewModel.IsAlwaysOnTop || _viewModel.IsCompactMode)
                {
                    Models.Utils.PInvoke.SetWindowPos(
                        handle,
                        Models.Utils.PInvoke.HWND_TOPMOST,
                        0, 0, 0, 0,
                        Models.Utils.PInvoke.SWP_NOMOVE | Models.Utils.PInvoke.SWP_NOSIZE | Models.Utils.PInvoke.SWP_NOACTIVATE
                    );
                    Logger.SimpleLog("[ShowWindowFromTray] TopMost set without stealing focus");
                }

                // 4. 핀 모드가 활성화된 경우 TopBar를 숨긴 상태로 시작
                //    (창이 포커스 없이 표시되므로, 사용자가 클릭할 때 Activated 이벤트에서 TopBar가 나타남)
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

        // Note: TitleBar_MouseLeftButtonDown은 WindowDragBehavior로 대체되었습니다.
        // Note: Minimize_Click, MaximizeRestore_Click, Close_Click은 WindowControlBehavior로 대체되었습니다.

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
                                // SW_SHOWNOACTIVATE를 사용하여 포커스를 가져가지 않음
                                // 게임 중 W키 등이 끊기지 않고, 프레임 드롭도 방지됨
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
        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            if (_isClampingLocation) return;        // 무한 루프 방지
            if (_isInitializing) return;            // 초기화 중에는 무시

            try
            {
                _isClampingLocation = true;

                // ⚠️ 임시로 비활성화: 화면 경계 체크 버그로 인해 주석 처리
                /*
                // 화면 경계 체크 수행 (모니터가 없는 영역으로 나가면 되돌림)
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
                */

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

        /// <summary>
        /// WebViewContainer 크기 변경 시 WebView 크기 업데이트 및 클리핑 영역 업데이트
        /// </summary>
        private void WebViewContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // WebView2 크기를 컨테이너 크기에 맞춤
            if (_webView != null && WebViewContainer.ActualWidth > 0 && WebViewContainer.ActualHeight > 0)
            {
                _webView.Width = WebViewContainer.ActualWidth;
                _webView.Height = WebViewContainer.ActualHeight;
                // Logger.SimpleLog($"[WebViewContainer_SizeChanged] Updated WebView size to {_webView.Width}x{_webView.Height}");
            }

            ApplyWebViewClipping();
        }

        /// <summary>
        /// WebView2에 둥근 모서리 클리핑 적용 (Airspace 문제 해결)
        /// </summary>
        private void ApplyWebViewClipping()
        {
            if (_webView == null || WebViewContainer.ActualWidth <= 0 || WebViewContainer.ActualHeight <= 0)
                return;

            try
            {
                // 8px 둥근 모서리를 가진 RectangleGeometry 생성
                var clipGeometry = new RectangleGeometry
                {
                    Rect = new Rect(0, 0, WebViewContainer.ActualWidth, WebViewContainer.ActualHeight),
                    RadiusX = 8,
                    RadiusY = 8
                };

                // WebView2에 클리핑 적용
                _webView.Clip = clipGeometry;

                // Logger.SimpleLog($"[ApplyWebViewClipping] Applied clipping: {WebViewContainer.ActualWidth}x{WebViewContainer.ActualHeight}");
            }
            catch (Exception ex)
            {
                Logger.Error("[ApplyWebViewClipping] Failed to apply clipping", ex);
            }
        }

        /// <summary>
        /// WebView를 임시로 리사이즈하여 맵 재렌더링 강제
        /// 마진 제거 후 맵이 새로운 공간을 채우도록 함
        /// </summary>
        private void TriggerWebViewResize()
        {
            try
            {
                if (_webView == null || WebViewContainer.ActualHeight <= 0) return;

                // Logger.SimpleLog("[TriggerWebViewResize] Starting temporary resize");

                // 현재 컨테이너 높이 저장
                var containerHeight = WebViewContainer.ActualHeight;

                // Height를 1px 증가
                _webView.Height = containerHeight + 1;

                // 레이아웃 업데이트 강제
                _webView.UpdateLayout();

                // Logger.SimpleLog($"[TriggerWebViewResize] Increased height to {_webView.Height}");

                // 100ms 후 컨테이너 크기로 복원
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // 컨테이너의 현재 크기로 복원 (변경되었을 수 있음)
                        _webView.Width = WebViewContainer.ActualWidth;
                        _webView.Height = WebViewContainer.ActualHeight;
                        _webView.UpdateLayout();
                        // Logger.SimpleLog($"[TriggerWebViewResize] Restored to container size: {_webView.Width}x{_webView.Height}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("[TriggerWebViewResize] Failed to restore size", ex);
                    }
                }), System.Windows.Threading.DispatcherPriority.Background, null);
            }
            catch (Exception ex)
            {
                Logger.Error("[TriggerWebViewResize] Failed to trigger resize", ex);
            }
        }

        // Note: Window_MouseLeftButtonDown은 CompactModeDragBehavior로 대체되었습니다.
        // Note: MainWindow_Activated, MainWindow_Deactivated, MainWindow_MouseEnter,
        //       MainWindow_MouseLeave, AnimateTopBar는 TopBarAnimationBehavior로 대체되었습니다.

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