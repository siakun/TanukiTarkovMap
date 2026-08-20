using CefSharp;
using CefSharp.Wpf;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using TanukiTarkovMap.Messages;
using TanukiTarkovMap.Models.Data;
using TanukiTarkovMap.Models.JavaScript;
using TanukiTarkovMap.Models.Offline;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Models.Utils;

/**
WebBrowserViewModel - CefSharp 웹 브라우저 제어 ViewModel

Purpose: CEF가 준비된 뒤 시작 맵을 열고, tarkov-market.com의 JavaScript 주입과 메시지 수신을 처리
Architecture: WebBrowserLifecycleBehavior가 XAML의 ChromiumWebBrowser를 SetBrowser()로 전달한다.
ViewModel은 CEF 준비 시점, 주소 상태와 모든 탐색을 관리하고 View는 브라우저를 표시하는 데 그친다.

Core Functionality:
- 브라우저 연결: SetBrowser()로 이벤트를 먼저 구독하고 CEF 준비 완료를 확인
- 탐색 조정: Navigate()가 모든 URL 요청의 준비 상태를 판정하고 준비 전 마지막 요청만 보관
- 시작 탐색: App.StartupUrl을 Navigate()에 전달해 다른 탐색 요청과 같은 준비 경로 사용
- 페이지 로드 후처리: UI 요소 제거, 마진 제거, 줌 적용
- 로컬 모드: 사본으로 응답할 처리기를 브라우저에 붙이고 토글에 따라 켜고 끈 뒤 다시 읽기
- 상태 보고: 로드 시작 시점에 page-health.js를 넣어 페이지 오류와 맵 렌더 여부를 로그로 받기
- JavaScript 통신: CefSharp.PostMessage로 맵 정보/연결 상태 수신
- Pilot 브리지: 게임 사건을 웹 페이지의 window.pilot으로 전달
- Messenger 수신: MainWindowViewModel에서 맵 선택/줌/UI 숨김 설정 수신

State Management:
- _browser: Behavior가 전달한 ChromiumWebBrowser 인스턴스
- _pendingNavigationUrl: CEF 준비 전에 받은 URL 중 마지막 하나, 실행을 시작하면 null로 전환
- Address: AddressChanged/FrameLoadEnd가 갱신하는 현재 관측 주소
- ReadyBrowser: BrowserCore 생성과 폐기 여부로 스크립트 실행 가능 상태 판정
- _archiveFactory: 로컬 모드일 때만 요청을 사본으로 응답한다. 첫 이동이 시작되기 전에 브라우저에
  붙여야 첫 요청부터 사본이 답한다

Method Flow:
  SetBrowser -> 사본 처리기 연결 -> 필요하면 IsBrowserInitializedChanged 구독
             -> 기존 대기 URL 또는 NavigateToStartupUrl
  FrameLoadStart -> page-health.js 주입 (로딩 중에 난 실패를 잡으려면 자원보다 먼저 들어가야 한다)
  Navigate(준비 전) -> _pendingNavigationUrl 교체 -> 로드 보류
  IsBrowserInitializedChanged(true) -> 이벤트 해제 -> 마지막 대기 URL을 Navigate로 재전달
  Navigate(준비됨) -> 대기 URL 제거 -> 현재 주소 중복 확인 -> LoadUrl
  FrameLoadEnd -> Address 갱신 -> 스크립트와 사용자 설정 적용

Message Flow:
  MainWindowViewModel → MapSelectionChangedMessage → NavigateToMap
  MainWindowViewModel → ZoomLevelChangedMessage → ApplyZoomLevel
  MonitorRefreshRateBehavior → MonitorRefreshRateChangedMessage → ApplyWindowlessFrameRate
  MapEventService(ScreenshotTaken/QuestCompleted) → SendToPilot → window.pilot

Design Rationale: 시작 주소는 설정에서 계산하는 App.StartupUrl을 명령 값으로 사용하고, Address는
브라우저가 알려 주는 현재 상태로만 다룬다. 시작, 맵 선택과 디버그 URL을 모두 Navigate()에서
준비 판정해 경로별 누락을 막는다. 준비 전 요청은 큐로 쌓지 않고 마지막 URL 하나만 남겨 사용자가
최종적으로 보려던 페이지를 한 번만 연다. 중복 주소 판정은 준비 완료 뒤에만 적용해 대기 요청을
잘못 버리지 않으면서 이미 열린 페이지의 불필요한 재로드는 막는다.

Historical Context: 2026-08-19 이전에는 UserControl.Loaded에서 SetBrowser() 직후 Address를 다시 읽어
Load()를 호출했다. CEF 초기화가 느린 Windows Sandbox에서는 호출이 사라져 about:blank에 머물렀다.
2026-08-20에는 시작 경로만 준비 상태를 확인하고 메시지 경로의 Navigate()는 곧바로 LoadUrl을 호출해,
CEF 준비 전에 들어온 자동 맵 전환이 사라지는 남은 경합을 모든 탐색의 공통 대기 경로로 합쳤다.
Known Limitations: ChromiumWebBrowser 수명은 WebBrowserUserControl과 같다고 전제한다.

Last Updated: 2026-08-20 | .NET 8.0 / CefSharp 141.0.110 | By 준비 전 탐색 보존과 로컬 모드 병합
*/
namespace TanukiTarkovMap.ViewModels
{
    public partial class WebBrowserViewModel : ObservableObject,
        IRecipient<MapSelectionChangedMessage>,
        IRecipient<HideWebElementsChangedMessage>,
        IRecipient<ZoomLevelChangedMessage>,
        IRecipient<ExtractionFilterChangedMessage>,
        IRecipient<NavigateToUrlMessage>,
        IRecipient<MonitorRefreshRateChangedMessage>,
        IRecipient<LocalMapModeChangedMessage>
    {
        private readonly BrowserUIService _browserUIService;
        private readonly MapEventService _mapEventService;
        private readonly ArchiveResourceRequestHandlerFactory _archiveFactory;
        private ChromiumWebBrowser? _browser;
        private string? _pendingNavigationUrl;

        /// <summary> 디버그 모드 - 모든 JavaScript 주입 비활성화 </summary>
        private bool _isDebugMode = false;

        /// <summary> OSR 페인트 상한 목표값 - 창이 위치한 모니터의 주사율 (기본 60) </summary>
        private int _monitorRefreshRate = 60;

        /// <summary>
        /// 스크립트와 설정을 넣을 수 있는 상태의 브라우저. 아직 준비되지 않았으면 null.
        ///
        /// ChromiumWebBrowser의 IsBrowserInitialized와 Address는 WPF DependencyProperty라서
        /// CEF가 UI 스레드에 따로 게시해 갱신한다. 릴리즈 빌드에서는 첫 페이지 로드가 그 갱신보다
        /// 먼저 처리되는 일이 생겨, 그 값으로 판정하면 페이지 로드 후처리가 통째로 조용히 건너뛰어진다
        /// (0.2.3에서 UI 숨김과 위치 표시가 릴리즈에서만 동작하지 않던 원인).
        /// BrowserCore는 CEF가 만든 실제 인스턴스라 그 지연이 없다
        /// </summary>
        private ChromiumWebBrowser? ReadyBrowser =>
            _browser?.BrowserCore is { IsDisposed: false } ? _browser : null;

        #region Observable Properties

        /// <summary> 브라우저가 마지막으로 알린 현재 URL </summary>
        [ObservableProperty]
        private string _address = "about:blank";

        /// <summary> 페이지 로딩 중 여부 </summary>
        [ObservableProperty]
        private bool _isLoading = true;

        /// <summary> 현재 맵 ID </summary>
        [ObservableProperty]
        private string? _currentMap;

        /// <summary> UI 요소 숨기기 여부 </summary>
        [ObservableProperty]
        private bool _hideWebElements = true;

        /// <summary> 줌 레벨 (%) </summary>
        [ObservableProperty]
        private int _zoomLevel = AppSettings.DefaultBrowserZoomLevel;

        /// <summary> Extraction 필터: true = PMC, false = SCAV </summary>
        [ObservableProperty]
        private bool _isPmcExtraction = true;

        #endregion

        public WebBrowserViewModel()
        {
            _browserUIService = ServiceLocator.BrowserUIService;
            _mapEventService = ServiceLocator.MapEventService;
            _archiveFactory = new ArchiveResourceRequestHandlerFactory(ServiceLocator.MapArchive)
            {
                LocalModeEnabled = App.GetSettings().LocalMapEnabled && App.GetSettings().LocalMapModeActive,
            };

            _zoomLevel = App.GetSettings().EffectiveBrowserZoomLevel;

            // 게임 사건 구독 (감시자 -> 웹 페이지의 window.pilot)
            _mapEventService.ScreenshotTaken += OnScreenshotTaken;
            _mapEventService.QuestCompleted += OnQuestCompleted;

            // Messenger 등록 (MainWindowViewModel로부터 메시지 수신)
            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        /// <summary>
        /// ChromiumWebBrowser 인스턴스 설정 (Behavior에서 호출)
        /// </summary>
        public void SetBrowser(ChromiumWebBrowser browser)
        {
            if (ReferenceEquals(_browser, browser))
            {
                return;
            }

            _browser = browser;

            // 이벤트 핸들러 등록
            _browser.FrameLoadStart += OnFrameLoadStart;
            _browser.FrameLoadEnd += OnFrameLoadEnd;
            _browser.AddressChanged += OnAddressChanged;

            // JavaScript 메시지 수신 이벤트 등록
            _browser.JavascriptMessageReceived += OnJavascriptMessageReceived;

            // 로컬 모드일 때 요청을 사본으로 응답한다 (온라인 모드에서는 아무것도 하지 않는다).
            // 이 메서드 끝에서 첫 이동이 시작되므로 그 전에 붙여야 첫 요청부터 사본이 답한다
            _browser.ResourceRequestHandlerFactory = _archiveFactory;

            Logger.SimpleLog($"[WebBrowserViewModel] Browser attached (initialized: {_browser.IsBrowserInitialized})");

            if (!_browser.IsBrowserInitialized)
            {
                _browser.IsBrowserInitializedChanged += OnBrowserInitializedChanged;
            }

            // 브라우저 연결 전에 들어온 요청이 있으면 시작 주소보다 그 마지막 의도를 우선한다.
            if (_pendingNavigationUrl is { } pendingUrl)
            {
                Navigate(pendingUrl);
            }
            else
            {
                NavigateToStartupUrl();
            }
        }

        private void OnBrowserInitializedChanged(object? sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (sender is not ChromiumWebBrowser browser
                || !ReferenceEquals(_browser, browser)
                || e.NewValue is not true)
            {
                return;
            }

            // true가 된 뒤에는 Navigate()가 즉시 로드하므로 한 번 쓰는 준비 이벤트를 먼저 해제한다.
            // 계속 구독하면 이후 상태 변화가 이미 처리한 대기 URL을 다시 건드릴 여지만 남는다.
            browser.IsBrowserInitializedChanged -= OnBrowserInitializedChanged;

            if (_pendingNavigationUrl is { } pendingUrl)
            {
                Navigate(pendingUrl);
            }
        }

        private void NavigateToStartupUrl()
        {
            // Address는 about:blank의 AddressChanged와 FrameLoadEnd가 비동기로 덮어쓰는 관측값이다.
            // 시작 주소의 원천인 App.StartupUrl을 직접 읽어 Address 갱신 순서와 경합하지 않게 한다.
            Navigate(App.StartupUrl);
        }

        /// <summary>
        /// 주소 변경 이벤트 (WPF DependencyProperty 방식)
        /// </summary>
        private void OnAddressChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Address = e.NewValue?.ToString() ?? string.Empty;
            });
        }

        /// <summary>
        /// 페이지 로드 시작 이벤트
        ///
        /// 여기서는 상태 보고 스크립트만 넣는다. 로딩 중에 난 자원 실패와 스크립트 오류를 잡으려면
        /// 자원을 받기 전에 들어가 있어야 하므로, 다른 스크립트와 달리 로드가 끝나기를 기다리지 않는다
        /// </summary>
        private void OnFrameLoadStart(object? sender, FrameLoadStartEventArgs e)
        {
            if (!e.Frame.IsMain || _isDebugMode) return;

            try
            {
                e.Frame.ExecuteJavaScriptAsync(PageHealth.INIT_SCRIPT);
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[WebBrowserViewModel] Page health inject skipped: {ex.Message}");
            }
        }

        /// <summary>
        /// 페이지 로드 완료 이벤트
        /// </summary>
        private void OnFrameLoadEnd(object? sender, FrameLoadEndEventArgs e)
        {
            // 메인 프레임만 처리
            if (!e.Frame.IsMain)
                return;

            // 이벤트가 준 주소를 쓴다. CEF가 넘긴 값이므로 UI 스레드로 넘어간 뒤에도 확실하다.
            // IFrame은 이 핸들러가 끝나면 정리되므로 문자열만 들고 간다
            var loadedUrl = e.Url ?? string.Empty;

            // CEF 스레드에서 호출되므로 UI 스레드로 전환
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                IsLoading = false;

                // 브라우저의 Address DependencyProperty보다 이쪽이 먼저 도착할 수 있으므로
                // 뷰모델이 아는 주소를 여기서 맞춘다. 이후 판정은 모두 이 값으로 한다
                Address = loadedUrl;

                // 브라우저 초기화 전에 수신된 모니터 주사율을 반영 (이미 같은 값이면 CEF 내부에서 무시됨)
                ApplyWindowlessFrameRate();

                // 디버그 모드일 때는 모든 JavaScript 주입 스킵
                if (_isDebugMode)
                {
                    Logger.SimpleLog($"[WebBrowserViewModel] Debug mode - skipping all scripts: {loadedUrl}");
                    return;
                }

                // 준비되지 않았으면 아래 주입이 전부 빈 호출이 된다.
                // 그 상태를 조용히 넘기면 화면만 이상해지고 로그에는 아무 단서가 남지 않는다
                if (ReadyBrowser == null)
                {
                    Logger.SimpleLog($"[WebBrowserViewModel] Page setup skipped, browser not ready: {loadedUrl}");
                    return;
                }

                try
                {
                    // 불필요한 UI 요소 제거
                    await ExecuteScriptAsync(UICustomization.REMOVE_UNWANTED_ELEMENTS_SCRIPT);

                    // 웹 페이지 마진/패딩 제거
                    await ExecuteScriptAsync(PageLayout.REMOVE_PAGE_MARGINS_SCRIPT);

                    // 줌 레벨 적용
                    ApplyZoomLevel();

                    // Tarkov Market 전용 처리
                    if (loadedUrl.Contains("tarkov-market.com"))
                    {
                        // 게임 사건을 넘길 통로 등록 (페이지마다 다시 만들어야 한다)
                        await ExecuteScriptAsync(PilotBridge.INIT_SCRIPT);

                        // 방향 표시기 추가
                        await ExecuteScriptAsync(MapMarkers.ADD_DIRECTION_INDICATORS_SCRIPT);

                        // 맵을 열 때 창 크기에 맞추고, 끄는 동안 화면 가운데를 벗어나지 않게 한다
                        await ExecuteScriptAsync(MapKeepVisible.KEEP_MAP_VISIBLE_SCRIPT);

                        // UI 요소 숨김 설정 적용
                        await ApplyUIVisibilityAsync();

                        // 맵 페이지에서 Extraction 필터 적용 (맵 이동 직후이므로 DOM 대기 필요)
                        if (loadedUrl.Contains("/maps/"))
                        {
                            await ApplyExtractionFilterAsync(IsPmcExtraction, waitForDom: true);
                        }
                    }

                    Logger.SimpleLog($"[WebBrowserViewModel] Frame load completed: {loadedUrl}");
                }
                catch (Exception ex)
                {
                    Logger.Error("[WebBrowserViewModel] OnFrameLoadEnd error", ex);
                }
            });
        }

        /// <summary>
        /// JavaScript 메시지 수신 처리
        /// </summary>
        private void OnJavascriptMessageReceived(object? sender, JavascriptMessageReceivedEventArgs e)
        {
            try
            {
                // 디버깅: 모든 수신 메시지 로깅
                Logger.SimpleLog($"[WebBrowserViewModel] JavascriptMessageReceived triggered! Raw message type: {e.Message?.GetType().Name}");

                var message = e.Message?.ToString();
                Logger.SimpleLog($"[WebBrowserViewModel] Message content: {message}");

                if (string.IsNullOrEmpty(message))
                    return;

                // 맵 정보 파싱 (예: "map:customs_preset")
                if (message.StartsWith("map:"))
                {
                    var mapName = message.Substring(4);
                    Logger.SimpleLog($"[WebBrowserViewModel] Map received: {mapName}");

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        CurrentMap = mapName;
                        // Messenger로 MainWindowViewModel에 전달
                        WeakReferenceMessenger.Default.Send(new MapReceivedMessage(mapName));
                    });
                }
                // JSON 메시지 처리
                else if (message.StartsWith("{"))
                {
                    ProcessJsonMessage(message);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("[WebBrowserViewModel] OnJavascriptMessageReceived error", ex);
            }
        }

        /// <summary>
        /// JSON 메시지 처리
        /// </summary>
        private void ProcessJsonMessage(string message)
        {
            try
            {
                var json = System.Text.Json.JsonDocument.Parse(message);
                var messageType = json.RootElement.GetProperty("type").GetString();

                switch (messageType)
                {
                    case "margins-removed":
                    case "ui-elements-removed":
                        Logger.SimpleLog($"[WebBrowserViewModel] {messageType}");
                        // CefSharp은 자동으로 리사이즈를 처리하므로 별도 작업 불필요
                        break;

                    // 페이지가 낸 오류. 앱 안에서만 나는 렌더 실패의 단서가 여기에 남는다
                    case "page-error":
                        Logger.SimpleLog($"[PageHealth] {json.RootElement.GetProperty("kind").GetString()}: " +
                            $"{json.RootElement.GetProperty("detail").GetString()}");
                        break;

                    // 맵이 그려졌는지. 바닥 맵이 없으면 그 자체가 증상이므로 눈에 띄게 남긴다
                    case "page-health":
                        var baseMap = json.RootElement.GetProperty("baseMap").GetBoolean();
                        Logger.SimpleLog($"[PageHealth] {json.RootElement.GetProperty("path").GetString()} " +
                            $"baseMap={baseMap}, markerLayer={json.RootElement.GetProperty("markerLayer").GetBoolean()}" +
                            (baseMap ? string.Empty : " <- 바닥 맵이 그려지지 않았다"));
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("[WebBrowserViewModel] ProcessJsonMessage error", ex);
            }
        }

        #region Commands

        /// <summary>
        /// URL로 네비게이션
        /// </summary>
        [RelayCommand]
        public void Navigate(string url)
        {
            if (string.IsNullOrEmpty(url)) return;

            if (_browser?.IsBrowserInitialized != true)
            {
                // CEF 준비 전에 LoadUrl을 호출하면 느린 머신에서는 요청이 사라진다.
                // 중간 페이지를 차례로 열 필요는 없으므로 마지막 요청 하나만 보관하고,
                // IsBrowserInitializedChanged가 준비 완료를 알리면 이 메서드로 다시 보낸다.
                _pendingNavigationUrl = url;
                IsLoading = true;
                Logger.SimpleLog($"[WebBrowserViewModel] Navigation deferred until browser initialization: {url}");
                return;
            }

            // 대기 요청은 실행을 시작하는 순간 비운다. 아래 중복 주소 판정으로 로드를 생략해도
            // 이미 처리한 요청이 남아 나중에 다시 실행되는 일을 막는다.
            _pendingNavigationUrl = null;

            // 준비 전 Address는 마지막 관측값일 뿐이므로 위에서 요청을 먼저 보관한다.
            // 준비된 브라우저가 이미 그 주소에 있을 때만 다시 받지 않는다.
            // 시작할 때 맵 페이지로 바로 들어가면
            // pilot 연결 직후 같은 맵으로 이동 요청이 한 번 더 오는데, 그대로 두면 페이지를
            // 두 번 그려서 없애려던 덜컥임이 그대로 남는다.
            // 페이지를 다시 받아야 할 때는 Refresh()가 따로 있다
            if (string.Equals(Address, url, StringComparison.OrdinalIgnoreCase))
            {
                Logger.SimpleLog($"[WebBrowserViewModel] Already at {url}, skipping navigation");
                return;
            }

            IsLoading = true;
            _browser.LoadUrl(url);
            Logger.SimpleLog($"[WebBrowserViewModel] Navigating to: {url}");
        }

        /// <summary>
        /// 맵 정보로 네비게이션
        /// </summary>
        [RelayCommand]
        public void NavigateToMap(MapInfo? mapInfo)
        {
            if (mapInfo != null)
            {
                CurrentMap = mapInfo.MapId;
                Navigate(mapInfo.Url);
            }
        }

        /// <summary>
        /// 새로고침
        /// </summary>
        [RelayCommand]
        public void Refresh()
        {
            _browser?.Reload();
        }

        /// <summary>
        /// 개발자 도구 열기/닫기
        /// </summary>
        [RelayCommand]
        public void ToggleDevTools()
        {
            _browser?.ShowDevTools();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// UI 요소 숨김/표시 적용
        /// </summary>
        public async Task ApplyUIVisibilityAsync()
        {
            if (_browser == null)
                return;

            string mapId = CurrentMap ?? "default";
            await _browserUIService.ApplyUIVisibilityAsync(_browser, mapId, HideWebElements);
            Logger.SimpleLog($"[WebBrowserViewModel] Applied UI visibility: mapId={mapId}, hide={HideWebElements}");
        }

        /// <summary>
        /// JavaScript 스크립트 실행
        /// </summary>
        public async Task<JavascriptResponse?> ExecuteScriptAsync(string script)
        {
            var browser = ReadyBrowser;

            if (browser == null)
                return null;

            try
            {
                return await browser.EvaluateScriptAsync(script);
            }
            catch (Exception ex)
            {
                Logger.Error("[WebBrowserViewModel] ExecuteScriptAsync error", ex);
                return null;
            }
        }

        /// <summary>
        /// 줌 레벨 적용
        /// </summary>
        public void ApplyZoomLevel()
        {
            var browser = ReadyBrowser;

            if (browser == null)
                return;

            try
            {
                // CefSharp의 ZoomLevel은 로그 스케일 (0 = 100%)
                // 백분율을 로그 스케일로 변환
                double zoomFactor = ZoomLevel / 100.0;
                double zoomLevelLog = Math.Log(zoomFactor) / Math.Log(1.2);
                browser.ZoomLevel = zoomLevelLog;

                Logger.SimpleLog($"[WebBrowserViewModel] Zoom level set to {ZoomLevel}% (log: {zoomLevelLog:F2})");
            }
            catch (Exception ex)
            {
                Logger.Error("[WebBrowserViewModel] ApplyZoomLevel error", ex);
            }
        }

        #endregion

        #region Property Changed Handlers

        partial void OnHideWebElementsChanged(bool value)
        {
            _ = ApplyUIVisibilityAsync();
        }

        partial void OnZoomLevelChanged(int value)
        {
            ApplyZoomLevel();
        }

        #endregion

        #region Messenger Handlers

        /// <summary>
        /// 맵 선택 변경 메시지 핸들러 (MainWindowViewModel → WebBrowserViewModel)
        /// </summary>
        public void Receive(MapSelectionChangedMessage message)
        {
            if (message.Value != null)
            {
                // 맵 선택 시 디버그 모드 해제
                if (_isDebugMode)
                {
                    _isDebugMode = false;
                    Logger.SimpleLog("[WebBrowserViewModel] Debug mode disabled - Map selected");
                }

                CurrentMap = message.Value.MapId;
                NavigateToMap(message.Value);
                Logger.SimpleLog($"[WebBrowserViewModel] MapSelectionChanged via Messenger: {message.Value.MapId}");
            }
        }

        /// <summary>
        /// UI 요소 숨기기 설정 변경 메시지 핸들러 (MainWindowViewModel → WebBrowserViewModel)
        /// </summary>
        public void Receive(HideWebElementsChangedMessage message)
        {
            HideWebElements = message.Value;
            Logger.SimpleLog($"[WebBrowserViewModel] HideWebElementsChanged via Messenger: {message.Value}");
        }

        /// <summary>
        /// 줌 레벨 변경 메시지 핸들러 (MainWindowViewModel → WebBrowserViewModel)
        /// </summary>
        public void Receive(ZoomLevelChangedMessage message)
        {
            ZoomLevel = message.Value;
            Logger.SimpleLog($"[WebBrowserViewModel] ZoomLevelChanged via Messenger: {message.Value}");
        }

        /// <summary>
        /// Extraction 필터 변경 메시지 핸들러 (MainWindowViewModel → WebBrowserViewModel)
        /// </summary>
        public void Receive(ExtractionFilterChangedMessage message)
        {
            IsPmcExtraction = message.Value;
            _ = ApplyExtractionFilterAsync(message.Value);
            Logger.SimpleLog($"[WebBrowserViewModel] ExtractionFilterChanged via Messenger: {(message.Value ? "PMC" : "SCAV")}");
        }

        /// <summary>
        /// URL 이동 메시지 핸들러 (SettingsViewModel → WebBrowserViewModel)
        /// 디버그 모드 활성화 및 지정된 URL로 이동
        /// </summary>
        public void Receive(NavigateToUrlMessage message)
        {
            _isDebugMode = true;
            Navigate(message.Value);
            Logger.SimpleLog($"[WebBrowserViewModel] Debug mode enabled - Navigate to: {message.Value}");
        }

        /// <summary>
        /// 모니터 주사율 변경 메시지 핸들러 (MonitorRefreshRateBehavior → WebBrowserViewModel)
        /// OSR 페인트 상한을 창이 위치한 모니터의 주사율에 맞춘다
        /// </summary>
        public void Receive(MonitorRefreshRateChangedMessage message)
        {
            // 하한 30: 저주사율 모니터에서도 조작감 유지, 상한 240: 비정상 드라이버 값 방어
            _monitorRefreshRate = Math.Clamp(message.Value, 30, 240);
            ApplyWindowlessFrameRate();
        }

        /// <summary>
        /// 로컬 맵 전환 메시지 핸들러 (MainWindowViewModel → WebBrowserViewModel)
        ///
        /// 가로채기는 새 요청부터 걸리므로, 이미 그려진 페이지를 다시 읽어야 화면이 바뀐다.
        /// Navigate()는 같은 주소면 건너뛰므로 여기서는 Refresh()를 쓴다
        /// </summary>
        public void Receive(LocalMapModeChangedMessage message)
        {
            _archiveFactory.LocalModeEnabled = message.Value;
            Logger.SimpleLog($"[WebBrowserViewModel] Local map mode via Messenger: {message.Value}");

            Refresh();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 스크린샷 생성 이벤트 처리 (ScreenshotsWatcher -> MapEventService)
        /// </summary>
        private void OnScreenshotTaken(object? sender, ScreenshotTakenEventArgs e)
        {
            SendToPilot(PilotBridge.SendScreenshot(e.Filename), $"screenshot: {e.Filename}");
        }

        /// <summary>
        /// 퀘스트 완료 이벤트 처리 (LogsWatcher -> MapEventService)
        /// </summary>
        private void OnQuestCompleted(object? sender, QuestCompletedEventArgs e)
        {
            SendToPilot(PilotBridge.CompleteQuest(e.QuestId), $"quest complete: {e.QuestId}");
        }

        /// <summary>
        /// 게임 사건을 웹 페이지의 window.pilot으로 전달
        ///
        /// 파일 감시 스레드에서 불리므로 UI 스레드로 넘겨 실행한다.
        /// 전달 결과를 로그에 남기는 이유: 사이트가 브리지를 거두면 위치가 조용히 멈추는데,
        /// 그때 앱과 사이트 중 어느 쪽이 끊겼는지 로그만으로 가릴 수 있어야 한다
        /// </summary>
        /// <param name="script">PilotBridge가 만든 호출문</param>
        /// <param name="description">로그에 남길 사건 설명</param>
        private void SendToPilot(string script, string description)
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                // 다른 사이트를 보고 있으면 넘길 곳이 없다
                if (Address?.Contains("tarkov-market.com") != true)
                {
                    Logger.SimpleLog($"[PilotBridge] Skipped ({description}): not on tarkov-market.com");
                    return;
                }

                var response = await ExecuteScriptAsync(script);
                bool delivered = response?.Result as bool? ?? false;

                if (delivered)
                {
                    Logger.SimpleLog($"[PilotBridge] Sent ({description})");
                }
                else
                {
                    Logger.SimpleLog($"[PilotBridge] Not delivered ({description}): window.pilot unavailable");
                }
            });
        }

        /// <summary>
        /// CefSharp OSR 페인트 상한(WindowlessFrameRate)을 현재 모니터 주사율로 적용
        /// 브라우저 초기화 전이면 보류되고, 첫 FrameLoadEnd에서 재적용된다
        /// </summary>
        private void ApplyWindowlessFrameRate()
        {
            var browser = ReadyBrowser;

            if (browser == null)
                return;

            try
            {
                var browserHost = browser.GetBrowserHost();
                if (browserHost != null)
                {
                    browserHost.WindowlessFrameRate = _monitorRefreshRate;
                    Logger.SimpleLog($"[WebBrowserViewModel] WindowlessFrameRate applied: {_monitorRefreshRate}fps");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("[WebBrowserViewModel] ApplyWindowlessFrameRate error", ex);
            }
        }

        /// <summary>
        /// Extraction 필터 적용 (PMC/SCAV)
        /// </summary>
        /// <param name="isPmc">true = PMC, false = SCAV</param>
        /// <param name="waitForDom">true = 맵 이동 직후 DOM 대기 필요</param>
        private async Task ApplyExtractionFilterAsync(bool isPmc, bool waitForDom = false)
        {
            if (ReadyBrowser == null)
                return;

            // tarkov-market.com 맵 페이지에서만 동작
            if (Address?.Contains("tarkov-market.com/maps/") != true)
                return;

            try
            {
                // 먼저 초기화 스크립트 실행 (함수가 없을 수 있음)
                await ExecuteScriptAsync(WebElementsControl.INIT_SCRIPT);

                // 맵 이동 직후에만 DOM 렌더링 대기
                if (waitForDom)
                {
                    await Task.Delay(700);
                }

                var script = isPmc
                    ? WebElementsControl.CLICK_PMC_EXTRACTION
                    : WebElementsControl.CLICK_SCAV_EXTRACTION;

                await ExecuteScriptAsync(script);
                Logger.SimpleLog($"[WebBrowserViewModel] Applied extraction filter: {(isPmc ? "PMC" : "SCAV")}");
            }
            catch (Exception ex)
            {
                Logger.Error("[WebBrowserViewModel] ApplyExtractionFilterAsync error", ex);
            }
        }

        #endregion
    }
}
