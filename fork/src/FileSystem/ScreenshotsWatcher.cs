using System;
using System.IO;

namespace TarkovClient
{
    public static class ScreenshotsWatcher
    {
        static FileSystemWatcher screenshotsWatcher;

        public static void Start()
        {
            if (!Directory.Exists(Env.ScreenshotsFolder))
            {
                return;
            }

            screenshotsWatcher = new FileSystemWatcher(Env.ScreenshotsFolder);
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
                    
                    // 2차 트리거: 스크린샷 생성 시 PiP 활성화
                    if (Env.GetSettings().pipEnabled && PipController.Instance != null)
                    {
                        PipController.Instance.OnScreenshotTaken();
                    }
                }
            }
            catch (Exception) { }
        }
    }
}
