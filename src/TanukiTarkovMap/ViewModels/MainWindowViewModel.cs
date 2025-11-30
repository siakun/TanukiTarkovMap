using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Generic;
using System.Windows;
using TanukiTarkovMap.Messages;
using TanukiTarkovMap.Models.Data;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Models.Utils;

/**
MainWindowViewModel - 메인 윈도우 상태 및 UI 로직 관리

Purpose: MainWindow의 모든 바인딩 데이터와 커맨드를 제공하며, 창 상태/맵 선택/설정을 관리

Core Functionality:
- 창 상태 관리: 위치, 크기, 항상 위, 리사이즈 모드
- 맵 선택: ComboBox 바인딩, MapEventService 이벤트 수신으로 자동 맵 전환
- 설정 오버레이: 설정 페이지 토글 및 Visibility 관리
- 브라우저 줌: 배율 선택 및 WebBrowserViewModel로 메시지 전송

Message Flow:
  MapEventService.MapChanged → OnMapEventReceived → ChangeMapCommand
  SelectedMapInfo 변경 → MapSelectionChangedMessage → WebBrowserViewModel
  WebBrowserViewModel → MapReceivedMessage/PilotConnectedMessage → 이 ViewModel

Dependencies:
- WindowBoundsService: 창 위치/크기 저장소
- WindowStateManager: Normal 모드 Rect 관리
- MapEventService: 게임 로그 기반 맵 변경 이벤트
- WeakReferenceMessenger: ViewModel 간 통신
*/
namespace TanukiTarkovMap.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject,
        IRecipient<MapReceivedMessage>,
        IRecipient<PilotConnectedMessage>
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
                    IsTopmost = value;

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

        /// <summary> 설정 오버레이 표시 여부 </summary>
        [ObservableProperty] public partial bool IsSettingsOpen { get; set; } = false;

        /// <summary> 설정 오버레이 Visibility (IsSettingsOpen과 연동) </summary>
        public Visibility SettingsOverlayVisibility => IsSettingsOpen ? Visibility.Visible : Visibility.Collapsed;

        /// <summary> Browser 컨테이너 Visibility (설정이 열리면 숨김) </summary>
        public Visibility BrowserContainerVisibility => IsSettingsOpen ? Visibility.Collapsed : Visibility.Visible;
        #endregion

        #region Settings Properties
        [ObservableProperty] public partial string CurrentMap { get; set; }
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

        #region Browser Zoom Properties
        /// <summary> 사용 가능한 Browser 배율 목록 </summary>
        public List<int> AvailableZoomLevels { get; } = new List<int> { 50, 67, 75, 80, 90, 100, 110, 125, 150, 175, 200 };

        private int _selectedZoomLevel = 67;
        /// <summary> 선택된 Browser 배율 (%) </summary>
        public int SelectedZoomLevel
        {
            get => _selectedZoomLevel;
            set
            {
                if (SetProperty(ref _selectedZoomLevel, value))
                {
                    // Settings에 저장
                    var settings = App.GetSettings();
                    settings.BrowserZoomLevel = value;
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

            // Messenger 등록 (WebBrowserViewModel로부터 메시지 수신)
            WeakReferenceMessenger.Default.RegisterAll(this);
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

            // Load Browser zoom level
            _selectedZoomLevel = _settings.BrowserZoomLevel > 0 ? _settings.BrowserZoomLevel : 67;
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
                    case nameof(CurrentMap):
                        OnMapChanged();
                        break;
                    case nameof(SelectedMapInfo):
                        OnSelectedMapInfoChanged();
                        // WebBrowserViewModel에 메시지 전송
                        WeakReferenceMessenger.Default.Send(new MapSelectionChangedMessage(SelectedMapInfo));
                        break;
                    case nameof(HideWebElements):
                        OnHideWebElementsChanged();
                        // WebBrowserViewModel에 메시지 전송
                        WeakReferenceMessenger.Default.Send(new HideWebElementsChangedMessage(HideWebElements));
                        break;
                    case nameof(SelectedZoomLevel):
                        // WebBrowserViewModel에 메시지 전송
                        WeakReferenceMessenger.Default.Send(new ZoomLevelChangedMessage(SelectedZoomLevel));
                        break;
                }
            };
        }

        #region Commands
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
            _windowStateManager.UpdateNormalModeRect(CurrentWindowBounds);
            Logger.SimpleLog($"[SaveSettings] Saved window bounds: {CurrentWindowBounds}");

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
        private void OnMapChanged()
        {
            // CurrentMap이 null이어도 "default" 키로 처리되므로 early return 하지 않음
            string mapKey = string.IsNullOrEmpty(CurrentMap) ? "default" : CurrentMap;
            Logger.SimpleLog($"Map changed to: {mapKey}");
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
        }

        /// <summary>
        /// View에서 창 위치/크기 변경 이벤트를 받아 즉시 저장
        /// </summary>
        public void OnWindowBoundsChanged(object? sender, TanukiTarkovMap.Views.WindowBoundsChangedEventArgs e)
        {
            // ViewModel 속성 업데이트 (바인딩용)
            CurrentWindowLeft = e.Bounds.Left;
            CurrentWindowTop = e.Bounds.Top;
            CurrentWindowWidth = e.Bounds.Width;
            CurrentWindowHeight = e.Bounds.Height;

            // WindowStateManager를 통해 즉시 저장
            _windowStateManager.UpdateNormalModeRect(e.Bounds);
            var settings = App.GetSettings();
            _windowStateManager.SaveToSettings(settings);
            App.SetSettings(settings);
            Settings.Save();
        }

        /// <summary>
        /// IsSettingsOpen 변경 시 관련 Visibility 속성 업데이트
        /// </summary>
        partial void OnIsSettingsOpenChanged(bool value)
        {
            OnPropertyChanged(nameof(SettingsOverlayVisibility));
            OnPropertyChanged(nameof(BrowserContainerVisibility));
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

        #region Messenger Handlers

        /// <summary>
        /// 맵 수신 메시지 핸들러 (WebBrowserViewModel → MainWindowViewModel)
        /// </summary>
        public void Receive(MapReceivedMessage message)
        {
            CurrentMap = message.Value;
            Logger.SimpleLog($"[MainWindowViewModel] MapReceived via Messenger: {message.Value}");
        }

        /// <summary>
        /// Pilot 연결 메시지 핸들러 (WebBrowserViewModel → MainWindowViewModel)
        /// </summary>
        public void Receive(PilotConnectedMessage message)
        {
            Logger.SimpleLog("[MainWindowViewModel] PilotConnected via Messenger");

            if (SelectedMapInfo == null)
            {
                // 맵이 선택되어 있지 않으면 기본 맵으로 이동
                var defaultMap = App.AvailableMaps.FirstOrDefault();
                if (defaultMap != null)
                {
                    SelectedMapInfo = defaultMap;
                    Logger.SimpleLog($"[MainWindowViewModel] Auto-navigating to default map: {defaultMap.DisplayName}");
                }
            }
            else
            {
                // 이미 선택된 맵이 있으면 해당 맵으로 네비게이션 메시지 전송
                WeakReferenceMessenger.Default.Send(new MapSelectionChangedMessage(SelectedMapInfo));
            }
        }

        #endregion
    }
}