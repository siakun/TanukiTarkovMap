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
using CommunityToolkit.Mvvm.Messaging;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using Siakun.AutoUpdate;
using TanukiTarkovMap.Localization;
using TanukiTarkovMap.Messages;
using TanukiTarkovMap.Core.Data;
using TanukiTarkovMap.Core.FileSystem;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Core.Services;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.Core.Utils;
using TanukiTarkovMap.Views;
using TanukiTarkovMap.ViewModels;
using Velopack;
using Velopack.Sources;

namespace TanukiTarkovMap
{

    public partial class App : Application
    {
        private TaskbarIcon? _trayIcon;
        private MainWindow? _mainWindow;
        private SplashWindow? _splashWindow;
        private SplashWindowViewModel? _splashViewModel;
        private UpdateService? _updateService;
        private bool _isExiting = false;

        static App()
        {
            try
            {
                var updateManager = new Velopack.UpdateManager(
                    new Velopack.Sources.GithubSource(UpdateServiceFactory.RepositoryUrl, null, false));

                if (updateManager.IsInstalled && updateManager.CurrentVersion != null)
                {
                    Version = $"v{updateManager.CurrentVersion}";
                }
                else
                {
                    var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TanukiTarkovMap.exe");
                    if (File.Exists(exePath))
                    {
                        var fileInfo = new FileInfo(exePath);
                        Version = $"Dev ({fileInfo.LastWriteTime:yyyy-MM-dd})";
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

        public static string StartupUrl
        {
            get
            {
                return MapNavigation.InitialMap(GetSettings())?.Url ?? WebsiteUrl;
            }
        }

        public static List<MapInfo> AvailableMaps => MapConfiguration.AvailableMaps;

        public static string? GameFolder { get => Settings.GameFolder; set => Settings.GameFolder = value; }
        public static string? LogsFolder => Settings.LogsFolder;
        public static string ScreenshotsFolder { get => Settings.ScreenshotsFolder; set => Settings.ScreenshotsFolder = value; }
        public static void SetSettings(AppSettings settings, bool force = false) => Settings.Set(settings, force);
        public static AppSettings GetSettings() => Settings.Get();
        public static void ResetSettings() => Settings.Reset();

        public static void RestartApp()
        {
            string appPath = Process.GetCurrentProcess().MainModule!.FileName;
            Process.Start(appPath);
            Current.Shutdown();
        }

        private static readonly TimeSpan SplashDelay = TimeSpan.FromMilliseconds(500);

        private readonly Stopwatch _startupWatch = new();

        private void ShowSplashIfStartupIsSlow(string status)
        {
            if (_startupWatch.Elapsed < SplashDelay) return;

            if (_splashWindow == null)
            {
                Logger.SimpleLog($"Startup is slow ({_startupWatch.ElapsedMilliseconds}ms), showing splash");
                _splashViewModel = new SplashWindowViewModel();
                _splashWindow = new SplashWindow { DataContext = _splashViewModel };
                _splashWindow.Show();
            }

            _splashWindow.Dispatcher.Invoke(() => _splashViewModel!.Status = status);
        }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            _startupWatch.Start();
            SetCulture();

            Logger.SimpleLog("=== Application_Startup Begin ===");

            try
            {
                AppPaths.PrepareOnStartup();

                Logger.SimpleLog("Loading settings...");
                Settings.Load();
                ApplyLanguage();

                Logger.SimpleLog("Initializing CEF...");
                InitializeCef();
                Logger.SimpleLog("CEF initialized.");
                ShowSplashIfStartupIsSlow(Strings.Startup_Initializing);

                ServiceLocator.Initialize();
                _updateService = ServiceLocator.UpdateService;
                _updateService.RestartRequested += UpdateService_RestartRequested;

                Logger.SimpleLog("=== Application Starting ===");
                Logger.SimpleLog($"Working Directory: {Environment.CurrentDirectory}");
                Logger.SimpleLog($"Executable Path: {System.Reflection.Assembly.GetExecutingAssembly().Location}");

                Logger.SimpleLog("Creating tray icon...");
                CreateTrayIcon();

                ServiceLocator.GoonTrackerService.Enabled = GetSettings().GoonTrackerEnabled;

                ServiceLocator.RaidShareService.Configure(RaidShareSettings.From(GetSettings()));

                Logger.SimpleLog("Starting file watchers...");
                ShowSplashIfStartupIsSlow(Strings.Startup_StartingWatchers);
                ServiceLocator.ScreenshotsWatcher.Start();
                ServiceLocator.LogsWatcher.Start();

                Logger.SimpleLog("Cleaning old log folders...");
                GameSessionCleaner.CleanOldLogFolders();

                if (_splashWindow != null)
                {
                    Logger.SimpleLog($"Closing splash window (startup took {_startupWatch.ElapsedMilliseconds}ms)");
                    _splashWindow.Close();
                    _splashWindow = null;
                    _splashViewModel = null;
                }

                Logger.SimpleLog("Showing main window...");
                ShowMainWindow();
                Logger.SimpleLog("Main window shown successfully.");

                _ = ServiceLocator.UpdateService.CheckAndDownloadAsync();
            }
            catch (Exception ex)
            {
                _splashWindow?.Close();
                _splashWindow = null;
                _splashViewModel = null;

                Logger.SimpleLog($"ERROR in Application_Startup: {ex}");
                MessageBox.Show(string.Format(Strings.Startup_ErrorMessage, ex.Message), Strings.Startup_ErrorTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private static void ApplyLanguage()
        {
            try
            {
                AppLanguage.Apply(GetSettings().Language);
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[AppLanguage] Failed to apply language: {ex}");
            }
        }

        private void CreateTrayIcon()
        {
            var iconUri = new Uri("pack://application:,,,/Resources/icon.ico");
            var iconStream = Application.GetResourceStream(iconUri)?.Stream;

            _trayIcon = new TaskbarIcon
            {
                IconSource = new BitmapImage(iconUri),
                ToolTipText = "Tanuki Tarkov Map",
                ContextMenu = CreateTrayMenu()
            };

            WeakReferenceMessenger.Default.Register<App, LanguageChangedMessage>(this,
                static (app, _) => { if (app._trayIcon != null) app._trayIcon.ContextMenu = app.CreateTrayMenu(); });

            _trayIcon.TrayMouseDoubleClick += (s, args) => ToggleMainWindow();
        }

        private ContextMenu CreateTrayMenu()
        {
            var contextMenu = new ContextMenu { Language = AppLanguage.CurrentXmlLanguage };

            var toggleWindowItem = new MenuItem { Header = Strings.Tray_ToggleWindow };
            toggleWindowItem.Click += (s, args) => ToggleMainWindow();
            contextMenu.Items.Add(toggleWindowItem);

            var settingsItem = new MenuItem { Header = Strings.Common_Settings };
            settingsItem.Click += (s, args) => ShowSettings();
            contextMenu.Items.Add(settingsItem);

            contextMenu.Items.Add(new Separator());

            var openWebItem = new MenuItem { Header = Strings.Tray_OpenTarkovMarket };
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

            var exitItem = new MenuItem { Header = Strings.Tray_Exit };
            exitItem.Click += (s, args) => ExitApplication();
            contextMenu.Items.Add(exitItem);

            return contextMenu;
        }

        private void ShowMainWindow()
        {
            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow();
                _mainWindow.Closing += (s, e) =>
                {
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
            if (_mainWindow?.DataContext is ViewModels.MainWindowViewModel viewModel)
            {
                viewModel.IsSettingsOpen = true;
            }
        }

        private void ExitApplication()
        {
            if (_isExiting) return;
            _isExiting = true;

            Logger.SimpleLog("=== Application Exit Started ===");

            try
            {
                Logger.SimpleLog("Stopping services...");
                var cleanupTasks = new List<Task>
                {
                    Task.Run(() => ServiceLocator.GoonTrackerService.Dispose()),
                    Task.Run(() => ServiceLocator.RaidShareService.Dispose()),
                    Task.Run(() => ServiceLocator.HotkeyService.Dispose()),
                    Task.Run(() => { ServiceLocator.ScreenshotsWatcher.Stop(); ServiceLocator.LogsWatcher.Stop(); }),
                };
                Task.WaitAll(cleanupTasks.ToArray(), 300);
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"Service cleanup error: {ex.Message}");
            }

            try
            {
                Logger.SimpleLog("Closing UI...");
                _mainWindow?.Close();
                _trayIcon?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"UI cleanup error: {ex.Message}");
            }

            try
            {
                Logger.SimpleLog("Shutting down CEF...");
                if (Cef.IsInitialized == true)
                {
                    Cef.Shutdown();
                }
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"CEF shutdown error: {ex.Message}");
            }

            try
            {
                AppPaths.DeleteBrowserCacheIfRequested();
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"Browser cache cleanup error: {ex.Message}");
            }

            _updateService?.ApplyOnExit();

            CleanupVelopackLog();

            Logger.SimpleLog("=== Application Exit Completed ===");

            Shutdown();
        }

        private void UpdateService_RestartRequested(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(ExitApplication);
        }

        private void CleanupVelopackLog()
        {
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "velopack.log");
                if (File.Exists(logPath))
                {
                    File.Delete(logPath);
                }
            }
            catch { }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (!_isExiting)
            {
                Logger.SimpleLog("Application_Exit: Cleanup not done by ExitApplication");
                try
                {
                    _trayIcon?.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.SimpleLog($"Application_Exit UI cleanup error: {ex.Message}");
                }

                try
                {
                    if (Cef.IsInitialized == true)
                    {
                        Cef.Shutdown();
                    }
                }
                catch (Exception ex)
                {
                    Logger.SimpleLog($"Application_Exit CEF shutdown error: {ex.Message}");
                }

                try
                {
                    AppPaths.DeleteBrowserCacheIfRequested();
                }
                catch (Exception ex)
                {
                    Logger.SimpleLog($"Application_Exit browser cache cleanup error: {ex.Message}");
                }

                _updateService?.ApplyOnExit();
            }
        }

        private static void SetCulture()
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        }

        private static void InitializeCef()
        {
            if (Cef.IsInitialized == true)
                return;

            var settings = new CefSettings
            {
                CachePath = AppPaths.BrowserCacheFolder,

                LogSeverity = LogSeverity.Disable,

                Locale = "ko",

            };

            settings.CefCommandLineArgs.Add("enable-gpu");
            settings.CefCommandLineArgs.Add("enable-gpu-compositing");

            settings.CefCommandLineArgs.Add("autoplay-policy", "no-user-gesture-required");

#if DEBUG
            settings.RemoteDebuggingPort = 9222;
#endif

            Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null);
        }
    }
}
