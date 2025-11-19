using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using TarkovClient.Constants;
using TarkovClient.Utils;

namespace TarkovClient;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    // Win32 API 선언 (윈도우 드래그용)
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_CAPTION = 0x2;

    private int _tabCounter = 1;
    private readonly Dictionary<TabItem, WebView2> _tabWebViews = new();
    private PipController _pipController;
    private System.Windows.Threading.DispatcherTimer _settingsSaveTimer;
    private TarkovClient.Utils.HotkeyManager _hotkeyManager;

    public MainWindow()
    {
        try
        {
            InitializeComponent();

            // 윈도우 로드 완료 후 초기화
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
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

        // PiP 컨트롤러 초기화
        _pipController = new PipController(this);

        // 창 크기/위치 변경 시 설정 저장을 위한 이벤트 핸들러 등록
        this.SizeChanged += MainWindow_SizeChanged;
        this.LocationChanged += MainWindow_LocationChanged;

        // 핫키 매니저 초기화
        InitializeHotkeyManager();
    }

    // 탭 시스템 초기화 및 첫 번째 탭 생성
    private async Task InitializeTabs()
    {
        try
        {
            // 첫 번째 탭 생성
            await CreateNewTab();

            // 전체 로딩 패널 숨김
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
        catch (Exception)
        {
            // 로딩 패널에 에러 메시지 표시
            LoadingPanel.Visibility = Visibility.Visible;

            var stackPanel = LoadingPanel.Child as System.Windows.Controls.StackPanel;

            if (stackPanel?.Children.Count > 1)
            {
                var errorText = stackPanel.Children[1] as System.Windows.Controls.TextBlock;
                if (errorText != null)
                {
                    errorText.Text = "탭 시스템 초기화 실패";
                }
            }
        }
    }

    // 새 탭 생성
    private async Task CreateNewTab()
    {
        try
        {
            // 새 TabItem 생성
            var newTab = new TabItem
            {
                Header = $"Tarkov Client {_tabCounter}",
                Background = System.Windows.Media.Brushes.Transparent,
            };

            // 새 WebView2 생성
            var webView = new WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.Transparent, // 투명 배경으로 설정
            };

            // 탭에 WebView2 추가
            newTab.Content = webView;

            // TabControl에 새 탭 추가
            TabContainer.Items.Add(newTab);
            _tabWebViews[newTab] = webView;

            // 새 탭 선택
            TabContainer.SelectedItem = newTab;

            // WebView2 초기화
            await InitializeWebView(webView, newTab);

            _tabCounter++;
        }
        catch (Exception)
        {
            // 에러 처리는 상위에서
        }
    }

    // WebView2 초기화
    private async Task InitializeWebView(WebView2 webView, TabItem tabItem)
    {
        try
        {
            // WebView2 데이터 폴더를 사용자 앱데이터 폴더로 설정
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TarkovClient",
                "WebView2"
            );

            var webView2Environment = await CoreWebView2Environment.CreateAsync(
                null,
                userDataFolder
            );
            await webView.EnsureCoreWebView2Async(webView2Environment);

            // WebView2 설정
            webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

            // CSP 우회 설정 추가
            webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
            webView.CoreWebView2.Settings.IsScriptEnabled = true;

            // 이벤트 핸들러 등록
            webView.NavigationCompleted += (sender, e) =>
                WebView_NavigationCompleted(sender, e, tabItem);
            webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            webView.CoreWebView2.DocumentTitleChanged += (sender, e) =>
                UpdateTabTitle(tabItem, webView.CoreWebView2.DocumentTitle);

            // 타르코프 마켓 파일럿 페이지 로드
            string pilotUrl = Env.WebsiteUrl;
            webView.Source = new Uri(pilotUrl);
        }
        catch (Exception)
        {
            // 에러 처리
        }
    }

    // TC 버튼 클릭 - 새 탭 생성
    private void NewTab_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 새로운 탭 생성
            _ = CreateNewTab();
        }
        catch (Exception) { }
    }

    // 설정 버튼 클릭
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 이미 설정 탭이 있는지 확인
            TabItem existingSettingsTab = null;
            foreach (TabItem tab in TabContainer.Items)
            {
                if (tab.Header?.ToString() == "설정")
                {
                    existingSettingsTab = tab;
                    break;
                }
            }

            if (existingSettingsTab != null)
            {
                // 이미 설정 탭이 있으면 해당 탭으로 이동
                TabContainer.SelectedItem = existingSettingsTab;
            }
            else
            {
                // 새 설정 탭 생성
                CreateSettingsTab();
            }
        }
        catch (Exception) { }
    }


    // 탭별 WebView2 네비게이션 완료 이벤트
    private void WebView_NavigationCompleted(
        object sender,
        CoreWebView2NavigationCompletedEventArgs e,
        TabItem tabItem
    )
    {
        var webView = sender as WebView2;

        if (e.IsSuccess)
        {
            /* WebSocket 서버에 WebView 준비 완료 알림 (첫 번째 탭에서만) */
            if (TabContainer.Items.IndexOf(tabItem) == 0 && Server.CanSend)
            {
                Server.SendConfiguration();
            }

            // 불필요한 UI 요소 제거 및 방향 표시기 추가
            _ = RemoveUnwantedElements(webView);
            _ = AddDirectionIndicators(webView);
        }
    }

    // 탭 닫기 버튼 클릭
    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as System.Windows.Controls.Button;
        var tabItem = button?.Tag as TabItem;

        /* 최소 1개 탭은 유지 */
        if (tabItem != null && TabContainer.Items.Count > 1)
        {
            CloseTab(tabItem);
        }
    }

    // 탭 닫기
    private void CloseTab(TabItem tabItem)
    {
        if (_tabWebViews.TryGetValue(tabItem, out var webView))
        {
            // WebView2 정리
            webView?.Dispose();
            _tabWebViews.Remove(tabItem);
        }

        // TabControl에서 탭 제거
        TabContainer.Items.Remove(tabItem);
    }

    // JavaScript에서 C#로 메시지 수신
    private void CoreWebView2_WebMessageReceived(
        object sender,
        CoreWebView2WebMessageReceivedEventArgs e
    )
    {
        try
        {
            string message = e.TryGetWebMessageAsString();

            // 빈 메시지 체크
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            // JSON 파싱을 위해 Newtonsoft.Json 사용
            var messageObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(message);

            string messageType = messageObj?.type?.ToString();

            switch (messageType)
            {
                case "pip-drag-start":
                    // Win32 API를 사용한 윈도우 드래그 (DragMove 대안)
                    try
                    {
                        // Win32 API를 사용하여 윈도우를 드래그 가능 상태로 만듦
                        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                        ReleaseCapture();
                        SendMessage(hwnd, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                    }
                    catch (Exception) { }
                    break;

                case "pip-exit":
                    // JavaScript에서 PiP 종료 요청
                    if (_pipController != null)
                    {
                        _pipController.HidePip();
                    }
                    else { }
                    break;

                case "pip-overlay-ready":
                    break;

                case "pip-toggle":
                    // JavaScript에서 PiP 토글 요청
                    if (_pipController != null)
                    {
                        _pipController.TogglePip();
                    }

                    break;

                case "save-map-settings":
                    // JavaScript에서 맵별 설정 저장 요청
                    try
                    {
                        string transform = messageObj?.transform?.ToString();

                        if (_pipController != null && !string.IsNullOrEmpty(transform))
                        {
                            var settings = Env.GetSettings();
                            string currentMap = _pipController.GetCurrentMap();

                            if (!string.IsNullOrEmpty(currentMap))
                            {
                                // 맵별 설정 초기화 (필요시)
                                if (settings.mapSettings == null)
                                {
                                    settings.mapSettings =
                                        new System.Collections.Generic.Dictionary<
                                            string,
                                            MapSetting
                                        >();
                                }

                                // 해당 맵 설정이 없으면 기본값으로 생성
                                if (!settings.mapSettings.ContainsKey(currentMap))
                                {
                                    settings.mapSettings[currentMap] = new MapSetting();
                                }

                                // transform 값과 현재 창 크기/위치 저장
                                var mapSetting = settings.mapSettings[currentMap];
                                mapSetting.transform = transform;
                                mapSetting.width = this.Width;
                                mapSetting.height = this.Height;
                                mapSetting.left = this.Left;
                                mapSetting.top = this.Top;

                                // 설정 저장
                                Env.SetSettings(settings);
                                Settings.Save();
                            }
                        }
                    }
                    catch (Exception) { }
                    break;

                default:
                    break;
            }
        }
        catch (Exception) { }
    }

    // 창 크기 변경 이벤트
    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 기존 타이머가 있으면 재시작
        ScheduleSettingsSave();
    }

    // 창 위치 변경 이벤트
    private void MainWindow_LocationChanged(object sender, EventArgs e)
    {
        // 기존 타이머가 있으면 재시작
        ScheduleSettingsSave();
    }

    // 설정 저장 스케줄링 (중복 방지)
    private void ScheduleSettingsSave()
    {
        // 기존 타이머 중지
        _settingsSaveTimer?.Stop();

        // 새 타이머 생성 또는 재사용
        if (_settingsSaveTimer == null)
        {
            _settingsSaveTimer = new System.Windows.Threading.DispatcherTimer();
            _settingsSaveTimer.Interval = TimeSpan.FromMilliseconds(500);
            _settingsSaveTimer.Tick += (s, args) =>
            {
                _settingsSaveTimer.Stop();

                // 모드별 설정 저장
                if (_pipController != null && _pipController.IsActive)
                {
                    // PiP 모드: 맵별 설정 저장 (창 크기/위치만, transform은 JavaScript에서 처리)
                    SavePipModeSettings();
                }
                else
                {
                    // 일반 모드: 일반 모드 설정 저장
                    SaveNormalModeSettings();
                }
            };
        }

        // 타이머 시작
        _settingsSaveTimer.Start();
    }

    // PiP 모드 설정 저장 (맵별)
    private void SavePipModeSettings()
    {
        try
        {
            if (_pipController == null)
            {
                return;
            }

            var settings = Env.GetSettings();
            string currentMap = _pipController.GetCurrentMap();

            if (string.IsNullOrEmpty(currentMap))
            {
                return;
            }

            // 맵별 설정 초기화 (필요시)
            if (settings.mapSettings == null)
            {
                settings.mapSettings = new System.Collections.Generic.Dictionary<
                    string,
                    MapSetting
                >();
            }

            // 해당 맵 설정이 없으면 기본값으로 생성
            if (!settings.mapSettings.ContainsKey(currentMap))
            {
                settings.mapSettings[currentMap] = new MapSetting();
            }

            // 현재 창 크기/위치를 맵별 설정에 저장 (transform은 JavaScript에서 처리)
            var mapSetting = settings.mapSettings[currentMap];
            mapSetting.width = this.Width;
            mapSetting.height = this.Height;
            mapSetting.left = this.Left;
            mapSetting.top = this.Top;

            // 설정 저장
            Env.SetSettings(settings);
            Settings.Save();
        }
        catch (Exception) { }
    }

    // 일반 모드 설정 저장
    private void SaveNormalModeSettings()
    {
        try
        {
            var settings = Env.GetSettings();

            // 현재 일반 모드일 때만 저장
            if (_pipController == null || !_pipController.IsActive)
            {
                settings.normalLeft = this.Left;
                settings.normalTop = this.Top;
                settings.normalWidth = this.Width;
                settings.normalHeight = this.Height;

                // 화면 비율 계산
                double aspectRatio = this.Width / this.Height;
                string aspectRatioFormatted = $"{aspectRatio:F3}";

                // 일반적인 비율 판별
                string aspectRatioName = GetAspectRatioName(aspectRatio);

                Env.SetSettings(settings);
                Settings.Save();
            }
        }
        catch (Exception) { }
    }

    // 화면 비율 이름 판별
    private string GetAspectRatioName(double aspectRatio)
    {
        // 허용 오차 범위
        const double tolerance = 0.05;

        // 일반적인 화면 비율들
        if (Math.Abs(aspectRatio - (16.0 / 9.0)) < tolerance)
            return "16:9";
        if (Math.Abs(aspectRatio - (16.0 / 10.0)) < tolerance)
            return "16:10";
        if (Math.Abs(aspectRatio - (4.0 / 3.0)) < tolerance)
            return "4:3";
        if (Math.Abs(aspectRatio - (21.0 / 9.0)) < tolerance)
            return "21:9 (Ultra-wide)";
        if (Math.Abs(aspectRatio - (32.0 / 9.0)) < tolerance)
            return "32:9 (Super Ultra-wide)";
        if (Math.Abs(aspectRatio - (3.0 / 2.0)) < tolerance)
            return "3:2";
        if (Math.Abs(aspectRatio - (5.0 / 4.0)) < tolerance)
            return "5:4";
        if (Math.Abs(aspectRatio - 1.0) < tolerance)
            return "1:1 (Square)";

        // 일반적이지 않은 비율
        return "Custom";
    }

    // PiP 드래그 관련 변수
    private bool _isDragging = false;
    private System.Windows.Point _lastMousePosition;

    // PiP 상단 드래그 영역 - 마우스 다운
    private void PipDragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            _isDragging = true;
            _lastMousePosition = e.GetPosition(this);

            // 마우스 캡처
            ((Border)sender).CaptureMouse();
        }
        catch (Exception) { }
    }

    // PiP 상단 드래그 영역 - 마우스 이동
    private void PipDragArea_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        try
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                System.Windows.Point currentPosition = e.GetPosition(this);

                // 이동 거리 계산
                double deltaX = currentPosition.X - _lastMousePosition.X;
                double deltaY = currentPosition.Y - _lastMousePosition.Y;

                // 창 위치 업데이트
                this.Left += deltaX;
                this.Top += deltaY;
            }
        }
        catch (Exception) { }
    }

    // PiP 상단 드래그 영역 - 마우스 업
    private void PipDragArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            _isDragging = false;

            // 마우스 캡처 해제
            ((Border)sender).ReleaseMouseCapture();

            // 위치 저장은 JavaScript에서 호버 해제 시 자동으로 처리됨
        }
        catch (Exception) { }
    }

    // PiP 하단 종료 영역 - 클릭
    private void PipExitArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            // PiP 해제 실행
            if (_pipController != null)
            {
                _pipController.HidePip();
            }
        }
        catch (Exception) { }
    }

    // 윈도우 종료 이벤트
    private void MainWindow_Closed(object sender, EventArgs e)
    {
        try
        {
            // PiP 창 정리
            _pipController?.HidePip();

            // 핫키 매니저 정리
            _hotkeyManager?.Dispose();

            // 모든 탭의 WebView2 정리
            foreach (var kvp in _tabWebViews)
            {
                var webView = kvp.Value;
                if (webView?.CoreWebView2 != null)
                {
                    webView.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
                }
                webView?.Dispose();
            }
            _tabWebViews.Clear();

            // 애플리케이션 종료
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception)
        {
            // 에러 처리
        }
    }

    // 현재 활성 WebView2 반환
    private WebView2 GetActiveWebView()
    {
        try
        {
            var selectedTabItem = this.TabContainer.SelectedItem as System.Windows.Controls.TabItem;
            if (selectedTabItem != null && _tabWebViews.ContainsKey(selectedTabItem))
            {
                return _tabWebViews[selectedTabItem];
            }

            // 첫 번째 WebView2 반환 (fallback)
            return _tabWebViews.Values.FirstOrDefault();
        }
        catch (Exception)
        {
            return null;
        }
    }

    // 설정 탭 생성
    private void CreateSettingsTab()
    {
        try
        {
            // 새 TabItem 생성
            var settingsTab = new TabItem
            {
                Header = "설정",
                Background = new SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(255, 26, 26, 26)
                ),
                Foreground = new SolidColorBrush(Colors.White),
            };

            // 설정 페이지 UserControl 생성 (나중에 구현)
            var settingsPage = new SettingsPage();
            settingsTab.Content = settingsPage;

            // TabContainer에 추가
            TabContainer.Items.Add(settingsTab);
            TabContainer.SelectedItem = settingsTab;
        }
        catch (Exception) { }
    }

    /// <summary>
    /// 핫키 매니저를 초기화합니다.
    /// </summary>
    private void InitializeHotkeyManager()
    {
        try
        {
            var settings = Env.GetSettings();

            // 핫키 기능이 비활성화되어 있으면 종료
            if (!settings.pipHotkeyEnabled)
            {
                return;
            }

            // 기존 핫키 매니저가 있으면 정리
            _hotkeyManager?.Dispose();

            // 새 핫키 매니저 생성
            _hotkeyManager = new TarkovClient.Utils.HotkeyManager(this);

            // 핫키 등록 (새 HotkeyManager가 UI 스레드에서 액션 실행)
            bool success = _hotkeyManager.RegisterHotkey(
                settings.pipHotkeyKey,
                () =>
                {
                    _pipController?.TogglePipWindowPosition();
                }
            );
        }
        catch (Exception) { }
    }

    /// <summary>
    /// 핫키 설정을 업데이트합니다 (설정 변경 시 호출)
    /// </summary>
    public void UpdateHotkeySettings()
    {
        InitializeHotkeyManager();
    }
}
