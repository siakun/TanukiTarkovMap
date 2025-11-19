using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace TanukiTarkovMap.Models.Utils
{
    /// <summary>
    /// 간단한 파일 로거
    /// </summary>
    public static class Logger
    {
        private static readonly object _lockObject = new object();
        private static string? _logFilePath;

        static Logger()
        {
            InitializeLogFile();
        }

        private static void InitializeLogFile()
        {
            try
            {
                // Logs 폴더 경로
                string logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

                // Logs 폴더가 없으면 생성
                if (!Directory.Exists(logsDirectory))
                {
                    Directory.CreateDirectory(logsDirectory);
                }

                // 로그 파일명 생성 (날짜_시간.txt)
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                _logFilePath = Path.Combine(logsDirectory, $"{timestamp}.txt");

                // 초기 로그 작성
                WriteLog("=== TanukiTarkovMap Log Started ===");
                WriteLog($"Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
                WriteLog($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                WriteLog("=====================================");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize log file: {ex.Message}");
            }
        }

        /// <summary>
        /// 로그 메시지를 파일에 기록
        /// </summary>
        public static void Log(string message,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            // Debug 출력도 함께
            System.Diagnostics.Debug.WriteLine(message);

            // 파일 기록
            WriteLog($"[{DateTime.Now:HH:mm:ss.fff}] {Path.GetFileNameWithoutExtension(sourceFilePath)}.{memberName}:{sourceLineNumber} - {message}");
        }

        /// <summary>
        /// 간단한 로그 메시지 (호출자 정보 없이)
        /// </summary>
        public static void SimpleLog(string message)
        {
            // Debug 출력도 함께
            System.Diagnostics.Debug.WriteLine(message);

            // 파일 기록
            WriteLog($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        /// <summary>
        /// 에러 로그
        /// </summary>
        public static void Error(string message, Exception? ex = null)
        {
            string errorMessage = $"[ERROR] {message}";
            if (ex != null)
            {
                errorMessage += $"\nException: {ex.GetType().Name}\nMessage: {ex.Message}\nStackTrace: {ex.StackTrace}";
            }

            // Debug 출력
            System.Diagnostics.Debug.WriteLine(errorMessage);

            // 파일 기록
            WriteLog($"[{DateTime.Now:HH:mm:ss.fff}] {errorMessage}");
        }

        /// <summary>
        /// 파일에 로그 작성 (thread-safe)
        /// </summary>
        private static void WriteLog(string message)
        {
            if (string.IsNullOrEmpty(_logFilePath))
                return;

            lock (_lockObject)
            {
                try
                {
                    File.AppendAllText(_logFilePath, message + Environment.NewLine);
                }
                catch
                {
                    // 로그 작성 실패 시 무시
                }
            }
        }

        /// <summary>
        /// 현재 로그 파일 경로 반환
        /// </summary>
        public static string? GetCurrentLogPath()
        {
            return _logFilePath;
        }
    }
}