using TanukiTarkovMap.Models.FileSystem;

namespace TanukiTarkovMap.Models.Services
{
    public static class Watcher
    {
        public static void Start()
        {
            ScreenshotsWatcher.Start();
            LogsWatcher.Start();
        }

        public static void Stop()
        {
            ScreenshotsWatcher.Stop();
            LogsWatcher.Stop();
        }

        public static void Restart()
        {
            //Env.InitialLogsRead = true;
            ScreenshotsWatcher.Restart();
            LogsWatcher.Restart();
        }
    }
}
