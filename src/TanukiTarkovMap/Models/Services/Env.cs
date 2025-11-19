using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using TanukiTarkovMap.Models.Data;

namespace TanukiTarkovMap.Models.Services
{
    public static class Env
    {
        static Env()
        {
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TanukiTarkovMap.exe")
            );

            Version = versionInfo.FileVersion;
        }

        // first logs read on app start
        //public static bool InitialLogsRead { get; set; } = true;

        public static string Version = "0.0";

        public static string WebsiteUrl = "https://tarkov-market.com/pilot";

        private static string _gameFolder = null;
        public static string GameFolder
        {
            get
            {
                if (_gameFolder == null)
                {
                    RegistryKey key = Registry.LocalMachine.OpenSubKey(
                        "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\EscapeFromTarkov"
                    );
                    var installPath = key?.GetValue("InstallLocation")?.ToString();
                    key?.Dispose();

                    if (!String.IsNullOrEmpty(installPath))
                    {
                        _gameFolder = installPath;
                    }
                }

                return _gameFolder;
            }
            set { _gameFolder = value; }
        }

        public static string LogsFolder
        {
            get
            {
                if (string.IsNullOrEmpty(GameFolder))
                    return null;

                return Path.Combine(GameFolder, "Logs");
            }
        }

        private static string _screenshotsFolder;
        public static string ScreenshotsFolder
        {
            get
            {
                if (_screenshotsFolder == null)
                {
                    _screenshotsFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "Escape From Tarkov",
                        "Screenshots"
                    );
                }
                return _screenshotsFolder;
            }
            set { _screenshotsFolder = value; }
        }

        //===================== AppContext Settings ============================

        private static AppSettings _appSettings = null;

        public static void SetSettings(AppSettings settings, bool force = false)
        {
            if (force || !String.IsNullOrEmpty(settings.gameFolder))
            {
                Env.GameFolder = settings.gameFolder ?? null;
            }
            if (force || !String.IsNullOrEmpty(settings.screenshotsFolder))
            {
                Env.ScreenshotsFolder = settings.screenshotsFolder ?? null;
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
                _appSettings.gameFolder = Env.GameFolder;
                _appSettings.screenshotsFolder = Env.ScreenshotsFolder;
                return _appSettings;
            }

            // 설정이 없으면 경고 - 이는 Settings.Load()가 호출되지 않은 경우
            return new AppSettings()
            {
                gameFolder = Env.GameFolder,
                screenshotsFolder = Env.ScreenshotsFolder,
            };
        }

        public static void ResetSettings()
        {
            AppSettings settings = new AppSettings()
            {
                gameFolder = null,
                screenshotsFolder = null,
                // PiP 설정은 기본값으로 리셋
                pipEnabled = true,
                pipRememberPosition = true,
                normalWidth = 1400,
                normalHeight = 900,
                normalLeft = -1,
                normalTop = -1,
            };
            SetSettings(settings, true);
        }

        //===================== AppContext Settings ============================

        public static void RestartApp()
        {
            // WPF 애플리케이션 재시작
            string appPath = Process.GetCurrentProcess().MainModule.FileName;
            Process.Start(appPath);
            Application.Current.Shutdown();
        }
    }
}
