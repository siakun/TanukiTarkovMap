using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace TarkovClient
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
