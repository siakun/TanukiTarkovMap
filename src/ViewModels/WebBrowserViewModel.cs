using CefSharp;
using CefSharp.Wpf;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows.Threading;
using TanukiTarkovMap.Localization;
using TanukiTarkovMap.Messages;
using TanukiTarkovMap.Core.Data;
using TanukiTarkovMap.Core.Site;
using TanukiTarkovMap.Core.Offline;
using TanukiTarkovMap.Models.Offline;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Core.Services;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.Core.Utils;

namespace TanukiTarkovMap.ViewModels
{
    public partial class WebBrowserViewModel : ObservableObject,
        IRecipient<MapSelectionChangedMessage>,
        IRecipient<HideWebElementsChangedMessage>,
        IRecipient<ZoomLevelChangedMessage>,
        IRecipient<ExtractionFilterChangedMessage>,
        IRecipient<NavigateToUrlMessage>,
        IRecipient<MonitorRefreshRateChangedMessage>,
        IRecipient<ViewModeChangedMessage>,
        IRecipient<LanguageChangedMessage>,
        IRecipient<TopBarHiddenChangedMessage>
    {
        private readonly MapEventService _mapEventService;
        private readonly RaidShareService _raidShare;
        private ChromiumWebBrowser? _browser;
        private string? _pendingNavigationUrl;
        private readonly DispatcherTimer _positionMaintenanceTimer = new(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        private int _documentVersion;
        private string? _requestedUrl;
        private readonly MapPositionSession _position = new();
        private string? _overlayReport;
        private int _overlayDispatch;
        private bool _maintainingOverlay;
        public MapOverlayViewModel Overlay { get; }

        public MapViewMode Mode { get; private set; }

        public event EventHandler? BrowserModeChanged;

        private bool _isDebugMode = false;

        private bool InjectionEnabled => !_isDebugMode && Mode == MapViewMode.Online;

        private bool _isTopBarHidden;

        private bool IsPageUiVisible => !HideWebElements || (Mode != MapViewMode.Local && !InjectionEnabled);

        public bool IsBelowTopBar => !_isTopBarHidden && IsPageUiVisible;

        private int _monitorRefreshRate = 60;

        private ChromiumWebBrowser? ReadyBrowser =>
            _browser?.BrowserCore is { IsDisposed: false } ? _browser : null;

        [ObservableProperty]
        private string _address = "about:blank";

        [ObservableProperty]
        private bool _isLoading = true;

        [ObservableProperty]
        private string? _currentMap;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsBelowTopBar))]
        private bool _hideWebElements = true;

        [ObservableProperty]
        private int _zoomLevel = AppSettings.DefaultBrowserZoomLevel;

        [ObservableProperty]
        private bool _isPmcExtraction = true;

        public WebBrowserViewModel()
        {
            Overlay = new MapOverlayViewModel(async script =>
            {
                if (!InjectionEnabled || IsLoading || LocalViewer.MapName(Address) != null) return false;
                return CreateSiteControl(requireLoaded: true) is { } site && (await site.ExecuteAsync(script)).IsTrue;
            });
            _positionMaintenanceTimer.Tick += async (_, _) => await MaintainPositionAsync();
            _mapEventService = ServiceLocator.MapEventService;
            Mode = App.GetSettings().EffectiveViewMode;

            _zoomLevel = App.GetSettings().EffectiveBrowserZoomLevel;
            Overlay.SetZoom(_zoomLevel);

            _mapEventService.ScreenshotTaken += OnScreenshotTaken;

            _raidShare = ServiceLocator.RaidShareService;
            _raidShare.Changed += (_, _) => System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => { ApplyTeammates(); ApplyPartyText(); });

            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        public void SetBrowser(ChromiumWebBrowser? browser)
        {
            if (ReferenceEquals(_browser, browser))
            {
                return;
            }

            if (_browser != null)
            {
                _browser.FrameLoadStart -= OnFrameLoadStart;
                _browser.FrameLoadEnd -= OnFrameLoadEnd;
                _browser.AddressChanged -= OnAddressChanged;
                _browser.JavascriptMessageReceived -= OnJavascriptMessageReceived;
                _browser.IsBrowserInitializedChanged -= OnBrowserInitializedChanged;
                _browser.Loaded -= OnBrowserLoaded;
                _browser.Unloaded -= OnBrowserUnloaded;
            }
            _positionMaintenanceTimer.Stop();
            Interlocked.Increment(ref _documentVersion);
            _pendingNavigationUrl ??= _requestedUrl ?? App.StartupUrl;
            _position.Replay(resetFailure: true);
            Address = "about:blank";
            IsLoading = true;
            UpdateOverlayContext();
            _browser = browser;
            if (_browser == null) return;
            _browser.Loaded += OnBrowserLoaded;
            _browser.Unloaded += OnBrowserUnloaded;
            if (_browser.IsLoaded) _positionMaintenanceTimer.Start();

            _browser.FrameLoadStart += OnFrameLoadStart;
            _browser.FrameLoadEnd += OnFrameLoadEnd;
            _browser.AddressChanged += OnAddressChanged;

            _browser.JavascriptMessageReceived += OnJavascriptMessageReceived;

            Logger.SimpleLog($"[WebBrowserViewModel] Browser attached (initialized: {_browser.IsBrowserInitialized}, mode: {Mode})");

            if (!_browser.IsBrowserInitialized)
            {
                _browser.IsBrowserInitializedChanged += OnBrowserInitializedChanged;
            }

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

            browser.IsBrowserInitializedChanged -= OnBrowserInitializedChanged;

            if (_pendingNavigationUrl is { } pendingUrl)
            {
                Navigate(pendingUrl);
            }
        }

        private void NavigateToStartupUrl()
        {
            Navigate(App.StartupUrl);
        }

        private void OnAddressChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (!ReferenceEquals(sender, _browser)) return;
                Address = e.NewValue?.ToString() ?? string.Empty;
                if (!IsLoading && !IsBootstrapPage(Address)) AcceptPageAddress(Address);
                UpdateOverlayContext();
            });
        }

        private void OnFrameLoadStart(object? sender, FrameLoadStartEventArgs e)
        {
            if (!ReferenceEquals(sender, _browser) || !e.Frame.IsMain || IsBootstrapPage(e.Url)) return;
            var version = Interlocked.Increment(ref _documentVersion);
            var loadingUrl = e.Url ?? string.Empty;
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (!ReferenceEquals(sender, _browser) || version != _documentVersion) return;
                IsLoading = true;
                AcceptPageAddress(loadingUrl);
                _position.Replay();
                UpdateOverlayContext();
            });
            if (!InjectionEnabled || LocalViewer.MapName(loadingUrl) != null) return;

            try
            {
                foreach (var script in SiteControl.DocumentStart())
                {
                    if (!ReferenceEquals(sender, _browser) || !InjectionEnabled || version != _documentVersion) return;
                    e.Frame.ExecuteJavaScriptAsync(script.Source);
                }
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[WebBrowserViewModel] Load-start inject skipped: {ex.Message}");
            }
        }

        private void OnFrameLoadEnd(object? sender, FrameLoadEndEventArgs e)
        {
            if (!ReferenceEquals(sender, _browser) || !e.Frame.IsMain || IsBootstrapPage(e.Url))
                return;

            var loadedUrl = e.Url ?? string.Empty;
            var version = _documentVersion;

            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (!ReferenceEquals(sender, _browser) || version != _documentVersion) return;
                IsLoading = false;
                AcceptPageAddress(loadedUrl);

                Address = loadedUrl;
                UpdateOverlayContext();

                ApplyWindowlessFrameRate();

                if (_isDebugMode)
                {
                    Logger.SimpleLog($"[WebBrowserViewModel] Debug mode - skipping all scripts: {loadedUrl}");
                    return;
                }

                if (ReadyBrowser == null)
                {
                    Logger.SimpleLog($"[WebBrowserViewModel] Page setup skipped, browser not ready: {loadedUrl}");
                    return;
                }

                try
                {
                    if (LocalViewer.MapName(loadedUrl) != null)
                    {
                        ApplyZoomLevel();
                        await ApplyUIVisibilityAsync();
                        if (!ReferenceEquals(sender, _browser) || version != _documentVersion) return;
                        await ApplyExtractionFilterAsync(IsPmcExtraction);
                        if (!ReferenceEquals(sender, _browser) || version != _documentVersion) return;
                        await ExecuteScriptAsync(LocalViewer.SetLanguage(AppLanguage.Current), documentVersion: version);
                        if (!ReferenceEquals(sender, _browser) || version != _documentVersion) return;
                        await MaintainPositionAsync();
                        if (!ReferenceEquals(sender, _browser) || version != _documentVersion) return;
                        ApplyTeammates();
                        Logger.SimpleLog($"[WebBrowserViewModel] Local viewer loaded: {loadedUrl}");
                        return;
                    }

                    if (!InjectionEnabled)
                    {
                        ApplyZoomLevel();
                        Logger.SimpleLog($"[WebBrowserViewModel] Untouched site loaded ({Mode}), zoom only: {loadedUrl}");
                        return;
                    }

                    var site = CreateSiteControl();
                    if (site == null || !await site.InitializeAsync(loadedUrl, version, MapKey(loadedUrl) != null,
                        () => HideWebElements, () => IsPmcExtraction, ApplyZoomLevel,
                        () => { if (_position.Latest is { } latest) Overlay.SetScreenshot(latest.Filename); },
                        LogFactionResult))
                    {
                        Logger.SimpleLog($"[WebBrowserViewModel] Page setup stopped (page changed or script failed): {loadedUrl}");
                        return;
                    }

                    Logger.SimpleLog($"[WebBrowserViewModel] Frame load completed: {loadedUrl}");
                }
                catch (Exception ex)
                {
                    Logger.Error("[WebBrowserViewModel] OnFrameLoadEnd error", ex);
                }
            });
        }

        private void OnJavascriptMessageReceived(object? sender, JavascriptMessageReceivedEventArgs e)
        {
            if (!ReferenceEquals(sender, _browser)) return;
            try
            {
                if (e.Message is string report && SiteControl.IsOverlayReport(report))
                {
                    Interlocked.Exchange(ref _overlayReport, report);
                    if (Interlocked.CompareExchange(ref _overlayDispatch, 1, 0) == 0)
                        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            Interlocked.Exchange(ref _overlayDispatch, 0);
                            var latest = Interlocked.Exchange(ref _overlayReport, null);
                            if (InjectionEnabled && latest != null) Overlay.Accept(latest);
                        }, DispatcherPriority.Render);
                    return;
                }
                Logger.SimpleLog($"[WebBrowserViewModel] JavascriptMessageReceived triggered! Raw message type: {e.Message?.GetType().Name}");

                var message = e.Message?.ToString();
                Logger.SimpleLog($"[WebBrowserViewModel] Message content: {message}");

                if (string.IsNullOrEmpty(message))
                    return;

                if (message.StartsWith("{"))
                {
                    ProcessJsonMessage(message);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("[WebBrowserViewModel] OnJavascriptMessageReceived error", ex);
            }
        }

        private void ProcessJsonMessage(string message)
        {
            try
            {
                if (SiteControl.Diagnostic(message) is { } diagnostic) Logger.SimpleLog(diagnostic);
            }
            catch (Exception ex)
            {
                Logger.Error("[WebBrowserViewModel] ProcessJsonMessage error", ex);
            }
        }

        [RelayCommand]
        public void Navigate(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            url = AddressForMode(url);
            _requestedUrl = url;
            _position.Navigate(MapKey(url));
            UpdateOverlayContext();

            if (_browser?.IsBrowserInitialized != true)
            {
                _pendingNavigationUrl = url;
                IsLoading = true;
                Logger.SimpleLog($"[WebBrowserViewModel] Navigation deferred until browser initialization: {url}");
                return;
            }

            _pendingNavigationUrl = null;

            if (string.Equals(Address, url, StringComparison.OrdinalIgnoreCase))
            {
                Logger.SimpleLog($"[WebBrowserViewModel] Already at {url}, skipping navigation");
                return;
            }

            IsLoading = true;
            UpdateOverlayContext();
            _browser.LoadUrl(url);
            Logger.SimpleLog($"[WebBrowserViewModel] Navigating to: {url}");
        }

        [RelayCommand]
        public void NavigateToMap(MapInfo? mapInfo)
        {
            if (mapInfo != null)
            {
                CurrentMap = mapInfo.MapId;
                Navigate(mapInfo.Url);
            }
        }

        [RelayCommand]
        public void Refresh()
        {
            _browser?.Reload();
        }

        [RelayCommand]
        public void ToggleDevTools()
        {
            _browser?.ShowDevTools();
        }

        public async Task ApplyUIVisibilityAsync()
        {
            if (_browser == null)
                return;

            if (LocalViewer.MapName(Address) != null)
            {
                await ExecuteScriptAsync(LocalViewer.SetControlsVisible(!HideWebElements));
                return;
            }

            if (!InjectionEnabled)
                return;

            try
            {
                var site = CreateSiteControl();
                if (site == null) return;
                var mapId = CurrentMap ?? "default";
                Logger.SimpleLog($"[BrowserUIService] ApplyUIVisibilityAsync called for map ID: {mapId}, hideElements: {HideWebElements}");
                if (await site.SetUiVisibilityAsync(HideWebElements))
                    Logger.SimpleLog($"[WebBrowserViewModel] Applied UI visibility: mapId={mapId}, hide={HideWebElements}");
            }
            catch (Exception ex) { Logger.Error("[BrowserUIService] ApplyUIVisibilityAsync error", ex); }
        }

        private SiteControl? CreateSiteControl(bool requireLoaded = false)
        {
            var browser = ReadyBrowser;
            var version = _documentVersion;
            var map = requireLoaded ? MapKey(Address) : null;
            bool Current() => ReferenceEquals(browser, _browser) && version == _documentVersion
                && browser?.BrowserCore is { IsDisposed: false } && InjectionEnabled
                && (!requireLoaded || !IsLoading && map == MapKey(Address)) && LocalViewer.MapName(Address) == null;
            if (browser == null || !Current()) return null;
            return new SiteControl(async script =>
            {
                try
                {
                    var response = script.AwaitPromise
                        ? await browser.EvaluateScriptAsPromiseAsync("return " + script.Source, timeout: TimeSpan.FromSeconds(3))
                        : await browser.EvaluateScriptAsync(script.Source, timeout: TimeSpan.FromSeconds(3));
                    if (!response.Success) Logger.SimpleLog($"[WebBrowserViewModel] Site script failed: {response.Message}");
                    return new SiteResult(response.Success, response.Result, response.Message);
                }
                catch (Exception ex)
                {
                    Logger.Error("[WebBrowserViewModel] ExecuteScriptAsync error", ex);
                    return new SiteResult(false, Error: ex.Message);
                }
            }, Current, Logger.SimpleLog);
        }

        private async Task<JavascriptResponse?> ExecuteScriptAsync(string script, int? documentVersion = null)
        {
            var browser = ReadyBrowser;
            var version = documentVersion ?? _documentVersion;

            if (browser == null || version != _documentVersion)
                return null;
            if (LocalViewer.MapName(Address) == null && !InjectionEnabled) return null;

            try
            {
                var response = await browser.EvaluateScriptAsync(script, timeout: TimeSpan.FromSeconds(3));
                return ReferenceEquals(browser, _browser) && version == _documentVersion ? response : null;
            }
            catch (Exception ex)
            {
                Logger.Error("[WebBrowserViewModel] ExecuteScriptAsync error", ex);
                return null;
            }
        }

        public void ApplyZoomLevel()
        {
            var browser = ReadyBrowser;

            if (browser == null)
                return;

            try
            {
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

        partial void OnHideWebElementsChanged(bool value)
        {
            _ = ApplyUIVisibilityAsync();
        }

        partial void OnZoomLevelChanged(int value)
        {
            ApplyZoomLevel();
            Overlay.SetZoom(value);
        }

        public void Receive(MapSelectionChangedMessage message)
        {
            if (message.Value != null)
            {
                if (_isDebugMode)
                {
                    _isDebugMode = false;
                    OnPropertyChanged(nameof(IsBelowTopBar));
                    Logger.SimpleLog("[WebBrowserViewModel] Debug mode disabled - Map selected");
                }

                CurrentMap = message.Value.MapId;
                NavigateToMap(message.Value);
                Logger.SimpleLog($"[WebBrowserViewModel] MapSelectionChanged via Messenger: {message.Value.MapId}");
            }
        }

        public void Receive(HideWebElementsChangedMessage message)
        {
            HideWebElements = message.Value;
            Logger.SimpleLog($"[WebBrowserViewModel] HideWebElementsChanged via Messenger: {message.Value}");
        }

        public void Receive(ZoomLevelChangedMessage message)
        {
            ZoomLevel = message.Value;
            Logger.SimpleLog($"[WebBrowserViewModel] ZoomLevelChanged via Messenger: {message.Value}");
        }

        public void Receive(ExtractionFilterChangedMessage message)
        {
            IsPmcExtraction = message.Value;
            _ = ApplyExtractionFilterAsync(message.Value);
            Logger.SimpleLog($"[WebBrowserViewModel] ExtractionFilterChanged via Messenger: {(message.Value ? "PMC" : "SCAV")}");
        }

        public void Receive(NavigateToUrlMessage message)
        {
            _isDebugMode = true;
            OnPropertyChanged(nameof(IsBelowTopBar));
            UpdateOverlayContext();
            Navigate(message.Value);
            Logger.SimpleLog($"[WebBrowserViewModel] Debug mode enabled - Navigate to: {message.Value}");
        }

        public void Receive(MonitorRefreshRateChangedMessage message)
        {
            _monitorRefreshRate = Math.Clamp(message.Value, 30, 240);
            ApplyWindowlessFrameRate();
        }

        public void Receive(ViewModeChangedMessage message)
        {
            var previousMode = Mode;
            if (previousMode == message.Value) return;
            Mode = message.Value;
            _isDebugMode = false;
            OnPropertyChanged(nameof(IsBelowTopBar));
            UpdateOverlayContext();
            Logger.SimpleLog($"[WebBrowserViewModel] View mode via Messenger: {previousMode} -> {Mode}");

            if (MapNavigation.RequiresNewBrowser(previousMode, Mode))
            {
                if (_requestedUrl != null) _requestedUrl = AddressForModeSwitch(_requestedUrl);
                if (_pendingNavigationUrl != null) _pendingNavigationUrl = AddressForModeSwitch(_pendingNavigationUrl);
                BrowserModeChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            ReopenCurrentPage();
        }

        public void Receive(LanguageChangedMessage message)
        {
            if (LocalViewer.MapName(Address) == null) return;
            _ = ExecuteScriptAsync(LocalViewer.SetLanguage(message.Value));
        }

        public void Receive(TopBarHiddenChangedMessage message)
        {
            if (_isTopBarHidden == message.Value) return;
            _isTopBarHidden = message.Value;
            OnPropertyChanged(nameof(IsBelowTopBar));
        }

        private void OnScreenshotTaken(object? sender, ScreenshotTakenEventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                var target = MapKey(_requestedUrl ?? _pendingNavigationUrl ?? Address);
                if (_isDebugMode || target == null) return;
                _position.Accept(e.Filename, target);
                UpdateOverlayContext();
                if (InjectionEnabled && target == MapKey(Address)) Overlay.SetScreenshot(e.Filename);
                await MaintainPositionAsync();
            });
        }

        [ObservableProperty] public partial string PartyText { get; set; } = string.Empty;

        private void ApplyPartyText()
        {
            try { PartyText = _raidShare.PartyText; }
            catch (Exception ex) { PartyText = string.Empty; Logger.SimpleLog($"[RaidShare] Party text unavailable: {ex.Message}"); }
        }

        private void ApplyTeammates()
        {
            var teammates = _raidShare.Teammates;
            Overlay.SetTeammates(teammates);
            if (LocalViewer.MapName(Address) == null || MapKey(Address) is not { } map) return;
            _ = ExecuteScriptAsync(LocalViewer.ShowTeammates(teammates.Where(teammate => teammate.Position.Map == map)));
        }

        private void ReopenCurrentPage()
        {
            if (_browser?.IsBrowserInitialized != true || _pendingNavigationUrl != null || _requestedUrl is not { } url)
                return;

            var requestedPageCommitted = string.Equals(Address, url, StringComparison.OrdinalIgnoreCase);
            Interlocked.Increment(ref _documentVersion);
            IsLoading = true;
            UpdateOverlayContext();
            if (requestedPageCommitted) _browser.Reload();
            else _browser.LoadUrl(url);
            Logger.SimpleLog($"[WebBrowserViewModel] Reopening for {Mode} ({(requestedPageCommitted ? "reload" : "restart load")}): {url}");
        }

        private void OnBrowserLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (ReferenceEquals(sender, _browser)) _positionMaintenanceTimer.Start();
        }

        private void OnBrowserUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (ReferenceEquals(sender, _browser)) _positionMaintenanceTimer.Stop();
        }

        private bool IsBootstrapPage(string? url) =>
            string.Equals(url, "about:blank", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(_requestedUrl, "about:blank", StringComparison.OrdinalIgnoreCase);

        private static string? MapKey(string? address) => MapNavigation.MapKey(address);

        private string AddressForMode(string url) => MapNavigation.AddressForMode(url, Mode, AppLanguage.Current);

        private string AddressForModeSwitch(string url) =>
            MapNavigation.AddressForModeSwitch(url, Mode, AppLanguage.Current, App.StartupUrl);

        private void AcceptPageAddress(string url)
        {
            _requestedUrl = url;
            _position.Navigate(MapKey(url));
            UpdateOverlayContext();
        }

        private void UpdateOverlayContext()
        {
            var url = _requestedUrl ?? Address;
            Overlay.SetContext(InjectionEnabled && !IsLoading && LocalViewer.MapName(url) == null, MapKey(url), _documentVersion);
            if (_position.Latest is { } latest && latest.Map == MapKey(url)) Overlay.SetScreenshot(latest.Filename);
        }

        private async Task MaintainPositionAsync()
        {
            if (InjectionEnabled && !IsLoading && ReadyBrowser != null && LocalViewer.MapName(Address) == null
                && MapKey(Address) != null)
            {
                if (_maintainingOverlay) return;
                _maintainingOverlay = true;
                try
                {
                    if (CreateSiteControl(requireLoaded: true) is { } site) await Overlay.MaintainAsync(site);
                }
                finally { _maintainingOverlay = false; }
                return;
            }
            if (_isDebugMode || IsLoading || ReadyBrowser == null
                || LocalViewer.MapName(Address) == null || MapKey(Address) is not { } map)
                return;
            var version = _documentVersion;
            await _position.MaintainLocalAsync(map, async source =>
            {
                var response = await ExecuteScriptAsync(source, documentVersion: version);
                return new SiteResult(response?.Success == true, response?.Result, response?.Message);
            }, () => version == _documentVersion && !IsLoading, Logger.SimpleLog);
        }

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

        private async Task ApplyExtractionFilterAsync(bool isPmc)
        {
            var version = _documentVersion;
            if (ReadyBrowser == null) return;
            if (LocalViewer.MapName(Address) != null)
            {
                await ExecuteScriptAsync(LocalViewer.SetFaction(isPmc), documentVersion: version);
                return;
            }
            if (!SiteControl.CanSetFaction(Address) || CreateSiteControl() is not { } site) return;
            try
            {
                var result = await site.SetFactionAsync(isPmc, () => IsPmcExtraction == isPmc);
                LogFactionResult(isPmc, result);
            }
            catch (Exception ex) { Logger.Error("[WebBrowserViewModel] ApplyExtractionFilterAsync error", ex); }
        }

        private static void LogFactionResult(bool isPmc, FactionResult result)
        {
            if (result.Cancelled) return;
            var faction = isPmc ? "PMC" : "SCAV";
            Logger.SimpleLog(result.Applied
                ? $"[WebBrowserViewModel] Applied extraction filter: {faction}"
                    + (result.Attempt > 1 ? $" (attempt {result.Attempt})" : "")
                : $"[WebBrowserViewModel] Extraction filter not applied: {faction} ({result.Reason}, attempt {result.Attempt})");
        }

    }
}
