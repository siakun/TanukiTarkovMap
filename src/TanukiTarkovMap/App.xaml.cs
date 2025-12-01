using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CefSharp;
using CefSharp.Wpf;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using TanukiTarkovMap.Models.Data;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.Views;
using Velopack;
using Velopack.Sources;

namespace TanukiTarkovMap
{
    /// <summary> Interaction logic for App.xaml </summary>
    public partial class App : Application
    {
        private const string GitHubRepoUrl = "https://github.com/Sia819/TanukiTarkovMap";

        private TaskbarIcon? _trayIcon;
        private MainWindow? _mainWindow;
        private SplashWindow? _splashWindow;
        private bool _isExiting = false; // 중복 종료 방지 플래그

        //===================== Application Global State (from Env.cs) ============================

        static App()
        {
            // Velopack에서 버전 가져오기
            try
            {
                var updateManager = new Velopack.UpdateManager(
                    new Velopack.Sources.GithubSource("https://github.com/Sia819/TanukiTarkovMap", null, false));

                if (updateManager.IsInstalled && updateManager.CurrentVersion != null)
                {
                    Version = $"v{updateManager.CurrentVersion}";
                }
                else
                {
                    // 개발 모드 - exe 파일 생성일 사용
                    var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TanukiTarkovMap.exe");
                    if (File.Exists(exePath))
                    {
                        var fileInfo = new FileInfo(exePath);
                        Version = $"Dev ({fileInfo.CreationTime:yyyy-MM-dd})";
                    }
                }
            }
            catch
            {
                Version = "Dev";
            }
        }

        public static string Version { get; private set; } = "Dev";

        public static string WebsiteUrl { get; } = "https://tarkov-market.com/pilot";

        /// <summary>
        /// 사용 가능한 타르코프 맵 목록 (MapConfiguration으로 위임)
        /// </summary>
        public static List<MapInfo> AvailableMaps => MapConfiguration.AvailableMaps;

        private static string? _gameFolder = null;
        private static bool _gameFolderInitialized = false;

        public static string? GameFolder
        {
            get
            {
                if (!_gameFolderInitialized)
                {
                    _gameFolder = TarkovPathFinder.FindGameFolder();
                    _gameFolderInitialized = true;
                }

                return _gameFolder;
            }
            set
            {
                _gameFolder = value;
                _gameFolderInitialized = true;
            }
        }

        public static string? LogsFolder
        {
            get
            {
                return TarkovPathFinder.GetLogsFolder(GameFolder);
            }
        }

        private static string? _screenshotsFolder;
        private static bool _screenshotsFolderInitialized = false;

        public static string ScreenshotsFolder
        {
            get
            {
                if (!_screenshotsFolderInitialized)
                {
                    // 자동 탐지 시도
                    _screenshotsFolder = TarkovPathFinder.FindScreenshotsFolder();

                    // 찾지 못한 경우 기본 경로 사용
                    if (_screenshotsFolder == null)
                    {
                        _screenshotsFolder = TarkovPathFinder.GetDefaultScreenshotsFolder();
                    }

                    _screenshotsFolderInitialized = true;
                }
                return _screenshotsFolder;
            }
            set
            {
                _screenshotsFolder = value;
                _screenshotsFolderInitialized = true;
            }
        }

        // AppSettings 관리
        private static AppSettings? _appSettings = null;

        public static void SetSettings(AppSettings settings, bool force = false)
        {
            if (force || !string.IsNullOrEmpty(settings.GameFolder))
            {
                GameFolder = settings.GameFolder ?? null;
            }
            if (force || !string.IsNullOrEmpty(settings.ScreenshotsFolder))
            {
                ScreenshotsFolder = settings.ScreenshotsFolder ?? null;
            }

            // AppSettings 객체를 내부적으로 저장
            _appSettings = settings;
        }

        public static AppSettings GetSettings()
        {
            // 저장된 설정이 있으면 반환, 없으면 기본값으로 새로 생성
            if (_appSettings != null)
            {
                // 경로 정보는 현재 값으로 업데이트
                _appSettings.GameFolder = GameFolder;
                _appSettings.ScreenshotsFolder = ScreenshotsFolder;
                return _appSettings;
            }

            // 설정이 없으면 경고 - 이는 Settings.Load()가 호출되지 않은 경우
            return new AppSettings()
            {
                GameFolder = GameFolder,
                ScreenshotsFolder = ScreenshotsFolder,
            };
        }

        public static void ResetSettings()
        {
            AppSettings settings = new AppSettings()
            {
                GameFolder = null,
                ScreenshotsFolder = null,
                NormalWidth = 800,
                NormalHeight = 600,
                NormalLeft = -1,
                NormalTop = -1,
            };
            SetSettings(settings, true);
        }

        public static void RestartApp()
        {
            // WPF 애플리케이션 재시작
            string appPath = Process.GetCurrentProcess().MainModule!.FileName;
            Process.Start(appPath);
            Current.Shutdown();
        }

        //===================== End of Application Global State ============================

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            SetCulture();

