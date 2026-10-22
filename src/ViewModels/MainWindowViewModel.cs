using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using TanukiTarkovMap.Messages;
using TanukiTarkovMap.Core.Data;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Core.Services;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.Core.Utils;

namespace TanukiTarkovMap.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject,
        IRecipient<TopBarHiddenChangedMessage>,
        IRecipient<OpacitySliderDragMessage>,
        IRecipient<UpdateReadyMessage>,
        IRecipient<UpdateIconPreviewMessage>
    {
        private readonly WindowStateManager _windowStateManager;
        private readonly MapEventService _mapEventService;
        private readonly GoonTrackerService _goonTrackerService;
        private AppSettings _settings;
        private readonly GameWindowTracker _gameWindowTracker;
        [ObservableProperty] public partial bool IsRaidFocusActive { get; set; }
        [ObservableProperty] public partial RaidFocusHint RaidFocusHint { get; set; }
        [ObservableProperty] public partial bool IsRaidFocusHintVisible { get; set; }
        internal bool IsRaidFocusMove { get; set; }
        internal Point? RaidFocusHomePosition { get; set; }
        internal event EventHandler? WindowBoundsChangedByUser;

        private void OnInRaidChanged(object? sender, bool inRaid)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted) return;
            dispatcher.BeginInvoke(() =>
            {
                if (_mapEventService.IsInRaid && !_gameWindowTracker.State.IsRunning) _gameWindowTracker.RefreshGame();
                UpdateRaidFocusState();
            });
        }

        private void OnGameWindowChanged(object? sender, EventArgs e) => UpdateRaidFocusState();
        private void UpdateRaidFocusState() => IsRaidFocusActive =
            App.GetSettings().RaidFocusModeEnabled && _mapEventService.IsInRaid && _gameWindowTracker.State.CanTrack && _gameWindowTracker.State.IsRunning;

        internal void StopRaidFocusTracking()
        {
            _mapEventService.InRaidChanged -= OnInRaidChanged;
            _gameWindowTracker.StateChanged -= OnGameWindowChanged;
            IsRaidFocusActive = false;
        }


        [ObservableProperty] public partial double CurrentWindowWidth { get; set; }
        [ObservableProperty] public partial double CurrentWindowHeight { get; set; }
        [ObservableProperty] public partial double CurrentWindowLeft { get; set; }
        [ObservableProperty] public partial double CurrentWindowTop { get; set; }
        [ObservableProperty] public partial ResizeMode ResizeMode { get; set; } = ResizeMode.CanResize;
        [ObservableProperty] public partial bool IsTopmost { get; set; }
        [ObservableProperty] public partial double MinWidth { get; set; } = 300;
        [ObservableProperty] public partial double MinHeight { get; set; } = 200;

        private bool _isAlwaysOnTop;
        public bool IsAlwaysOnTop
        {
            get => _isAlwaysOnTop;
            set
            {
                if (SetProperty(ref _isAlwaysOnTop, value))
                {
                    IsTopmost = value;

                    var settings = App.GetSettings();
                    settings.IsAlwaysOnTop = value;
                    App.SetSettings(settings);
                    Settings.Save();

                    Logger.SimpleLog($"[IsAlwaysOnTop] Changed to: {value}, IsTopmost updated to: {IsTopmost}");
                }
            }
        }

        [ObservableProperty] public partial Visibility TopBarVisibility { get; set; } = Visibility.Visible;

        private double _windowOpacity = 1.0;
        public double WindowOpacity
        {
            get => _windowOpacity;
            set
            {
                var clampedValue = Math.Max(0.1, Math.Min(1.0, value));
                if (SetProperty(ref _windowOpacity, clampedValue))
                {
                    var settings = App.GetSettings();
                    settings.WindowOpacity = clampedValue;
                    App.SetSettings(settings);
                    Settings.Save();

                    OnPropertyChanged(nameof(ActualWindowOpacity));
                }
            }
        }

        private bool _isTopBarHidden = false;
        public bool IsTopBarHidden
        {
            get => _isTopBarHidden;
            set
            {
                if (SetProperty(ref _isTopBarHidden, value))
                {
                    OnPropertyChanged(nameof(ActualWindowOpacity));
                }
            }
        }

        private bool _isOpacitySliderDragging = false;
        public bool IsOpacitySliderDragging
        {
            get => _isOpacitySliderDragging;
            set
            {
                if (SetProperty(ref _isOpacitySliderDragging, value))
                {
                    OnPropertyChanged(nameof(ActualWindowOpacity));
                }
            }
        }

        public double ActualWindowOpacity => IsOpacitySliderDragging || IsTopBarHidden ? WindowOpacity : 1.0;

        [ObservableProperty] public partial bool IsSettingsOpen { get; set; } = false;

        public Visibility SettingsOverlayVisibility => IsSettingsOpen ? Visibility.Visible : Visibility.Collapsed;

        public Visibility BrowserContainerVisibility => Visibility.Visible;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowUpdateIcon))]
        public partial bool IsUpdateReady { get; set; } = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowUpdateIcon))]
        public partial bool IsUpdateIconForced { get; set; } = false;

        public bool ShowUpdateIcon => IsUpdateReady || IsUpdateIconForced;

        [ObservableProperty] public partial bool HotkeyEnabled { get; set; } = true;
        [ObservableProperty] public partial string HotkeyKey { get; set; } = AppSettings.DefaultHotkeyKey;
        [ObservableProperty] public partial bool HideWebElements { get; set; } = true;

        [ObservableProperty] public partial bool IsPmcExtraction { get; set; } = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsOnlineMode), nameof(IsLocalMode), nameof(IsOriginMode), nameof(AreMapControlsEnabled))]
        public partial MapViewMode ViewMode { get; set; } = MapViewMode.Online;

        public bool IsOnlineMode => ViewMode == MapViewMode.Online;
        public bool IsLocalMode => ViewMode == MapViewMode.Local;
        public bool IsOriginMode => ViewMode == MapViewMode.Origin;

        public bool AreMapControlsEnabled => ViewMode != MapViewMode.Origin;

        [ObservableProperty] public partial bool GoonsOnCurrentMap { get; set; } = false;

        public List<MapInfo> AvailableMaps => App.AvailableMaps;

        [ObservableProperty] public partial MapInfo SelectedMapInfo { get; set; }

        public List<int> AvailableZoomLevels { get; } = new List<int> { 50, 67, 75, 80, 90, 100, 110, 125, 150, 175, 200 };

        private int _selectedZoomLevel = AppSettings.DefaultBrowserZoomLevel;
        public int SelectedZoomLevel
        {
            get => _selectedZoomLevel;
            set
            {
                if (SetProperty(ref _selectedZoomLevel, value))
                {
                    var settings = App.GetSettings();
                    settings.BrowserZoomLevel = value;
                    App.SetSettings(settings);
                    Settings.Save();

                    Logger.SimpleLog($"[SelectedZoomLevel] Changed to: {value}%");
                }
            }
        }

        public Rect CurrentWindowBounds => new Rect(CurrentWindowLeft, CurrentWindowTop, CurrentWindowWidth, CurrentWindowHeight);

        public Rect NormalModeBounds
        {
            get
            {
                var bounds = _windowStateManager.NormalBounds;
                return new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            }
        }

        public MainWindowViewModel(
            WindowStateManager windowStateManager,
            MapEventService mapEventService,
            GoonTrackerService goonTrackerService,
            GameWindowTracker gameWindowTracker)
        {
            _windowStateManager = windowStateManager;
            _mapEventService = mapEventService;
            _goonTrackerService = goonTrackerService;
            _gameWindowTracker = gameWindowTracker;

            LoadSettings();
            InitializeCommands();
            NotifyInitialMapSelection();
            SubscribeToMapEvents();
            SubscribeToGoonTrackerEvents();
            _mapEventService.InRaidChanged += OnInRaidChanged;
            _gameWindowTracker.StateChanged += OnGameWindowChanged;
            UpdateRaidFocusState();

            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        private void SubscribeToMapEvents()
        {
            Logger.SimpleLog("[MainWindowViewModel] Subscribing to MapEventService events");

            _mapEventService.MapChanged += OnMapEventReceived;

            Logger.SimpleLog("[MainWindowViewModel] Successfully subscribed to MapEventService events");
        }

        private void SubscribeToGoonTrackerEvents()
        {
            _goonTrackerService.GoonsMapChanged += OnGoonsMapChanged;
            UpdateGoonsOnCurrentMap();
        }

        private void OnGoonsMapChanged(object? sender, string? goonsMap)
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;

            app.Dispatcher.Invoke(() =>
            {
                UpdateGoonsOnCurrentMap();
            });
        }

        private void UpdateGoonsOnCurrentMap()
        {
            GoonsOnCurrentMap = _goonTrackerService.IsGoonsOnMap(SelectedMapInfo?.Name);
            Logger.SimpleLog($"[MainWindowViewModel] GoonsOnCurrentMap updated: {GoonsOnCurrentMap} (Map: {SelectedMapInfo?.Name}, Goons: {_goonTrackerService.CurrentGoonsMap})");
        }

        private void OnMapEventReceived(object sender, MapChangedEventArgs e)
        {
            if (!IsMapChangeAllowed(e.Source))
            {
                return;
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (SelectedMapInfo?.Name == e.Map.Name)
                {
                    return;
                }

                Logger.SimpleLog($"[MainWindowViewModel] Auto map switch: {e.Map.DisplayName}");

                SelectedMapInfo = e.Map;
            });
        }

        private static bool IsMapChangeAllowed(MapChangeSource source) =>
            MapNavigation.AllowsAutomaticChange(App.GetSettings(), source);

        public void LoadSettings()
        {
            _settings = App.GetSettings();

            _windowStateManager.LoadFromSettings(_settings);

            HotkeyEnabled = _settings.HotkeyEnabled;
            HotkeyKey = _settings.HotkeyKey;

            _isAlwaysOnTop = _settings.IsAlwaysOnTop;
            IsTopmost = _settings.IsAlwaysOnTop;
            OnPropertyChanged(nameof(IsAlwaysOnTop));

            _selectedZoomLevel = _settings.EffectiveBrowserZoomLevel;
            OnPropertyChanged(nameof(SelectedZoomLevel));

            _windowOpacity = _settings.WindowOpacity > 0 ? _settings.WindowOpacity : 1.0;
            OnPropertyChanged(nameof(WindowOpacity));

            ViewMode = _settings.EffectiveViewMode;
            Logger.SimpleLog($"[LoadSettings] View mode: {ViewMode} (saved: '{_settings.ViewMode}')");

            var savedMap = MapNavigation.InitialMap(_settings);
            if (savedMap != null)
            {
                SelectedMapInfo = savedMap;
            }

            UpdateRaidFocusState();
            if (RaidFocusHomePosition.HasValue) return;

            var normalRect = _windowStateManager.NormalBounds;
            CurrentWindowWidth = normalRect.Width;
            CurrentWindowHeight = normalRect.Height;

            Logger.SimpleLog($"[LoadSettings] Loading window size: {normalRect.Width}x{normalRect.Height}");
            Logger.SimpleLog($"[LoadSettings] CurrentWindowWidth={CurrentWindowWidth}, CurrentWindowHeight={CurrentWindowHeight}");

            var positionSaved = !(normalRect.Left == -1 && normalRect.Top == -1);
            if (positionSaved && MonitorLayout.IsOnAnyMonitor(new Point(normalRect.Left, normalRect.Top)))
            {
                CurrentWindowLeft = normalRect.Left;
                CurrentWindowTop = normalRect.Top;
                Logger.SimpleLog($"[LoadSettings] Restored window position: ({normalRect.Left}, {normalRect.Top})");
            }
            else
            {
                CurrentWindowLeft = (SystemParameters.PrimaryScreenWidth - normalRect.Width) / 2;
                CurrentWindowTop = (SystemParameters.PrimaryScreenHeight - normalRect.Height) / 2;
                var reason = positionSaved ? $"saved ({normalRect.Left}, {normalRect.Top}) is not on a connected monitor" : "no saved position";
                Logger.SimpleLog($"[LoadSettings] Using center screen position: ({CurrentWindowLeft}, {CurrentWindowTop}), {reason}");
            }
        }

        private void InitializeCommands()
        {
            PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(SelectedMapInfo):
                        OnSelectedMapInfoChanged();
                        NotifyMapSelectionChanged();
                        break;
                    case nameof(HideWebElements):
                        OnHideWebElementsChanged();
                        WeakReferenceMessenger.Default.Send(new HideWebElementsChangedMessage(HideWebElements));
                        break;
                    case nameof(SelectedZoomLevel):
                        WeakReferenceMessenger.Default.Send(new ZoomLevelChangedMessage(SelectedZoomLevel));
                        break;
                    case nameof(IsPmcExtraction):
                        WeakReferenceMessenger.Default.Send(new ExtractionFilterChangedMessage(IsPmcExtraction));
                        break;
                    case nameof(ViewMode):
                        SaveAndBroadcastViewMode();
                        break;
                }
            };
        }

        private void NotifyInitialMapSelection()
        {
            if (SelectedMapInfo == null)
            {
                return;
            }

            NotifyMapSelectionChanged();
            Logger.SimpleLog($"[MainWindowViewModel] Initial map selection notified: {SelectedMapInfo.MapId}");
        }

        private void NotifyMapSelectionChanged()
        {
            WeakReferenceMessenger.Default.Send(new MapSelectionChangedMessage(SelectedMapInfo));
        }

        [RelayCommand]
        private void TogglePinMode()
        {
            Logger.SimpleLog($"TogglePinMode called. Current state: {IsAlwaysOnTop}");
            IsAlwaysOnTop = !IsAlwaysOnTop;
        }

        [RelayCommand]
        private void SaveSettings()
        {
            _windowStateManager.UpdateNormalBounds(new(CurrentWindowBounds.Left, CurrentWindowBounds.Top, CurrentWindowBounds.Width, CurrentWindowBounds.Height));
            Logger.SimpleLog($"[SaveSettings] Saved window bounds: {CurrentWindowBounds}");

            var settings = App.GetSettings();
            _windowStateManager.SaveToSettings(settings);
            App.SetSettings(settings);
            Settings.Save();
        }

        [RelayCommand]
        private void ToggleSettings()
        {
            IsSettingsOpen = !IsSettingsOpen;
            Logger.SimpleLog($"[ToggleSettings] IsSettingsOpen: {IsSettingsOpen}");
        }

        [RelayCommand]
        private void CloseSettings()
        {
            IsSettingsOpen = false;
            Logger.SimpleLog("[CloseSettings] Settings closed");
        }

        [RelayCommand]
        private void SetViewMode(string? modeName)
        {
            if (Enum.TryParse(modeName, out MapViewMode mode) && Enum.IsDefined(mode))
            {
                ViewMode = mode;
                return;
            }

            Logger.SimpleLog($"[SetViewMode] Unknown view mode: '{modeName}'");
        }

        [RelayCommand]
        private void SetPmcExtraction(object parameter)
        {
            if (parameter is string strValue)
            {
                IsPmcExtraction = bool.Parse(strValue);
            }
            else if (parameter is bool boolValue)
            {
                IsPmcExtraction = boolValue;
            }
            Logger.SimpleLog($"[SetPmcExtraction] IsPmcExtraction: {IsPmcExtraction}");
        }

        private void OnSelectedMapInfoChanged()
        {
            if (SelectedMapInfo != null)
            {
                Logger.SimpleLog($"[OnSelectedMapInfoChanged] SelectedMapInfo changed to: {SelectedMapInfo.MapId}");

                var settings = App.GetSettings();
                settings.SelectedMapId = SelectedMapInfo.MapId;
                App.SetSettings(settings);
                Settings.Save();

                UpdateGoonsOnCurrentMap();
            }
        }

        private void SaveAndBroadcastViewMode()
        {
            var settings = App.GetSettings();
            settings.ViewMode = ViewMode.ToString();
            App.SetSettings(settings);
            Settings.Save();

            Logger.SimpleLog($"[MainWindowViewModel] View mode changed: {ViewMode}");
            WeakReferenceMessenger.Default.Send(new ViewModeChangedMessage(ViewMode));
        }

        private void OnHideWebElementsChanged()
        {
            Logger.SimpleLog($"[OnHideWebElementsChanged] HideWebElements changed to: {HideWebElements}");
        }

        public void OnWindowBoundsChanged(object? sender, TanukiTarkovMap.Views.WindowBoundsChangedEventArgs e)
        {
            CurrentWindowLeft = e.Bounds.Left;
            CurrentWindowTop = e.Bounds.Top;
            CurrentWindowWidth = e.Bounds.Width;
            CurrentWindowHeight = e.Bounds.Height;

            if (IsRaidFocusMove) return;
            WindowBoundsChangedByUser?.Invoke(this, EventArgs.Empty);
            var home = RaidFocusHomePosition;
            _windowStateManager.UpdateNormalBounds(new(home?.X ?? e.Bounds.Left, home?.Y ?? e.Bounds.Top, e.Bounds.Width, e.Bounds.Height));
            var settings = App.GetSettings();
            _windowStateManager.SaveToSettings(settings);
            App.SetSettings(settings);
            ScheduleBoundsSave();
        }

        private const int BoundsSaveDelayMs = 400;
        private DispatcherTimer? _boundsSaveTimer;

        private void ScheduleBoundsSave()
        {
            if (_boundsSaveTimer == null)
            {
                _boundsSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(BoundsSaveDelayMs) };
                _boundsSaveTimer.Tick += (_, _) => FlushPendingBoundsSave();
            }
            _boundsSaveTimer.Stop();
            _boundsSaveTimer.Start();
        }

        internal void FlushPendingBoundsSave()
        {
            if (_boundsSaveTimer?.IsEnabled != true) return;
            _boundsSaveTimer.Stop();
            try { Settings.Save(); }
            catch (Exception ex) { Logger.Error("[MainWindowViewModel] Window bounds save failed", ex); }
        }

        partial void OnIsSettingsOpenChanged(bool value)
        {
            OnPropertyChanged(nameof(SettingsOverlayVisibility));
            OnPropertyChanged(nameof(BrowserContainerVisibility));

            if (value)
            {
                WeakReferenceMessenger.Default.Send(new SettingsOpenedMessage());
            }
        }

        public void UpdateWindowPosition(double left, double top)
        {
            CurrentWindowLeft = left;
            CurrentWindowTop = top;
        }

        public void Receive(TopBarHiddenChangedMessage message)
        {
            IsTopBarHidden = message.Value;
        }

        public void Receive(OpacitySliderDragMessage message)
        {
            IsOpacitySliderDragging = message.Value;
        }

        public void Receive(UpdateReadyMessage message)
        {
            Application.Current.Dispatcher.Invoke(() => IsUpdateReady = true);
            Logger.SimpleLog($"[MainWindowViewModel] Update ready indicator shown: v{message.Value}");
        }

        public void Receive(UpdateIconPreviewMessage message)
        {
            Application.Current.Dispatcher.Invoke(() => IsUpdateIconForced = message.Value);
        }

        [RelayCommand]
        private void RestartToApplyUpdate()
        {
            ServiceLocator.UpdateService.ApplyAndRestartNow();
        }

    }
}
