using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using TanukiTarkovMap.Models.Constants;
using TanukiTarkovMap.Models.Data;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.ViewModels;

namespace TanukiTarkovMap.Views
{
    /// <summary>
    /// 창 위치/크기 변경 이벤트 인자
    /// </summary>
    public class WindowBoundsChangedEventArgs : EventArgs
    {
        public Rect Bounds { get; set; }
        public bool IsPipMode { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // 창 위치/크기 변경 이벤트
        public event EventHandler<WindowBoundsChangedEventArgs>? WindowBoundsChanged;

        private int _tabCounter = 1;
        private readonly Dictionary<TabItem, WebView2> _tabWebViews = new();
        private MainWindowViewModel _viewModel;
        private PipService _pipService;
        private WindowBoundsService _windowBoundsService;
        private HotkeyManager _hotkeyManager;
        private bool _isClampingLocation = false; // 무한 루프 방지

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                // 서비스 초기화
                _pipService = new PipService();
                _windowBoundsService = new WindowBoundsService();

                // ViewModel 초기화 (서비스 주입)
                _viewModel = new MainWindowViewModel(_pipService, _windowBoundsService);
                DataContext = _viewModel;

                // 윈도우 로드 완료 후 초기화
                Loaded += MainWindow_Loaded;
                Closed += MainWindow_Closed;
                LocationChanged += MainWindow_LocationChanged;
                SizeChanged += MainWindow_SizeChanged;

                // ViewModel에 창 위치/크기 변경 이벤트 연결
                this.WindowBoundsChanged += _viewModel.OnWindowBoundsChanged;

                // 키보드 이벤트 핸들러 추가 (디버그 모드용)
                this.PreviewKeyDown += MainWindow_PreviewKeyDown;
            }
            catch (Exception)
            {
                throw;
            }
        }

        // 탭 제목 업데이트
        private static void UpdateTabTitle(TabItem tabItem, string title)
        {
            if (!string.IsNullOrEmpty(title))
            {
                // "Tarkov Pilot"를 "Tarkov Client"로 변경
                string displayTitle = title.Replace("Tarkov Pilot", "Tarkov Client");
                tabItem.Header =
                    displayTitle.Length > 20 ? displayTitle.Substring(0, 20) + "..." : displayTitle;
            }
        }

        // Tarkov Market Map 방향 표시기 추가 (탭별)
        private static async Task AddDirectionIndicators(WebView2 webView)
        {
            try
            {
                await Task.Delay(2000); // 페이지 로딩 완료 대기
                await webView.CoreWebView2.ExecuteScriptAsync(
                    JavaScriptConstants.ADD_DIRECTION_INDICATORS_SCRIPT
                );
            }
            catch (Exception)
            {
                // 에러 처리
            }
        }