            Logger.SimpleLog("=== Application_Startup Begin ===");

            try
            {
                // 1. 스플래시 창 먼저 표시
                Logger.SimpleLog("Creating SplashWindow...");
                _splashWindow = new SplashWindow();
                Logger.SimpleLog("Showing SplashWindow...");
                _splashWindow.Show();

                // 2. 업데이트 체크 (Velopack 설치 시에만)
                Logger.SimpleLog("Starting CheckForUpdatesAsync...");
                await CheckForUpdatesAsync();
                Logger.SimpleLog("CheckForUpdatesAsync completed.");

                // 3. CEF 초기화
                Logger.SimpleLog("Initializing CEF...");
                _splashWindow?.SetStatus("초기화 중...");
                InitializeCef();
                Logger.SimpleLog("CEF initialized.");

                // DI 컨테이너 초기화
                ServiceLocator.Initialize();

                // 애플리케이션 시작 로깅
                Logger.SimpleLog("=== Application Starting ===");
                Logger.SimpleLog($"Working Directory: {Environment.CurrentDirectory}");
                Logger.SimpleLog($"Executable Path: {System.Reflection.Assembly.GetExecutingAssembly().Location}");

                // 시스템 트레이 아이콘 생성
                Logger.SimpleLog("Creating tray icon...");
                CreateTrayIcon();

                // 설정 로드
                Logger.SimpleLog("Loading settings...");
                Settings.Load();

                // 서버 시작
                Logger.SimpleLog("Starting WebSocket server...");
                Server.Start();

                // 파일/로그 모니터링 시작 (스크린샷, 게임 로그 감시)
                Logger.SimpleLog("Starting file watchers...");
                Watcher.Start();

                // 프로그램 자동 정리 실행
                Logger.SimpleLog("Cleaning old log folders...");
                Models.FileSystem.GameSessionCleaner.CleanOldLogFolders();

                // 4. 스플래시 닫고 메인 창 표시
                Logger.SimpleLog("Closing splash window...");
                _splashWindow?.Close();
                _splashWindow = null;

                Logger.SimpleLog("Showing main window...");
                ShowMainWindow();
                Logger.SimpleLog("Main window shown successfully.");
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"ERROR in Application_Startup: {ex}");
                MessageBox.Show($"앱 시작 중 오류 발생:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                Logger.SimpleLog("=== Update Check Started ===");
                Logger.SimpleLog($"Update Source: {GitHubRepoUrl}");
                Logger.SimpleLog($"Current Version: {Version}");

                var updateManager = new UpdateManager(new GithubSource(GitHubRepoUrl, null, false));

                // Velopack으로 설치되지 않은 경우 (개발 모드) 스킵
                Logger.SimpleLog($"IsInstalled: {updateManager.IsInstalled}");
                if (!updateManager.IsInstalled)
                {
                    Logger.SimpleLog("Update Check: Skipped (Development mode - not installed via Velopack)");
                    _splashWindow?.SetStatus("시작하는 중...");
                    await Task.Delay(500); // 스플래시를 잠시 보여주기
                    return;
                }

                _splashWindow?.SetStatus("업데이트 확인 중...");
                Logger.SimpleLog("Update Check: Checking for updates from GitHub...");

                // 업데이트 확인
                var updateInfo = await updateManager.CheckForUpdatesAsync();

                if (updateInfo == null)
                {
                    Logger.SimpleLog("Update Check: Already up to date (no updates available)");
                    _splashWindow?.SetStatus("시작하는 중...");
                    return;
                }

                // 업데이트 발견
                var targetVersion = updateInfo.TargetFullRelease.Version.ToString();
                Logger.SimpleLog($"Update Available: {Version} → v{targetVersion}");
                Logger.SimpleLog($"Download URL: {updateInfo.TargetFullRelease.FileName}");

                // 다운로드
                _splashWindow?.SetStatus($"v{targetVersion} 다운로드 중...");
                Logger.SimpleLog($"Update Download: Starting download of v{targetVersion}...");

                await updateManager.DownloadUpdatesAsync(updateInfo, progress =>
                {
                    _splashWindow?.SetProgress(progress);
                });

                Logger.SimpleLog($"Update Download: Completed successfully");
                _splashWindow?.SetStatus("업데이트 적용 중...");
                _splashWindow?.SetProgress(100);

                // 업데이트 적용 및 재시작 (자동)
                Logger.SimpleLog($"Update Apply: Applying v{targetVersion} and restarting...");
                updateManager.ApplyUpdatesAndRestart(updateInfo);
            }
            catch (Exception ex)
            {
                // 업데이트 실패해도 앱은 정상 실행
                Logger.SimpleLog($"Update Check FAILED: {ex.Message}");
                Logger.SimpleLog($"Update Error Details: {ex}");
                _splashWindow?.SetStatus("시작하는 중...");
            }
        }

