using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.Views;

namespace TanukiTarkovMap
{
    /// <summary> Interaction logic for App.xaml </summary>
    public partial class App : Application
    {
        private TaskbarIcon? _trayIcon;
        private MainWindow? _mainWindow;
        private Mutex? _mutex;
        private bool _isExiting = false; // 중복 종료 방지 플래그

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            SetCulture();

            // 애플리케이션 시작 로깅
            Logger.SimpleLog("=== Application Starting ===");
            Logger.SimpleLog($"Working Directory: {Environment.CurrentDirectory}");
            Logger.SimpleLog($"Executable Path: {System.Reflection.Assembly.GetExecutingAssembly().Location}");

            // 중복 실행 방지
            bool createdNew;
            _mutex = new Mutex(true, "TanukiTarkovMapMutex", out createdNew);

            if (!createdNew)
            {
                Logger.SimpleLog("Application already running, exiting...");
                MessageBox.Show("Tanuki Tarkov Map이 이미 실행 중입니다.", "알림",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // 시스템 트레이 아이콘 생성
            Logger.SimpleLog("Creating tray icon...");
            CreateTrayIcon();

            // 설정 로드
            Logger.SimpleLog("Loading settings...");
            Settings.Load();

            // 서버 시작
            Logger.SimpleLog("Starting WebSocket server...");
            Server.Start();

            // 메인 창 표시 (항상 시작 시 메인 창을 표시)
            Logger.SimpleLog("Showing main window...");
            ShowMainWindow();
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
                    FileName = Env.WebsiteUrl,
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

            // 정리 작업
            _mainWindow?.Close();
            _trayIcon?.Dispose();
            Server.Stop();

            // 뮤텍스 해제
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch { }
            finally
            {
                _mutex?.Dispose();
                _mutex = null;
            }

            Shutdown();
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            // ExitApplication에서 이미 처리되지 않은 경우만 처리
            if (!_isExiting)
            {
                _trayIcon?.Dispose();

                try
                {
                    _mutex?.ReleaseMutex();
                }
                catch { }
                finally
                {
                    _mutex?.Dispose();
                }
            }
        }

        private static void SetCulture()
        {
            // 소수점을 점(.)으로 표시
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        }
    }
}