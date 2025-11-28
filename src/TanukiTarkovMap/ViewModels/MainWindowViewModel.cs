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
        private readonly WindowBoundsService _windowBoundsService;
        private readonly WindowStateManager _windowStateManager;
        private readonly MapEventService _mapEventService;
        private AppSettings _settings;

        #region Current Window Properties
        [ObservableProperty] public partial double CurrentWindowWidth { get; set; }
        [ObservableProperty] public partial double CurrentWindowHeight { get; set; }
        [ObservableProperty] public partial double CurrentWindowLeft { get; set; }
        [ObservableProperty] public partial double CurrentWindowTop { get; set; }
        [ObservableProperty] public partial ResizeMode ResizeMode { get; set; } = ResizeMode.CanResize;
        [ObservableProperty] public partial bool IsTopmost { get; set; }
        [ObservableProperty] public partial double MinWidth { get; set; } = 300;
        [ObservableProperty] public partial double MinHeight { get; set; } = 200;

        /// <summary> 핀 모드 (항상 위) 활성화 여부 </summary>
        private bool _isAlwaysOnTop;
        public bool IsAlwaysOnTop
        {
            get => _isAlwaysOnTop;
            set
            {
                if (SetProperty(ref _isAlwaysOnTop, value))
                {
                    // IsAlwaysOnTop이 변경되면 IsTopmost도 Compact 모드가 아닐 때만 업데이트
                    if (!IsCompactMode)
                    {
                        IsTopmost = value;
                    }

                    // Settings에 저장
                    var settings = App.GetSettings();
                    settings.IsAlwaysOnTop = value;
                    App.SetSettings(settings);
                    Settings.Save();

                    Logger.SimpleLog($"[IsAlwaysOnTop] Changed to: {value}, IsTopmost updated to: {IsTopmost}");
                }
            }
        }
        #endregion

        #region UI Visibility Properties
        [ObservableProperty] public partial Visibility TopBarVisibility { get; set; } = Visibility.Visible;

        /// <summary> Compact 모드일 때 최소화/최대화 버튼 숨김 (닫기만 표시) </summary>
        public Visibility WindowControlButtonsVisibility => IsCompactMode ? Visibility.Collapsed : Visibility.Visible;

        /// <summary> 설정 오버레이 표시 여부 </summary>
        [ObservableProperty] public partial bool IsSettingsOpen { get; set; } = false;

        /// <summary> 설정 오버레이 Visibility (IsSettingsOpen과 연동) </summary>
        public Visibility SettingsOverlayVisibility => IsSettingsOpen ? Visibility.Visible : Visibility.Collapsed;

        /// <summary> WebView 컨테이너 Visibility (설정이 열리면 숨김) </summary>
        public Visibility WebViewContainerVisibility => IsSettingsOpen ? Visibility.Collapsed : Visibility.Visible;
        #endregion

        #region Compact Mode Properties
        [ObservableProperty] public partial string CurrentMap { get; set; }
        [ObservableProperty] public partial bool IsCompactMode { get; set; }
        [ObservableProperty] public partial bool HotkeyEnabled { get; set; } = true;
        [ObservableProperty] public partial string HotkeyKey { get; set; } = "F11";
        [ObservableProperty] public partial bool HideWebElements { get; set; } = true;
        #endregion

        #region Map Selection Properties
        /// <summary> 사용 가능한 맵 목록 </summary>
        public List<MapInfo> AvailableMaps => App.AvailableMaps;

        /// <summary> 선택된 맵 정보 </summary>
        [ObservableProperty] public partial MapInfo SelectedMapInfo { get; set; }
        #endregion

        #region WebView Zoom Properties
        /// <summary> 사용 가능한 WebView 배율 목록 </summary>
        public List<int> AvailableZoomLevels { get; } = new List<int> { 50, 67, 75, 80, 90, 100, 110, 125, 150, 175, 200 };

        private int _selectedZoomLevel = 67;
        /// <summary> 선택된 WebView 배율 (%) </summary>
        public int SelectedZoomLevel
        {
            get => _selectedZoomLevel;
            set
            {
                if (SetProperty(ref _selectedZoomLevel, value))
                {
                    // Settings에 저장
                    var settings = App.GetSettings();
                    settings.WebViewZoomLevel = value;
                    App.SetSettings(settings);
                    Settings.Save();

                    Logger.SimpleLog($"[SelectedZoomLevel] Changed to: {value}%");
                }
            }
        }
        #endregion

        #region Computed Bounds Properties (Read-only)
        /// <summary> 현재 창의 Rect (현재 모드의 위치/크기) </summary>
        public Rect CurrentWindowBounds => new Rect(CurrentWindowLeft, CurrentWindowTop, CurrentWindowWidth, CurrentWindowHeight);

        /// <summary> Normal 모드의 Rect (WindowStateManager에서 관리) </summary>
        public Rect NormalModeBounds => _windowStateManager.NormalModeRect;

        /// <summary> Compact 모드 Rect 가져오기 (모든 맵에서 동일) </summary>
        public Rect GetCompactModeBounds() => _windowStateManager.GetCompactModeRect();
        #endregion

        /// <summary>
        /// DI 컨테이너에서 호출되는 생성자
        /// </summary>
        public MainWindowViewModel(
            WindowBoundsService windowBoundsService,
            WindowStateManager windowStateManager,
            MapEventService mapEventService)
        {
            _windowBoundsService = windowBoundsService;
            _windowStateManager = windowStateManager;
            _mapEventService = mapEventService;
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

            _mapEventService.MapChanged += OnMapEventReceived;
            _mapEventService.ScreenshotTaken += OnScreenshotEventReceived;

            Logger.SimpleLog("[MainWindowViewModel] Successfully subscribed to MapEventService events");
        }

        /// <summary>
        /// 맵 변경 이벤트 처리
        /// </summary>
        private void OnMapEventReceived(object sender, MapChangedEventArgs e)
        {
            // UI 스레드로 마샬링
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Logger.SimpleLog($"[MainWindowViewModel] MapEvent received: {e.MapName}");
                Logger.SimpleLog($"[MainWindowViewModel] Current IsCompactMode: {IsCompactMode}");

                // CurrentMap 업데이트 (ChangeMapCommand 사용)
                ChangeMapCommand.Execute(e.MapName);

                Logger.SimpleLog($"[MainWindowViewModel] ChangeMapCommand executed for: {e.MapName}");
            });
        }

        /// <summary>
        /// 스크린샷 이벤트 처리
        /// </summary>
        private void OnScreenshotEventReceived(object sender, EventArgs e)
        {
            // UI 스레드로 마샬링
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Logger.SimpleLog("[MainWindowViewModel] Screenshot event received");
                Logger.SimpleLog($"[MainWindowViewModel] Current IsCompactMode: {IsCompactMode}");

                // Compact 모드가 아닐 때만 활성화
                if (!IsCompactMode)
                {
                    Logger.SimpleLog("[MainWindowViewModel] Executing ToggleCompactModeCommand (Compact mode OFF -> ON)");
                    ToggleCompactModeCommand.Execute(null);
                }
                else
                {
                    Logger.SimpleLog("[MainWindowViewModel] Compact mode already active, skipping toggle");
                }
            });
        }

        public void LoadSettings()
        {
            _settings = App.GetSettings();

            // WindowStateManager에 설정 로드
            _windowStateManager.LoadFromSettings(_settings);

            // Load hotkey settings
            HotkeyEnabled = _settings.HotkeyEnabled;
            HotkeyKey = _settings.HotkeyKey;

            // Load pin mode (IsAlwaysOnTop) - 직접 필드 설정하여 Settings 재저장 방지
            _isAlwaysOnTop = _settings.IsAlwaysOnTop;
            IsTopmost = _settings.IsAlwaysOnTop; // 초기 TopMost 상태 설정
            OnPropertyChanged(nameof(IsAlwaysOnTop));

            // Load WebView zoom level
            _selectedZoomLevel = _settings.WebViewZoomLevel > 0 ? _settings.WebViewZoomLevel : 67;
            OnPropertyChanged(nameof(SelectedZoomLevel));

            // Load last selected map
            if (!string.IsNullOrEmpty(_settings.SelectedMapId))
            {
                var savedMap = AvailableMaps.FirstOrDefault(m => m.MapId == _settings.SelectedMapId);
                if (savedMap != null)
                {
                    SelectedMapInfo = savedMap;
                }
            }

            // Initialize window properties with normal mode
            var normalRect = _windowStateManager.NormalModeRect;
            CurrentWindowWidth = normalRect.Width;
            CurrentWindowHeight = normalRect.Height;

            Logger.SimpleLog($"[LoadSettings] Loading window size: {normalRect.Width}x{normalRect.Height}");
            Logger.SimpleLog($"[LoadSettings] CurrentWindowWidth={CurrentWindowWidth}, CurrentWindowHeight={CurrentWindowHeight}");

            // 저장된 위치가 있으면 사용, 없으면 화면 중앙 계산
            if (normalRect.Left >= 0 && normalRect.Top >= 0)
            {
                CurrentWindowLeft = normalRect.Left;
                CurrentWindowTop = normalRect.Top;
                Logger.SimpleLog($"[LoadSettings] Restored window position: ({normalRect.Left}, {normalRect.Top})");
            }
            else
            {
                // 화면 중앙 위치 계산
                CurrentWindowLeft = (SystemParameters.PrimaryScreenWidth - normalRect.Width) / 2;
                CurrentWindowTop = (SystemParameters.PrimaryScreenHeight - normalRect.Height) / 2;
                Logger.SimpleLog($"[LoadSettings] Using center screen position: ({CurrentWindowLeft}, {CurrentWindowTop})");
            }
        }

        private void InitializeCommands()
        {
            // Property change handlers
            PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(IsCompactMode):
                        OnCompactModeChanged();
                        break;
                    case nameof(CurrentMap):
                        OnMapChanged();
                        break;
                    case nameof(SelectedMapInfo):
                        OnSelectedMapInfoChanged();
                        break;
                    case nameof(HideWebElements):
                        OnHideWebElementsChanged();
                        break;
                    case nameof(CurrentWindowLeft):
                        // Logger.SimpleLog($"[PropertyChanged] CurrentWindowLeft changed to: {CurrentWindowLeft}, IsCompactMode={IsCompactMode}");
                        break;
                    case nameof(CurrentWindowTop):
                        // Logger.SimpleLog($"[PropertyChanged] CurrentWindowTop changed to: {CurrentWindowTop}, IsCompactMode={IsCompactMode}");
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
        private void ToggleCompactMode()
        {
            Logger.SimpleLog($"ToggleCompactMode called. Current state: {IsCompactMode}");
            IsCompactMode = !IsCompactMode;
        }

        [RelayCommand]
        private void TogglePinMode()
        {
            Logger.SimpleLog($"TogglePinMode called. Current state: {IsAlwaysOnTop}");
            IsAlwaysOnTop = !IsAlwaysOnTop;
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
            if (IsCompactMode)
            {
                _windowStateManager.UpdateCompactModeRect(CurrentWindowBounds);
                Logger.SimpleLog($"[SaveSettings] Saved Compact mode: {CurrentWindowBounds}");
            }
            else
            {
                _windowStateManager.UpdateNormalModeRect(CurrentWindowBounds);
                Logger.SimpleLog($"[SaveSettings] Saved Normal mode: {CurrentWindowBounds}");
            }

            // Persist to disk
            var settings = App.GetSettings();
            _windowStateManager.SaveToSettings(settings);
            App.SetSettings(settings);
            Settings.Save();
        }

        /// <summary>
        /// 설정 오버레이 토글
        /// </summary>
        [RelayCommand]
        private void ToggleSettings()
        {
            IsSettingsOpen = !IsSettingsOpen;
            Logger.SimpleLog($"[ToggleSettings] IsSettingsOpen: {IsSettingsOpen}");
        }

        /// <summary>
        /// 설정 오버레이 닫기
        /// </summary>
        [RelayCommand]
        private void CloseSettings()
        {
            IsSettingsOpen = false;
            Logger.SimpleLog("[CloseSettings] Settings closed");
        }
        #endregion

        #region Private Methods
        private void OnCompactModeChanged()
        {
            // Logger.SimpleLog($"Compact Mode changed to: {IsCompactMode}");

            if (IsCompactMode)
            {
                // 일반 모드 위치를 WindowStateManager에 즉시 저장 (이벤트 발생 전에 저장)
                _windowStateManager.UpdateNormalModeRect(CurrentWindowBounds);

                var settings = App.GetSettings();
                _windowStateManager.SaveToSettings(settings);
                App.SetSettings(settings);
                Settings.Save();

                // Logger.SimpleLog($"[OnCompactModeChanged] Saved Normal mode: {CurrentWindowBounds}");

                EnterCompactMode();
            }
            else
            {
                // Compact 모드 위치를 WindowStateManager에 즉시 저장
                _windowStateManager.UpdateCompactModeRect(CurrentWindowBounds);

                var settings = App.GetSettings();
                _windowStateManager.SaveToSettings(settings);
                App.SetSettings(settings);
                Settings.Save();

                // Logger.SimpleLog($"[OnCompactModeChanged] Saved Compact mode: {CurrentWindowBounds}");

                ExitCompactMode();
            }
        }

        private void OnMapChanged()
        {
            // CurrentMap이 null이어도 "default" 키로 처리되므로 early return 하지 않음
            string mapKey = string.IsNullOrEmpty(CurrentMap) ? "default" : CurrentMap;
            Logger.SimpleLog($"Map changed to: {mapKey}");

            // Compact 모드일 때는 창 크기/위치를 변경하지 않음 (맵별로 크기가 달라지지 않도록)
            if (IsCompactMode)
            {
                Logger.SimpleLog($"[OnMapChanged] Compact mode active - maintaining current window size and position");
                return;
            }
        }

        private void OnSelectedMapInfoChanged()
        {
            if (SelectedMapInfo != null)
            {
                Logger.SimpleLog($"[OnSelectedMapInfoChanged] SelectedMapInfo changed to: {SelectedMapInfo.MapId}");

                // 설정 저장
                var settings = App.GetSettings();
                settings.SelectedMapId = SelectedMapInfo.MapId;
                App.SetSettings(settings);
                Settings.Save();
            }
        }

        private void OnHideWebElementsChanged()
        {
            Logger.SimpleLog($"[OnHideWebElementsChanged] HideWebElements changed to: {HideWebElements}");

            // Compact 모드가 활성화되어 있으면 View에서 PropertyChanged 이벤트를 감지하여 재적용함
        }

        private void EnterCompactMode()
        {
            // Load Compact settings from WindowStateManager (모든 맵에서 동일한 Compact 사용)
            Rect compactRect = _windowStateManager.GetCompactModeRect();

            // Logger.SimpleLog($"[EnterCompactMode] Loaded Compact rect: {compactRect}");

            // Apply Compact size
            CurrentWindowWidth = compactRect.Width;
            CurrentWindowHeight = compactRect.Height;

            // Apply Compact position (use default if not set)
            if (compactRect.Left >= 0 && compactRect.Top >= 0)
            {
                CurrentWindowLeft = compactRect.Left;
                CurrentWindowTop = compactRect.Top;
            }
            else
            {
                // Default position: bottom right
                CurrentWindowLeft = SystemParameters.PrimaryScreenWidth - compactRect.Width - 20;
                CurrentWindowTop = SystemParameters.PrimaryScreenHeight - compactRect.Height - 80;
            }

            // Update window settings for Compact
            ResizeMode = ResizeMode.CanResize;
            MinWidth = 200;
            MinHeight = 150;
            IsTopmost = true;

            // Update UI visibility - 상단 바는 유지
            TopBarVisibility = Visibility.Visible;
        }

        private void ExitCompactMode()
        {
            // Load Normal mode settings from WindowStateManager
            var normalRect = _windowStateManager.NormalModeRect;
            Logger.SimpleLog($"[ExitCompactMode] Loaded Normal rect: {normalRect}");

            // Restore normal mode settings
            CurrentWindowWidth = normalRect.Width;
            CurrentWindowHeight = normalRect.Height;
            CurrentWindowLeft = normalRect.Left;
            CurrentWindowTop = normalRect.Top;

            // Restore window settings
            ResizeMode = ResizeMode.CanResize;
            MinWidth = 1000;
            MinHeight = 700;
            // Compact 모드 종료 시 핀 설정에 따라 TopMost 유지
            IsTopmost = IsAlwaysOnTop;

            // Restore UI visibility
            TopBarVisibility = Visibility.Visible;
        }


        /// <summary>
        /// View에서 창 위치/크기 변경 이벤트를 받아 즉시 저장
        /// WindowStateManager를 통해 Normal/Compact 모드 Rect를 분리 관리
        /// </summary>
        public void OnWindowBoundsChanged(object? sender, TanukiTarkovMap.Views.WindowBoundsChangedEventArgs e)
        {
            // Logger.SimpleLog($"[OnWindowBoundsChanged] Bounds={e.Bounds}, IsCompactMode={e.IsCompactMode}");

            // ViewModel 속성 업데이트 (바인딩용)
            CurrentWindowLeft = e.Bounds.Left;
            CurrentWindowTop = e.Bounds.Top;
            CurrentWindowWidth = e.Bounds.Width;
            CurrentWindowHeight = e.Bounds.Height;

            // WindowStateManager를 통해 즉시 저장 (타이머 없음)
            _windowStateManager.UpdateAndSave(e.Bounds, e.IsCompactMode);
        }

        /// <summary>
        /// IsCompactMode 변경 시 WindowControlButtonsVisibility 업데이트
        /// </summary>
        partial void OnIsCompactModeChanged(bool value)
        {
            OnPropertyChanged(nameof(WindowControlButtonsVisibility));
        }

        /// <summary>
        /// IsSettingsOpen 변경 시 관련 Visibility 속성 업데이트
        /// </summary>
        partial void OnIsSettingsOpenChanged(bool value)
        {
            OnPropertyChanged(nameof(SettingsOverlayVisibility));
            OnPropertyChanged(nameof(WebViewContainerVisibility));
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