        private void CreateTrayIcon()
        {
            // WPF BitmapImage로 아이콘 로드
            var iconUri = new Uri("pack://application:,,,/Resources/korea.ico");
            var iconStream = Application.GetResourceStream(iconUri)?.Stream;

            _trayIcon = new TaskbarIcon
            {
                IconSource = new BitmapImage(iconUri),
                ToolTipText = "Tanuki Tarkov Map"
            };

            // 컨텍스트 메뉴 생성
            var contextMenu = new ContextMenu();

            // 메인 창 열기/숨기기
            var toggleWindowItem = new MenuItem { Header = "창 표시/숨기기" };
            toggleWindowItem.Click += (s, args) => ToggleMainWindow();
            contextMenu.Items.Add(toggleWindowItem);

            // 설정 열기
            var settingsItem = new MenuItem { Header = "설정" };
            settingsItem.Click += (s, args) => ShowSettings();
            contextMenu.Items.Add(settingsItem);

            contextMenu.Items.Add(new Separator());

            // Tarkov Market 웹사이트 열기
            var openWebItem = new MenuItem { Header = "Tarkov Market 열기" };
            openWebItem.Click += (s, args) =>
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = App.WebsiteUrl,
                    UseShellExecute = true
                });
            };
            contextMenu.Items.Add(openWebItem);

            contextMenu.Items.Add(new Separator());

            // 종료
            var exitItem = new MenuItem { Header = "종료" };
            exitItem.Click += (s, args) => ExitApplication();
            contextMenu.Items.Add(exitItem);

            _trayIcon.ContextMenu = contextMenu;

            // 트레이 아이콘 더블클릭 시 메인 창 토글
            _trayIcon.TrayMouseDoubleClick += (s, args) => ToggleMainWindow();
        }

        private void ShowMainWindow()
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow();
                _mainWindow.Closing += (s, e) =>
                {
                    // 창 닫기 시 숨기기만 하고 종료하지 않음
                    e.Cancel = true;
                    _mainWindow.Hide();
                };
            }

            _mainWindow.Show();
            _mainWindow.Activate();
            _mainWindow.WindowState = WindowState.Normal;
        }

        private void HideMainWindow()
        {
            _mainWindow?.Hide();
        }

        private void ToggleMainWindow()
        {
            if (_mainWindow == null || !_mainWindow.IsVisible)
            {
                ShowMainWindow();
            }
            else
            {
                HideMainWindow();
            }
        }

        private void ShowSettings()
        {
            ShowMainWindow();
            // MainWindow가 표시된 후 설정 탭으로 이동
            if (_mainWindow != null)
            {
                _mainWindow.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        // Settings_Click 메서드 호출
                        var settingsButton = _mainWindow.FindName("SettingsButton") as Button;
                        if (settingsButton != null)
                        {
                            settingsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        }
                    }
                    catch { }
                });
            }
        }

        private void ExitApplication()
        {
            if (_isExiting) return; // 이미 종료 중이면 중복 실행 방지
            _isExiting = true;

            Logger.SimpleLog("=== Application Exit Started ===");

            try
            {
                // 1. 서비스 정리
                Logger.SimpleLog("Stopping services...");
                ServiceLocator.GoonTrackerService.Dispose();
                Watcher.Stop();
                Server.Stop();

                // 2. UI 정리
                Logger.SimpleLog("Closing UI...");
                _mainWindow?.Close();
                _trayIcon?.Dispose();

                // 3. CEF 종료 (가장 중요 - 브라우저 서브프로세스 종료)
                Logger.SimpleLog("Shutting down CEF...");
                if (Cef.IsInitialized == true)
                {
                    Cef.Shutdown();
                }

                Logger.SimpleLog("=== Application Exit Completed ===");
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"Exit error: {ex.Message}");
            }

            Shutdown();
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            // ExitApplication에서 이미 처리되지 않은 경우만 처리
            if (!_isExiting)
            {
                Logger.SimpleLog("Application_Exit: Cleanup not done by ExitApplication");
                _trayIcon?.Dispose();

                // CEF 종료
                if (Cef.IsInitialized == true)
                {
                    Cef.Shutdown();
                }
            }
        }

        private static void SetCulture()
        {
            // 소수점을 점(.)으로 표시
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        }

        /// <summary>
        /// CEF (Chromium Embedded Framework) 초기화
        /// </summary>
        private static void InitializeCef()
        {
            // CEF가 이미 초기화되었는지 확인
            if (Cef.IsInitialized == true)
                return;

            var settings = new CefSettings
            {
                // 캐시 경로 설정
                CachePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TarkovClient",
                    "CefSharp",
                    "Cache"),

                // 로그 비활성화 (프로덕션용)
                LogSeverity = LogSeverity.Disable,

                // 언어 설정
                Locale = "ko",

                // GPU 가속 활성화
                // CefCommandLineArgs에서 설정
            };

            // GPU 가속 설정
            settings.CefCommandLineArgs.Add("enable-gpu");
            settings.CefCommandLineArgs.Add("enable-gpu-compositing");

            // 자동 재생 정책
            settings.CefCommandLineArgs.Add("autoplay-policy", "no-user-gesture-required");

            // CEF 초기화
            Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null);
        }
    }
}