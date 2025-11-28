using CefSharp;
using CefSharp.Wpf;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using TanukiTarkovMap.Messages;
using TanukiTarkovMap.Models.Data;
using TanukiTarkovMap.Models.JavaScript;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.ViewModels
{
    /// <summary>
    /// WebBrowser UserControl의 ViewModel
    /// CefSharp 브라우저 로직을 관리
    /// </summary>
    public partial class WebBrowserViewModel : ObservableObject,
        IRecipient<MapSelectionChangedMessage>,
        IRecipient<HideWebElementsChangedMessage>,
        IRecipient<ZoomLevelChangedMessage>
    {
        private readonly BrowserUIService _browserUIService;
        private ChromiumWebBrowser? _browser;

        #region Observable Properties

        /// <summary> 현재 URL </summary>
        [ObservableProperty]
        private string _address = App.WebsiteUrl;

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
        private int _zoomLevel = 67;

        #endregion

        public WebBrowserViewModel()
        {
            _browserUIService = ServiceLocator.BrowserUIService;

            // Messenger 등록 (MainWindowViewModel로부터 메시지 수신)
            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        /// <summary>
        /// ChromiumWebBrowser 인스턴스 설정 (View에서 호출)
        /// </summary>
        public void SetBrowser(ChromiumWebBrowser browser)
        {
            _browser = browser;

            // 이벤트 핸들러 등록
            _browser.FrameLoadEnd += OnFrameLoadEnd;
            _browser.AddressChanged += OnAddressChanged;

            // JavaScript 메시지 수신 이벤트 등록
            _browser.JavascriptMessageReceived += OnJavascriptMessageReceived;

            Logger.SimpleLog("[WebBrowserViewModel] Browser initialized");
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
        /// 페이지 로드 완료 이벤트
        /// </summary>
        private void OnFrameLoadEnd(object? sender, FrameLoadEndEventArgs e)
        {
            // 메인 프레임만 처리
            if (!e.Frame.IsMain)
                return;

            // CEF 스레드에서 호출되므로 UI 스레드로 전환
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                IsLoading = false;

                try
                {
                    // 불필요한 UI 요소 제거
                    await ExecuteScriptAsync(UICustomization.REMOVE_UNWANTED_ELEMENTS_SCRIPT);

                    // 웹 페이지 마진/패딩 제거
                    await ExecuteScriptAsync(PageLayout.REMOVE_PAGE_MARGINS_SCRIPT);

                    // 줌 레벨 적용
                    ApplyZoomLevel();

                    // Tarkov Market 전용 처리
                    if (_browser?.Address?.Contains("tarkov-market.com") == true)
                    {
                        // 방향 표시기 추가
                        await ExecuteScriptAsync(MapMarkers.ADD_DIRECTION_INDICATORS_SCRIPT);

                        // UI 요소 숨김 설정 적용
                        await ApplyUIVisibilityAsync();

                        // "/pilot" 페이지에서 Connected 상태 감지 시작
                        if (_browser.Address.Contains("/pilot"))
                        {
                            await ExecuteScriptAsync(ConnectionDetector.DETECT_CONNECTION_STATUS);
                            Logger.SimpleLog("[WebBrowserViewModel] Connection detection script injected");
                        }
                    }

                    Logger.SimpleLog($"[WebBrowserViewModel] Frame load completed: {e.Url}");
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
                    case "pilot-connected":
                        Logger.SimpleLog("[WebBrowserViewModel] Pilot connected detected!");
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            // Messenger로 MainWindowViewModel에 전달
                            WeakReferenceMessenger.Default.Send(new PilotConnectedMessage());
                        });
                        break;

                    case "margins-removed":
                    case "ui-elements-removed":
                        Logger.SimpleLog($"[WebBrowserViewModel] {messageType}");
                        // CefSharp은 자동으로 리사이즈를 처리하므로 별도 작업 불필요
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
            if (_browser != null && !string.IsNullOrEmpty(url))
            {
                IsLoading = true;
                _browser.LoadUrl(url);
                Logger.SimpleLog($"[WebBrowserViewModel] Navigating to: {url}");
            }
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
            if (_browser?.IsBrowserInitialized != true)
                return null;

            try
            {
                return await _browser.EvaluateScriptAsync(script);
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
            if (_browser?.IsBrowserInitialized != true)
                return;

            try
            {
                // CefSharp의 ZoomLevel은 로그 스케일 (0 = 100%)
                // 백분율을 로그 스케일로 변환
                double zoomFactor = ZoomLevel / 100.0;
                double zoomLevelLog = Math.Log(zoomFactor) / Math.Log(1.2);
                _browser.ZoomLevel = zoomLevelLog;

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

        #endregion
    }
}