        // 불필요한 UI 요소 제거 (탭별)
        private static async Task RemoveUnwantedElements(WebView2 webView)
        {
            try
            {
                await webView.CoreWebView2.ExecuteScriptAsync(
                    JavaScriptConstants.REMOVE_UNWANTED_ELEMENTS_SCRIPT
                );
            }
            catch (Exception)
            {
                // 에러 처리
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 로딩 패널 강제 숨김 (디버그용)
            LoadingPanel.Visibility = Visibility.Collapsed;

            await InitializeTabs();

            // ViewModel이 PIP 모드 변경을 처리하도록 PropertyChanged 구독
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // 핫키 매니저 초기화 (전역 단축키용)
            InitializeHotkeyManager();
        }

        /// <summary>
        /// ViewModel 프로퍼티 변경 처리
        /// </summary>
        private async void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.IsPipMode):
                    await HandlePipModeChanged();
                    break;
                case nameof(MainWindowViewModel.CurrentMap):
                    await HandleMapChanged();
                    break;
            }
        }

        /// <summary>
        /// PIP 모드 변경 처리
        /// </summary>
        private async Task HandlePipModeChanged()
        {
            if (_viewModel.IsPipMode)
            {
                // PIP 모드 시작 시 현재 화면 저장 (LocationChanged에서 경계 체크에 사용)
                var windowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                _windowBoundsService.SavePipModeScreen(windowHandle);

                // 창 위치를 화면 내부로 보정
                var dpiInfo = VisualTreeHelper.GetDpi(this);
                var validatedPosition = _windowBoundsService.EnsureWindowWithinScreen(
                    _viewModel.WindowLeft,
                    _viewModel.WindowTop,
                    _viewModel.WindowWidth,
                    _viewModel.WindowHeight,
                    dpiInfo.DpiScaleX,
                    dpiInfo.DpiScaleY
                );

                // 검증된 위치 반영
                _viewModel.WindowLeft = validatedPosition.X;
                _viewModel.WindowTop = validatedPosition.Y;

                Logger.SimpleLog($"[PIP Entry] Position validated: ({validatedPosition.X}, {validatedPosition.Y})");

                // PIP 모드 진입 시 JavaScript 적용
                var activeWebView = GetActiveWebView();
                if (activeWebView != null)
                {
                    await _pipService.ApplyPipModeJavaScriptAsync(activeWebView, _viewModel.CurrentMap);
                }

                // Topmost 설정 (Win32 API)
                WindowTopmost.SetTopmost(this);
            }
            else
            {
                // PIP 모드 종료 시 화면 정보 초기화
                _windowBoundsService.ClearPipModeScreen();

                // 일반 모드 복원 시 JavaScript 복원
                var activeWebView = GetActiveWebView();
                if (activeWebView != null)
                {
                    await _pipService.RestoreNormalModeJavaScriptAsync(activeWebView);
                }

                // Topmost 해제 (Win32 API)
                WindowTopmost.RemoveTopmost(this);
            }
        }

        /// <summary>
        /// 맵 변경 처리
        /// </summary>
        private async Task HandleMapChanged()
        {
            if (_viewModel.IsPipMode && !string.IsNullOrEmpty(_viewModel.CurrentMap))
            {
                var activeWebView = GetActiveWebView();
                if (activeWebView != null)
                {
                    await _pipService.ApplyPipModeJavaScriptAsync(activeWebView, _viewModel.CurrentMap);
                }
            }
        }

        /// <summary>
        /// MainWindow 내에서의 키 입력 처리 (디버그 모드용)
        /// </summary>
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // 설정에서 핫키가 활성화되어 있고, 해당 키가 눌렸을 때
                if (_viewModel.PipHotkeyEnabled)
                {
                    // F11 또는 설정된 키 확인
                    if ((_viewModel.PipHotkeyKey == "F11" && e.Key == Key.F11) ||
                        (_viewModel.PipHotkeyKey == "Home" && e.Key == Key.Home) ||
                        (_viewModel.PipHotkeyKey == "F12" && e.Key == Key.F12) ||
                        (_viewModel.PipHotkeyKey == "F10" && e.Key == Key.F10) ||
                        (_viewModel.PipHotkeyKey == "F9" && e.Key == Key.F9))
                    {
                        Logger.SimpleLog($"MainWindow KeyDown detected: {e.Key}");

                        // PIP 모드 토글 Command 실행
                        _viewModel.TogglePipModeCommand.Execute(null);

                        // 이벤트 처리 완료 표시
                        e.Handled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("MainWindow_PreviewKeyDown error", ex);
            }
        }

        // 탭 시스템 초기화 및 첫 번째 탭 생성
        private async Task InitializeTabs()
        {
            // 첫 번째 탭 추가 (URL은 App.WebsiteUrl 사용)
            await AddNewTab(App.WebsiteUrl);
        }

        private async Task AddNewTab(string url = null)
        {
            url ??= App.WebsiteUrl;

            // 새 탭 생성
            var tabItem = new TabItem
            {
                Header = $"Tarkov Client {_tabCounter++}",
                Foreground = new SolidColorBrush(Colors.White)
            };

            // WebView2 생성
            var webView = new WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(26, 26, 26)
            };

            // 탭 컨텐츠 설정 (Visual Tree에 먼저 추가)
            tabItem.Content = webView;

            // 탭 추가 및 활성화 (WebView2가 렌더링될 수 있도록)
            _tabWebViews[tabItem] = webView;
            TabContainer.Items.Add(tabItem);
            TabContainer.SelectedItem = tabItem;

            // WebView2 초기화 (Visual Tree에 추가된 후)
            await InitializeWebView2(webView, tabItem);

            // URL 로드
            webView.Source = new Uri(url);
        }

        private async Task InitializeWebView2(WebView2 webView, TabItem tabItem)
        {
            try
            {
                Logger.SimpleLog("InitializeWebView2: Start");

                // UserDataFolder 설정 (각 WebView2 인스턴스가 동일한 폴더 공유)
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TarkovClient",
                    "WebView2"
                );
                Logger.SimpleLog($"InitializeWebView2: UserDataFolder = {userDataFolder}");

                // CoreWebView2 환경 생성
                Logger.SimpleLog("InitializeWebView2: Creating CoreWebView2Environment");
                var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                Logger.SimpleLog("InitializeWebView2: Environment created");

                // CoreWebView2 초기화
                Logger.SimpleLog("InitializeWebView2: Calling EnsureCoreWebView2Async");
                await webView.EnsureCoreWebView2Async(environment);
                Logger.SimpleLog("InitializeWebView2: CoreWebView2 initialized");

                // WebView2 설정
                ConfigureWebView2Settings(webView);
                Logger.SimpleLog("InitializeWebView2: Settings configured");

                // 이벤트 핸들러 등록
                webView.NavigationCompleted += (s, e) => WebView_NavigationCompleted(s, e, tabItem);
                Logger.SimpleLog("InitializeWebView2: Event handlers registered");
            }
            catch (Exception ex)
            {
                Logger.Error("InitializeWebView2 failed", ex);
                throw;
            }
        }

        // WebView2 설정 구성
        private static void ConfigureWebView2Settings(WebView2 webView)
        {
            var settings = webView.CoreWebView2.Settings;

            settings.IsScriptEnabled = true;
            settings.AreDefaultScriptDialogsEnabled = false;
            settings.IsWebMessageEnabled = true;
            settings.AreDevToolsEnabled = false;
            settings.AreDefaultContextMenusEnabled = false;

            settings.IsZoomControlEnabled = true;
            settings.IsPasswordAutosaveEnabled = false;
            settings.IsGeneralAutofillEnabled = false;
        }

        // 페이지 로딩 완료 시 처리 (탭별)
        private async void WebView_NavigationCompleted(
            object sender,
            CoreWebView2NavigationCompletedEventArgs e,
            TabItem tabItem
        )
        {
            if (!e.IsSuccess)
                return;

            var webView = sender as WebView2;
            if (webView == null)
                return;

            // 페이지 제목 가져오기 및 업데이트
            var title = await webView.CoreWebView2.ExecuteScriptAsync("document.title");
            UpdateTabTitle(tabItem, title?.Trim('"'));

            // WebSocket 통신을 위한 메시지 핸들러 등록
            webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            // 기본 작업들
            await RemoveUnwantedElements(webView);

            // Tarkov Market 전용 처리
            if (webView.Source?.ToString().Contains("tarkov-market.com") == true)
            {
                // 방향 표시기 추가
                await AddDirectionIndicators(webView);

                // PIP 모드 상태면 JavaScript 적용
                if (_viewModel.IsPipMode)
                {
                    await _pipService.ApplyPipModeJavaScriptAsync(webView, _viewModel.CurrentMap);
                }
            }
        }

        // WebSocket 메시지 수신 처리 (맵 정보 수신)
        private void CoreWebView2_WebMessageReceived(
            object sender,
            CoreWebView2WebMessageReceivedEventArgs e
        )
        {
            try
            {
                var message = e.TryGetWebMessageAsString();
                if (!string.IsNullOrEmpty(message))
                {
                    // 맵 정보 파싱 (예: "map:customs_preset")
                    if (message.StartsWith("map:"))
                    {
                        var mapName = message.Substring(4);
                        Logger.SimpleLog($"Map received from WebSocket: {mapName}");

                        // ViewModel의 CurrentMap 업데이트
                        Dispatcher.Invoke(() =>
                        {
                            _viewModel.CurrentMap = mapName;
                        });
                    }
                }
            }
            catch (Exception)
            {
                // 에러 처리
            }
        }

        // 새 탭 추가 버튼 클릭
        private async void NewTab_Click(object sender, RoutedEventArgs e)
        {
            await AddNewTab();
        }

        // 탭 닫기 버튼 클릭
        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.Tag is not TabItem tabItem)
                return;

            // 마지막 탭이면 닫지 않음
            if (TabContainer.Items.Count <= 1)
                return;

            // WebView2 정리
            if (_tabWebViews.TryGetValue(tabItem, out var webView))
            {
                webView?.Dispose();
                _tabWebViews.Remove(tabItem);
            }

            // 탭 제거
            TabContainer.Items.Remove(tabItem);
        }

        // 설정 버튼 클릭
        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            // 설정 페이지를 모달로 표시
            var settingsWindow = new Window
            {
                Title = "설정",
                Width = 800,
                Height = 600,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            var settingsPage = new SettingsPage();
            settingsWindow.Content = settingsPage;
            settingsWindow.ShowDialog();
        }

        // 핫키 매니저 초기화 (전역 단축키용)
        private void InitializeHotkeyManager()
        {
            try
            {
                _hotkeyManager = new HotkeyManager(this);

                // PIP 핫키 등록
                if (_viewModel.PipHotkeyEnabled && !string.IsNullOrEmpty(_viewModel.PipHotkeyKey))
                {
                    _hotkeyManager.RegisterHotkey(_viewModel.PipHotkeyKey, () =>
                    {
                        Logger.SimpleLog("Global hotkey triggered");
                        Dispatcher.Invoke(() =>
                        {
                            _viewModel.TogglePipModeCommand.Execute(null);
                        });
                    });

                    Logger.SimpleLog($"Hotkey registered: {_viewModel.PipHotkeyKey}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize HotkeyManager", ex);
            }
        }

        // 핫키 설정 업데이트 (SettingsPage에서 호출)
        public void UpdateHotkeySettings()
        {
            try
            {
                // 기존 핫키 매니저 정리
                _hotkeyManager?.Dispose();

                // ViewModel의 설정 다시 로드
                _viewModel.LoadSettings();

                // 핫키 매니저 재초기화
                InitializeHotkeyManager();

                Logger.SimpleLog("Hotkey settings updated");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to update hotkey settings", ex);
            }
        }

        // 현재 활성 WebView2 가져오기
        private WebView2 GetActiveWebView()
        {
            if (TabContainer.SelectedItem is TabItem selectedTab)
            {
                if (_tabWebViews.TryGetValue(selectedTab, out var webView))
                {
                    return webView;
                }
            }
            return null;
        }

        /// <summary>
        /// 창 위치 변경 시 ViewModel 즉시 업데이트 및 PIP 모드에서 화면 경계 체크
        /// </summary>
        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            if (_isClampingLocation) return;        // 무한 루프 방지

            try
            {
                _isClampingLocation = true;

                // PIP 모드에서는 화면 경계 체크 수행
                if (_viewModel.IsPipMode)
                {
                    var dpiScale = VisualTreeHelper.GetDpi(this);

                    // WindowBoundsService를 사용하여 창 위치 체크 및 조정
                    var newPosition = _windowBoundsService.ClampWindowPosition(
                        this.Left,
                        this.Top,
                        this.ActualWidth,
                        this.ActualHeight,
                        dpiScale.DpiScaleX,
                        dpiScale.DpiScaleY
                    );

                    // 조정이 필요한 경우 위치 업데이트
                    if (newPosition.HasValue)
                    {
                        this.Left = newPosition.Value.X;
                        this.Top = newPosition.Value.Y;
                    }
                }

                // 창 위치/크기 변경 이벤트 발생 (ViewModel에서 즉시 저장)
                WindowBoundsChanged?.Invoke(this, new WindowBoundsChangedEventArgs
                {
                    Bounds = new Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight),
                    IsPipMode = _viewModel.IsPipMode
                });
            }
            finally
            {
                _isClampingLocation = false;
            }
        }

        /// <summary>
        /// 창 크기 변경 시 이벤트 발생
        /// </summary>
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isClampingLocation) return;        // 무한 루프 방지

            // 창 위치/크기 변경 이벤트 발생 (ViewModel에서 즉시 저장)
            WindowBoundsChanged?.Invoke(this, new WindowBoundsChangedEventArgs
            {
                Bounds = new Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight),
                IsPipMode = _viewModel.IsPipMode
            });
        }

        // PIP 모드에서 창 드래그 이동 처리
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // PIP 모드일 때만 드래그 가능
            if (_viewModel.IsPipMode && e.ButtonState == MouseButtonState.Pressed)
            {
                // WebView2 영역 외부를 클릭한 경우만 드래그
                // (WebView2 내부에서는 맵 상호작용을 위해 드래그 비활성화)
                var position = e.GetPosition(this);
                var hitElement = this.InputHitTest(position) as DependencyObject;

                // WebView2가 아닌 경우에만 드래그 허용
                bool isWebView2 = false;
                while (hitElement != null)
                {
                    if (hitElement is WebView2)
                    {
                        isWebView2 = true;
                        break;
                    }
                    hitElement = System.Windows.Media.VisualTreeHelper.GetParent(hitElement);
                }

                if (!isWebView2)
                {
                    PInvoke.ReleaseCapture();
                    PInvoke.SendMessage(new System.Windows.Interop.WindowInteropHelper(this).Handle,
                               PInvoke.WM_NCLBUTTONDOWN, PInvoke.HT_CAPTION, 0);
                }
            }
        }

        // 창 닫기 시 정리
        private void MainWindow_Closed(object sender, EventArgs e)
        {
            // ViewModel 이벤트 구독 해제
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            // 핫키 매니저 정리
            _hotkeyManager?.Dispose();

            // WebView2 정리
            foreach (var webView in _tabWebViews.Values)
            {
                webView?.Dispose();
            }
            _tabWebViews.Clear();
        }
    }
}