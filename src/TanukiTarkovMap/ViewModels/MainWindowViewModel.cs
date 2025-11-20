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
        private readonly WindowStateManager _windowStateManager;
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
            _windowStateManager = new WindowStateManager();
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

            // WindowStateManager에 설정 로드
            _windowStateManager.LoadFromSettings(_settings);

            // Normal 모드 Rect 가져오기
            var normalRect = _windowStateManager.NormalModeRect;
            NormalWidth = normalRect.Width;
            NormalHeight = normalRect.Height;
            NormalLeft = normalRect.Left;
            NormalTop = normalRect.Top;

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
            // Save current window state to WindowStateManager
            var currentRect = new Rect(WindowLeft, WindowTop, WindowWidth, WindowHeight);

            if (IsPipMode)
            {
                if (!string.IsNullOrEmpty(CurrentMap))
                {
                    _windowStateManager.UpdatePipModeRect(CurrentMap, currentRect);
                    Logger.SimpleLog($"[SaveSettings] Saved PIP mode for {CurrentMap}: {currentRect}");
                }
            }
            else
            {
                _windowStateManager.UpdateNormalModeRect(currentRect);

                // Update ViewModel properties for consistency
                NormalWidth = WindowWidth;
                NormalHeight = WindowHeight;
                NormalLeft = WindowLeft;
                NormalTop = WindowTop;

                Logger.SimpleLog($"[SaveSettings] Saved Normal mode: {currentRect}");
            }

            // Persist to disk
            var settings = App.GetSettings();
            _windowStateManager.SaveToSettings(settings);
            App.SetSettings(settings);
            Settings.Save();
        }
        #endregion

        #region Private Methods
        private void OnPipModeChanged()
        {
            Logger.SimpleLog($"PIP Mode changed to: {IsPipMode}");

            if (IsPipMode)
            {
                // 일반 모드 위치를 WindowStateManager에 즉시 저장 (이벤트 발생 전에 저장)
                var currentRect = new Rect(WindowLeft, WindowTop, WindowWidth, WindowHeight);
                _windowStateManager.UpdateNormalModeRect(currentRect);

                var settings = App.GetSettings();
                _windowStateManager.SaveToSettings(settings);
                App.SetSettings(settings);
                Settings.Save();

                Logger.SimpleLog($"[OnPipModeChanged] Saved Normal mode: {currentRect}");

                EnterPipMode();
            }
            else
            {
                // PIP 모드 위치를 WindowStateManager에 즉시 저장
                if (!string.IsNullOrEmpty(CurrentMap))
                {
                    var currentRect = new Rect(WindowLeft, WindowTop, WindowWidth, WindowHeight);
                    _windowStateManager.UpdatePipModeRect(CurrentMap, currentRect);

                    var settings = App.GetSettings();
                    _windowStateManager.SaveToSettings(settings);
                    App.SetSettings(settings);
                    Settings.Save();

                    Logger.SimpleLog($"[OnPipModeChanged] Saved PIP mode for {CurrentMap}: {currentRect}");
                }

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
                // Load PIP settings for new map from WindowStateManager
                var pipRect = _windowStateManager.GetPipModeRect(CurrentMap);
                Logger.SimpleLog($"[OnMapChanged] Loaded PIP rect for {CurrentMap}: {pipRect}");

                // Apply new map's PIP settings
                WindowWidth = pipRect.Width;
                WindowHeight = pipRect.Height;

                if (pipRect.Left >= 0 && pipRect.Top >= 0)
                {
                    WindowLeft = pipRect.Left;
                    WindowTop = pipRect.Top;
                }
                else
                {
                    // Default position: bottom right
                    WindowLeft = SystemParameters.PrimaryScreenWidth - pipRect.Width - 20;
                    WindowTop = SystemParameters.PrimaryScreenHeight - pipRect.Height - 80;
                }
            }
        }

        private void EnterPipMode()
        {
            // Load PIP settings from WindowStateManager
            Rect pipRect;
            if (!string.IsNullOrEmpty(CurrentMap))
            {
                pipRect = _windowStateManager.GetPipModeRect(CurrentMap);
                Logger.SimpleLog($"[EnterPipMode] Loaded PIP rect for {CurrentMap}: {pipRect}");
            }
            else
            {
                // Use default PIP settings
                pipRect = new Rect(-1, -1, 300, 250);
                Logger.SimpleLog($"[EnterPipMode] Using default PIP rect: {pipRect}");
            }

            // Apply PIP size
            WindowWidth = pipRect.Width;
            WindowHeight = pipRect.Height;

            // Apply PIP position (use default if not set)
            if (pipRect.Left >= 0 && pipRect.Top >= 0)
            {
                WindowLeft = pipRect.Left;
                WindowTop = pipRect.Top;
            }
            else
            {
                // Default position: bottom right
                WindowLeft = SystemParameters.PrimaryScreenWidth - pipRect.Width - 20;
                WindowTop = SystemParameters.PrimaryScreenHeight - pipRect.Height - 80;
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
            // Load Normal mode settings from WindowStateManager
            var normalRect = _windowStateManager.NormalModeRect;
            Logger.SimpleLog($"[ExitPipMode] Loaded Normal rect: {normalRect}");

            // Restore normal mode settings
            WindowWidth = normalRect.Width;
            WindowHeight = normalRect.Height;
            WindowLeft = normalRect.Left;
            WindowTop = normalRect.Top;

            // Update ViewModel properties for consistency
            NormalWidth = normalRect.Width;
            NormalHeight = normalRect.Height;
            NormalLeft = normalRect.Left;
            NormalTop = normalRect.Top;

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


        /// <summary>
        /// View에서 창 위치/크기 변경 이벤트를 받아 즉시 저장
        /// WindowStateManager를 통해 Normal/PIP 모드 Rect를 분리 관리
        /// </summary>
        public void OnWindowBoundsChanged(object? sender, TanukiTarkovMap.Views.WindowBoundsChangedEventArgs e)
        {
            Logger.SimpleLog($"[OnWindowBoundsChanged] Bounds={e.Bounds}, IsPipMode={e.IsPipMode}, CurrentMap={CurrentMap}");

            // ViewModel 속성 업데이트 (바인딩용)
            WindowLeft = e.Bounds.Left;
            WindowTop = e.Bounds.Top;
            WindowWidth = e.Bounds.Width;
            WindowHeight = e.Bounds.Height;

            // WindowStateManager를 통해 즉시 저장 (타이머 없음)
            _windowStateManager.UpdateAndSave(e.Bounds, e.IsPipMode, CurrentMap);
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