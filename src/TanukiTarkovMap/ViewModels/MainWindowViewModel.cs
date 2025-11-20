using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
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

        #region Current Window Properties
        [ObservableProperty] public partial double CurrentWindowWidth { get; set; }
        [ObservableProperty] public partial double CurrentWindowHeight { get; set; }
        [ObservableProperty] public partial double CurrentWindowLeft { get; set; }
        [ObservableProperty] public partial double CurrentWindowTop { get; set; }
        [ObservableProperty] public partial WindowStyle WindowStyle { get; set; } = WindowStyle.SingleBorderWindow;
        [ObservableProperty] public partial ResizeMode ResizeMode { get; set; } = ResizeMode.CanResize;
        [ObservableProperty] public partial bool IsTopmost { get; set; }
        [ObservableProperty] public partial double MinWidth { get; set; } = 1000;
        [ObservableProperty] public partial double MinHeight { get; set; } = 700;
        #endregion

        #region UI Visibility Properties
        [ObservableProperty] public partial Visibility TopBarVisibility { get; set; } = Visibility.Visible;
        [ObservableProperty] public partial Visibility TabSidebarVisibility { get; set; } = Visibility.Visible;
        [ObservableProperty] public partial Thickness TabContainerMargin { get; set; } = new Thickness(0);
        [ObservableProperty] public partial int TabContainerColumn { get; set; } = 1;
        [ObservableProperty] public partial int TabContainerColumnSpan { get; set; } = 1;
        [ObservableProperty] public partial Visibility TabItemHeaderVisibility { get; set; } = Visibility.Visible;
        #endregion

        #region PIP Mode Properties
        [ObservableProperty] public partial string CurrentMap { get; set; }
        [ObservableProperty] public partial bool IsPipMode { get; set; }
        [ObservableProperty] public partial bool PipHotkeyEnabled { get; set; } = true;
        [ObservableProperty] public partial string PipHotkeyKey { get; set; } = "F11";
        [ObservableProperty] public partial bool PipHideWebElements { get; set; } = true;
        #endregion

        #region Map Selection Properties
        /// <summary> 사용 가능한 맵 목록 </summary>
        public List<MapInfo> AvailableMaps => App.AvailableMaps;

        /// <summary> 선택된 맵 정보 </summary>
        [ObservableProperty] public partial MapInfo SelectedMapInfo { get; set; }
        #endregion

        #region Computed Bounds Properties (Read-only)
        /// <summary> 현재 창의 Rect (현재 모드의 위치/크기) </summary>
        public Rect CurrentWindowBounds => new Rect(CurrentWindowLeft, CurrentWindowTop, CurrentWindowWidth, CurrentWindowHeight);

        /// <summary> Normal 모드의 Rect (WindowStateManager에서 관리) </summary>
        public Rect NormalModeBounds => _windowStateManager.NormalModeRect;

        /// <summary> 특정 맵의 PIP 모드 Rect 가져오기 </summary>
        public Rect GetPipModeBounds(string mapName) => _windowStateManager.GetPipModeRect(mapName);
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

            // Load PIP settings
            PipHotkeyEnabled = _settings.PipHotkeyEnabled;
            PipHotkeyKey = _settings.PipHotkeyKey;
            PipHideWebElements = _settings.PipHideWebElements;

            // Initialize window properties with normal mode
            var normalRect = _windowStateManager.NormalModeRect;
            CurrentWindowWidth = normalRect.Width;
            CurrentWindowHeight = normalRect.Height;
            CurrentWindowLeft = normalRect.Left;
            CurrentWindowTop = normalRect.Top;
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
                    case nameof(PipHideWebElements):
                        OnPipHideWebElementsChanged();
                        break;
                    case nameof(CurrentWindowLeft):
                        Logger.SimpleLog($"[PropertyChanged] CurrentWindowLeft changed to: {CurrentWindowLeft}, IsPipMode={IsPipMode}");
                        break;
                    case nameof(CurrentWindowTop):
                        Logger.SimpleLog($"[PropertyChanged] CurrentWindowTop changed to: {CurrentWindowTop}, IsPipMode={IsPipMode}");
                        break;
                    case nameof(CurrentWindowWidth):
                    case nameof(CurrentWindowHeight):
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
            if (IsPipMode)
            {
                if (!string.IsNullOrEmpty(CurrentMap))
                {
                    _windowStateManager.UpdatePipModeRect(CurrentMap, CurrentWindowBounds);
                    Logger.SimpleLog($"[SaveSettings] Saved PIP mode for {CurrentMap}: {CurrentWindowBounds}");
                }
            }
            else
            {
                _windowStateManager.UpdateNormalModeRect(CurrentWindowBounds);
                Logger.SimpleLog($"[SaveSettings] Saved Normal mode: {CurrentWindowBounds}");
            }

            // Persist to disk
            var settings = App.GetSettings();
            _windowStateManager.SaveToSettings(settings);
            settings.PipHideWebElements = PipHideWebElements;
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
                _windowStateManager.UpdateNormalModeRect(CurrentWindowBounds);

                var settings = App.GetSettings();
                _windowStateManager.SaveToSettings(settings);
                App.SetSettings(settings);
                Settings.Save();

                Logger.SimpleLog($"[OnPipModeChanged] Saved Normal mode: {CurrentWindowBounds}");

                EnterPipMode();
            }
            else
            {
                // PIP 모드 위치를 WindowStateManager에 즉시 저장
                if (!string.IsNullOrEmpty(CurrentMap))
                {
                    _windowStateManager.UpdatePipModeRect(CurrentMap, CurrentWindowBounds);

                    var settings = App.GetSettings();
                    _windowStateManager.SaveToSettings(settings);
                    App.SetSettings(settings);
                    Settings.Save();

                    Logger.SimpleLog($"[OnPipModeChanged] Saved PIP mode for {CurrentMap}: {CurrentWindowBounds}");
                }

                ExitPipMode();
            }
        }

        private void OnMapChanged()
        {
            // CurrentMap이 null이어도 "default" 키로 처리되므로 early return 하지 않음
            string mapKey = string.IsNullOrEmpty(CurrentMap) ? "default" : CurrentMap;
            Logger.SimpleLog($"Map changed to: {mapKey}");

            // PIP 모드일 때는 창 크기/위치를 변경하지 않음 (맵별로 크기가 달라지지 않도록)
            if (IsPipMode)
            {
                Logger.SimpleLog($"[OnMapChanged] PIP mode active - maintaining current window size and position");
                return;
            }
        }

        private void OnPipHideWebElementsChanged()
        {
            Logger.SimpleLog($"[OnPipHideWebElementsChanged] PipHideWebElements changed to: {PipHideWebElements}");

            // 설정 저장
            var settings = App.GetSettings();
            settings.PipHideWebElements = PipHideWebElements;
            App.SetSettings(settings);
            Settings.Save();

            // PIP 모드가 활성화되어 있으면 View에서 PropertyChanged 이벤트를 감지하여 재적용함
        }

        private void EnterPipMode()
        {
            // Load PIP settings from WindowStateManager
            // GetPipModeRect()는 CurrentMap이 null이면 "default" 키를 사용
            Rect pipRect = _windowStateManager.GetPipModeRect(CurrentMap);

            string mapKey = string.IsNullOrEmpty(CurrentMap) ? "default" : CurrentMap;
            Logger.SimpleLog($"[EnterPipMode] Loaded PIP rect for {mapKey}: {pipRect}");

            // Apply PIP size
            CurrentWindowWidth = pipRect.Width;
            CurrentWindowHeight = pipRect.Height;

            // Apply PIP position (use default if not set)
            if (pipRect.Left >= 0 && pipRect.Top >= 0)
            {
                CurrentWindowLeft = pipRect.Left;
                CurrentWindowTop = pipRect.Top;
            }
            else
            {
                // Default position: bottom right
                CurrentWindowLeft = SystemParameters.PrimaryScreenWidth - pipRect.Width - 20;
                CurrentWindowTop = SystemParameters.PrimaryScreenHeight - pipRect.Height - 80;
            }

            // Update window style for PIP
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            MinWidth = 200;
            MinHeight = 150;
            IsTopmost = true;

            // Update UI visibility - 사이드바만 숨김, 상단 바(드롭다운+체크박스)는 유지
            TopBarVisibility = Visibility.Visible;  // 드롭다운과 체크박스는 보이게 유지
            TabSidebarVisibility = Visibility.Collapsed;
            TabItemHeaderVisibility = Visibility.Collapsed;  // 탭 헤더 숨김 (Margin 조정 불필요)
            TabContainerColumn = 0;
            TabContainerColumnSpan = 2;
            TabContainerMargin = new Thickness(0);  // 탭 헤더는 Visibility로 숨기므로 Margin 조정 불필요
        }

        private void ExitPipMode()
        {
            // Load Normal mode settings from WindowStateManager
            var normalRect = _windowStateManager.NormalModeRect;
            Logger.SimpleLog($"[ExitPipMode] Loaded Normal rect: {normalRect}");

            // Restore normal mode settings
            CurrentWindowWidth = normalRect.Width;
            CurrentWindowHeight = normalRect.Height;
            CurrentWindowLeft = normalRect.Left;
            CurrentWindowTop = normalRect.Top;

            // Restore window style
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            MinWidth = 1000;
            MinHeight = 700;
            IsTopmost = false;

            // Restore UI visibility - 상단 바와 사이드바 복원
            TopBarVisibility = Visibility.Visible;
            TabSidebarVisibility = Visibility.Visible;
            TabItemHeaderVisibility = Visibility.Visible;  // 탭 헤더 복원
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
            CurrentWindowLeft = e.Bounds.Left;
            CurrentWindowTop = e.Bounds.Top;
            CurrentWindowWidth = e.Bounds.Width;
            CurrentWindowHeight = e.Bounds.Height;

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
            CurrentWindowLeft = left;
            CurrentWindowTop = top;
        }

        #endregion
    }
}