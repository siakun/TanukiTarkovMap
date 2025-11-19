using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using TanukiTarkovMap.Models.Data;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly PipService _pipService;
        private readonly WindowBoundsService _windowBoundsService;
        private AppSettings _settings;

        #region Properties for Normal Mode
        [ObservableProperty] private double _normalWidth = 1000;
        [ObservableProperty] private double _normalHeight = 700;
        [ObservableProperty] private double _normalLeft;
        [ObservableProperty] private double _normalTop;
        #endregion

        #region Properties for PIP Mode
        [ObservableProperty] private string _currentMap;
        [ObservableProperty] private bool _isPipMode;
        [ObservableProperty] private double _pipWidth = 300;
        [ObservableProperty] private double _pipHeight = 250;
        [ObservableProperty] private double _pipLeft;
        [ObservableProperty] private double _pipTop;
        [ObservableProperty] private bool _pipHotkeyEnabled = true;
        [ObservableProperty] private string _pipHotkeyKey = "F11";
        #endregion

        #region Window Properties
        [ObservableProperty] private double _windowWidth;
        [ObservableProperty] private double _windowHeight;
        [ObservableProperty] private double _windowLeft;
        [ObservableProperty] private double _windowTop;
        [ObservableProperty] private WindowStyle _windowStyle = WindowStyle.SingleBorderWindow;
        [ObservableProperty] private ResizeMode _resizeMode = ResizeMode.CanResize;
        [ObservableProperty] private bool _isTopmost;
        [ObservableProperty] private double _minWidth = 1000;
        [ObservableProperty] private double _minHeight = 700;
        #endregion

        #region UI Visibility Properties
        [ObservableProperty] private Visibility _tabSidebarVisibility = Visibility.Visible;
        [ObservableProperty] private Thickness _tabContainerMargin = new Thickness(0);
        [ObservableProperty] private int _tabContainerColumn = 1;
        [ObservableProperty] private int _tabContainerColumnSpan = 1;
        #endregion

        public MainWindowViewModel() : this(new PipService(), new WindowBoundsService()) { }

        public MainWindowViewModel(PipService pipService, WindowBoundsService windowBoundsService)
        {
            _pipService = pipService;
            _windowBoundsService = windowBoundsService;
            LoadSettings();
            InitializeCommands();
            SubscribeToMapEvents();
        }

        /// <summary>
        /// MapEventService 이벤트 구독
        /// </summary>
        private void SubscribeToMapEvents()
        {
            Logger.SimpleLog("[MainWindowViewModel] Subscribing to MapEventService events");

            MapEventService.Instance.MapChanged += OnMapEventReceived;
            MapEventService.Instance.ScreenshotTaken += OnScreenshotEventReceived;

            Logger.SimpleLog("[MainWindowViewModel] Successfully subscribed to MapEventService events");
        }

        /// <summary>
        /// 맵 변경 이벤트 처리
        /// </summary>
        private void OnMapEventReceived(object sender, MapChangedEventArgs e)
        {
            Logger.SimpleLog($"[MainWindowViewModel] MapEvent received: {e.MapName}");
            Logger.SimpleLog($"[MainWindowViewModel] Current IsPipMode: {IsPipMode}");

            // CurrentMap 업데이트 (ChangeMapCommand 사용)
            ChangeMapCommand.Execute(e.MapName);

            Logger.SimpleLog($"[MainWindowViewModel] ChangeMapCommand executed for: {e.MapName}");
        }

        /// <summary>
        /// 스크린샷 이벤트 처리
        /// </summary>
        private void OnScreenshotEventReceived(object sender, EventArgs e)
        {
            Logger.SimpleLog("[MainWindowViewModel] Screenshot event received");
            Logger.SimpleLog($"[MainWindowViewModel] Current IsPipMode: {IsPipMode}");

            // PIP 모드가 아닐 때만 활성화
            if (!IsPipMode)
            {
                Logger.SimpleLog("[MainWindowViewModel] Executing TogglePipModeCommand (PIP mode OFF -> ON)");
                TogglePipModeCommand.Execute(null);
            }
            else
            {
                Logger.SimpleLog("[MainWindowViewModel] PIP mode already active, skipping toggle");
            }
        }

        public void LoadSettings()
        {
            _settings = Env.GetSettings();

            // Load normal mode settings
            NormalWidth = _settings.NormalWidth;
            NormalHeight = _settings.NormalHeight;
            NormalLeft = _settings.NormalLeft;
            NormalTop = _settings.NormalTop;

            // Load PIP settings
            PipHotkeyEnabled = _settings.PipHotkeyEnabled;
            PipHotkeyKey = _settings.PipHotkeyKey;

            // Initialize window properties with normal mode
            WindowWidth = NormalWidth;
            WindowHeight = NormalHeight;
            WindowLeft = NormalLeft;
            WindowTop = NormalTop;
        }

        private void InitializeCommands()
        {
            // Property change handlers
            PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(IsPipMode):
                        OnPipModeChanged();
                        break;
                    case nameof(CurrentMap):
                        OnMapChanged();
                        break;
                    case nameof(WindowLeft):
                        Logger.SimpleLog($"[PropertyChanged] WindowLeft changed to: {WindowLeft}, IsPipMode={IsPipMode}");
                        ScheduleSaveSettings();
                        break;
                    case nameof(WindowTop):
                        Logger.SimpleLog($"[PropertyChanged] WindowTop changed to: {WindowTop}, IsPipMode={IsPipMode}");
                        ScheduleSaveSettings();
                        break;
                    case nameof(WindowWidth):
                    case nameof(WindowHeight):
                        ScheduleSaveSettings();
                        break;
                }
            };
        }

        #region Commands
        [RelayCommand]
        private void TogglePipMode()
        {
            Logger.SimpleLog($"TogglePipMode called. Current state: {IsPipMode}");
            IsPipMode = !IsPipMode;
        }

        [RelayCommand]
        private void ChangeMap(string mapName)
        {
            Logger.SimpleLog($"ChangeMap called: {mapName}");
            CurrentMap = mapName;
        }

        [RelayCommand]
        private void SaveSettings()
        {
            if (IsPipMode)
            {
                SavePipSettings();
            }
            else
            {
                SaveNormalSettings();
            }
        }
        #endregion

        #region Private Methods
        private void OnPipModeChanged()
        {
            Logger.SimpleLog($"PIP Mode changed to: {IsPipMode}");

            if (IsPipMode)
            {
                EnterPipMode();
            }
            else
            {
                ExitPipMode();
            }
        }

        private void OnMapChanged()
        {
            if (string.IsNullOrEmpty(CurrentMap))
                return;

            Logger.SimpleLog($"Map changed to: {CurrentMap}");

            if (IsPipMode)
            {
                LoadMapSettings(CurrentMap);
            }
        }

        private void EnterPipMode()
        {
            // Save current normal mode position
            SaveNormalSettings();

            // Load PIP settings for current map
            if (!string.IsNullOrEmpty(CurrentMap))
            {
                LoadMapSettings(CurrentMap);
            }
            else
            {
                // Use default PIP settings
                WindowWidth = PipWidth;
                WindowHeight = PipHeight;

                // Default position: bottom right
                WindowLeft = SystemParameters.PrimaryScreenWidth - PipWidth - 20;
                WindowTop = SystemParameters.PrimaryScreenHeight - PipHeight - 80;
            }

            // Update window style for PIP
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            MinWidth = 200;
            MinHeight = 150;
            IsTopmost = true;

            // Update UI visibility
            TabSidebarVisibility = Visibility.Collapsed;
            TabContainerColumn = 0;
            TabContainerColumnSpan = 2;
            TabContainerMargin = new Thickness(0, -30, 0, 0);
        }

        private void ExitPipMode()
        {
            // Save PIP settings before exiting
            SavePipSettings();

            // Restore normal mode settings
            WindowWidth = NormalWidth;
            WindowHeight = NormalHeight;
            WindowLeft = NormalLeft;
            WindowTop = NormalTop;

            // Restore window style
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            MinWidth = 1000;
            MinHeight = 700;
            IsTopmost = false;

            // Restore UI visibility
            TabSidebarVisibility = Visibility.Visible;
            TabContainerColumn = 1;
            TabContainerColumnSpan = 1;
            TabContainerMargin = new Thickness(0);
        }

        private void LoadMapSettings(string mapName)
        {
            if (_settings.MapSettings == null || !_settings.MapSettings.ContainsKey(mapName))
            {
                // Use default PIP settings
                WindowWidth = PipWidth;
                WindowHeight = PipHeight;
                return;
            }

            var mapSetting = _settings.MapSettings[mapName];

            // Load saved position and size
            WindowWidth = mapSetting.Width;
            WindowHeight = mapSetting.Height;

            if (mapSetting.Left >= 0 && mapSetting.Top >= 0)
            {
                WindowLeft = mapSetting.Left;
                WindowTop = mapSetting.Top;
            }
            else
            {
                // Default position
                WindowLeft = SystemParameters.PrimaryScreenWidth - mapSetting.Width - 20;
                WindowTop = SystemParameters.PrimaryScreenHeight - mapSetting.Height - 80;
            }

            Logger.SimpleLog($"Loaded PIP settings for {mapName}: {mapSetting.Width}x{mapSetting.Height} at ({mapSetting.Left}, {mapSetting.Top})");
        }

        private void SaveNormalSettings()
        {
            if (IsPipMode)
                return;

            _settings.NormalWidth = WindowWidth;
            _settings.NormalHeight = WindowHeight;
            _settings.NormalLeft = WindowLeft;
            _settings.NormalTop = WindowTop;

            NormalWidth = WindowWidth;
            NormalHeight = WindowHeight;
            NormalLeft = WindowLeft;
            NormalTop = WindowTop;

            Env.SetSettings(_settings);
            Settings.Save();

            Logger.SimpleLog($"Saved normal settings: {NormalWidth}x{NormalHeight} at ({NormalLeft}, {NormalTop})");
        }

        private void SavePipSettings()
        {
            if (!IsPipMode)
                return;

            // CurrentMap이 없으면 기본 키로 저장
            string mapKey = string.IsNullOrEmpty(CurrentMap) ? "default" : CurrentMap;

            if (_settings.MapSettings == null)
            {
                _settings.MapSettings = new System.Collections.Generic.Dictionary<string, MapSetting>();
            }

            if (!_settings.MapSettings.ContainsKey(mapKey))
            {
                _settings.MapSettings[mapKey] = new MapSetting();
            }

            var mapSetting = _settings.MapSettings[mapKey];
            mapSetting.Width = WindowWidth;
            mapSetting.Height = WindowHeight;
            mapSetting.Left = WindowLeft;
            mapSetting.Top = WindowTop;

            Env.SetSettings(_settings);
            Settings.Save();

            Logger.SimpleLog($"Saved PIP settings for {mapKey}: {WindowWidth}x{WindowHeight} at ({WindowLeft}, {WindowTop})");
        }

        private System.Windows.Threading.DispatcherTimer _saveTimer;

        private void ScheduleSaveSettings()
        {
            // Debounce save operations
            _saveTimer?.Stop();

            if (_saveTimer == null)
            {
                _saveTimer = new System.Windows.Threading.DispatcherTimer();
                _saveTimer.Interval = System.TimeSpan.FromMilliseconds(500);
                _saveTimer.Tick += (s, e) =>
                {
                    _saveTimer.Stop();
                    SaveSettingsCommand.Execute(null);
                };
            }

            _saveTimer.Start();
        }

        #endregion
    }
}