namespace TarkovClientLogger
{
    public class TarkovClientLogger
    {
        public static void CheckTarkovClientDailyUseUsersCount() { }

        public static void CheckTarkovClientInstallUsersCount() { }

        // 디버그 로그 작성
        public static void WriteDebugLog(string message)
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "test_log.txt"
                );
                var logMessage =
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - [WindowTopmost] {message}\n";
                System.IO.File.AppendAllText(logPath, logMessage);
            }
            catch { }
        }
    }
}
