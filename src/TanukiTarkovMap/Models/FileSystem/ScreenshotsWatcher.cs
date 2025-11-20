using System.IO;
using TanukiTarkovMap.Models.Services;

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

                if (!string.IsNullOrEmpty(filename))
                {
                    Server.SendFilename(filename);

                    // 2차 트리거: 스크린샷 생성 이벤트 발생
                    if (App.GetSettings().PipEnabled)
                    {
                        MapEventService.Instance.OnScreenshotTaken();
                    }
                }
            }
            catch (Exception) { }
        }
    }
}
