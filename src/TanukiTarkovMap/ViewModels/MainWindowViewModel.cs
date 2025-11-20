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
        [ObservableProperty] public partial double NormalWidth { get; set; } = 1000;
        [ObservableProperty] public partial double NormalHeight { get; set; } = 700;
        [ObservableProperty] public partial double NormalLeft { get; set; }
        [ObservableProperty] public partial double NormalTop { get; set; }
        #endregion

        #region Properties for PIP Mode
        [ObservableProperty] public partial string CurrentMap { get; set; }
        [ObservableProperty] public partial bool IsPipMode { get; set; }
        [ObservableProperty] public partial double PipWidth { get; set; } = 300;
        [ObservableProperty] public partial double PipHeight { get; set; } = 250;
        [ObservableProperty] public partial double PipLeft { get; set; }
        [ObservableProperty] public partial double PipTop { get; set; }
        [ObservableProperty] public partial bool PipHotkeyEnabled { get; set; } = true;
        [ObservableProperty] public partial string PipHotkeyKey { get; set; } = "F11";
        #endregion

        #region Window Properties
        [ObservableProperty] public partial double WindowWidth { get; set; }
        [ObservableProperty] public partial double WindowHeight { get; set; }
        [ObservableProperty] public partial double WindowLeft { get; set; }
        [ObservableProperty] public partial double WindowTop { get; set; }
        [ObservableProperty] public partial WindowStyle WindowStyle { get; set; } = WindowStyle.SingleBorderWindow;
        [ObservableProperty] public partial ResizeMode ResizeMode { get; set; } = ResizeMode.CanResize;
        [ObservableProperty] public partial bool IsTopmost { get; set; }
        [ObservableProperty] public partial double MinWidth { get; set; } = 1000;
        [ObservableProperty] public partial double MinHeight { get; set; } = 700;
        #endregion

        #region UI Visibility Properties
        [ObservableProperty] public partial Visibility TabSidebarVisibility { get; set; } = Visibility.Visible;
        [ObservableProperty] public partial Thickness TabContainerMargin { get; set; } = new Thickness(0);
        [ObservableProperty] public partial int TabContainerColumn { get; set; } = 1;
        [ObservableProperty] public partial int TabContainerColumnSpan { get; set; } = 1;
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
            _settings = App.GetSettings();

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
                        break;
                    case nameof(WindowTop):
                        Logger.SimpleLog($"[PropertyChanged] WindowTop changed to: {WindowTop}, IsPipMode={IsPipMode}");
                        break;
                    case nameof(WindowWidth):
                    case nameof(WindowHeight):
                        // 크기 변경은 View의 SizeChanged 이벤트에서 처리
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
                // 일반 모드 위치를 즉시 저장 (타이머 대기 안 함)
                SaveNormalSettings();
                EnterPipMode();
            }
            else
            {
                // PIP 모드 위치를 즉시 저장
                SavePipSettings();
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

            App.SetSettings(_settings);
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

            App.SetSettings(_settings);
            Settings.Save();

            Logger.SimpleLog($"Saved PIP settings for {mapKey}: {WindowWidth}x{WindowHeight} at ({WindowLeft}, {WindowTop})");
        }

        /// <summary>
        /// View에서 창 위치/크기 변경 이벤트를 받아 즉시 저장
        /// (모듈화 고려: 나중에 별도 서비스로 분리 가능)
        /// </summary>
        public void OnWindowBoundsChanged(object? sender, TanukiTarkovMap.Views.WindowBoundsChangedEventArgs e)
        {
            Logger.SimpleLog($"[OnWindowBoundsChanged] Bounds={e.Bounds}, IsPipMode={e.IsPipMode}");

            // ViewModel 속성 업데이트
            WindowLeft = e.Bounds.Left;
            WindowTop = e.Bounds.Top;
            WindowWidth = e.Bounds.Width;
            WindowHeight = e.Bounds.Height;

            // 즉시 저장 (타이머 없음)
            if (e.IsPipMode)
            {
                SavePipSettings();
            }
            else
            {
                SaveNormalSettings();
            }
        }

        #endregion

        #region Public Methods for View

        /// <summary>
        /// View에서 창 위치 변경 시 ViewModel 업데이트 (MVVM 패턴 준수)
        /// </summary>
        public void UpdateWindowPosition(double left, double top)
        {
            WindowLeft = left;
            WindowTop = top;
        }

        #endregion
    }
}