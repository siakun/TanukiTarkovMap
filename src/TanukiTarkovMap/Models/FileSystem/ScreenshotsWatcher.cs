using System.IO;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Models.FileSystem
{
    public static class ScreenshotsWatcher
    {
        static FileSystemWatcher screenshotsWatcher;

        public static void Start()
        {
            if (!Directory.Exists(App.ScreenshotsFolder))
            {
                return;
            }

            screenshotsWatcher = new FileSystemWatcher(App.ScreenshotsFolder);
            screenshotsWatcher.Created += OnScreenshot;
            screenshotsWatcher.EnableRaisingEvents = true;
        }

        public static void Stop()
        {
            if (screenshotsWatcher != null)
            {
                screenshotsWatcher.Created -= OnScreenshot;
                screenshotsWatcher.Dispose();
                screenshotsWatcher = null;
            }
        }

        public static void Restart()
        {
            Stop();
            Start();
        }

        static void OnScreenshot(object sender, FileSystemEventArgs e)
        {
            try
            {
                string filename = e.Name ?? "";
                string fullPath = e.FullPath ?? "";

                if (!string.IsNullOrEmpty(filename))
                {
                    // 파일 정보 수집
                    string fileInfo = "";
                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            System.Threading.Thread.Sleep(100);
                            var info = new FileInfo(fullPath);
                            fileInfo = $", Size: {info.Length / 1024.0:F2} KB, Created: {info.CreationTime:yyyy-MM-dd HH:mm:ss}";
                        }
                        catch { }
                    }

                    Logger.SimpleLog($"[Screenshot] {filename} | Path: {fullPath}{fileInfo}");

                    Server.SendFilename(filename);

                    // 2차 트리거: 스크린샷 생성 이벤트 발생
                    MapEventService.Instance.OnScreenshotTaken();
                }
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[Screenshot Error] {ex.Message}");
            }
        }
    }
}
