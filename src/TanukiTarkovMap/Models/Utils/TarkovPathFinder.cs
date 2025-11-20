using System.IO;
using Microsoft.Win32;

namespace TanukiTarkovMap.Models.Utils
{
    /// <summary>
    /// Escape from Tarkov 게임 관련 경로를 자동으로 탐지하는 유틸리티 클래스
    /// </summary>
    public static class TarkovPathFinder
    {
        /// <summary>
        /// Windows 레지스트리에서 Escape from Tarkov 게임 설치 경로를 찾습니다.
        /// </summary>
        /// <returns>게임 설치 경로, 찾지 못한 경우 null</returns>
        public static string? FindGameFolder()
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                    "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\EscapeFromTarkov"
                );

                var installPath = key?.GetValue("InstallLocation")?.ToString();

                if (!string.IsNullOrEmpty(installPath))
                {
                    Logger.SimpleLog($"Found game folder: {installPath}");
                    return installPath;
                }
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"Error finding game folder: {ex.Message}");
            }

            Logger.SimpleLog("Game folder not found in registry");
            return null;
        }

        /// <summary>
        /// 게임 로그 폴더 경로를 반환합니다.
        /// </summary>
        /// <param name="gameFolder">게임 설치 폴더</param>
        /// <returns>로그 폴더 경로, 게임 폴더가 없는 경우 null</returns>
        public static string? GetLogsFolder(string? gameFolder)
        {
            if (string.IsNullOrEmpty(gameFolder))
                return null;

            return Path.Combine(gameFolder, "Logs");
        }

        /// <summary>
        /// 다양한 경로 패턴을 시도하여 실제 스크린샷 폴더를 찾습니다.
        /// Windows의 다양한 환경(일반 Documents, OneDrive 동기화 등)을 지원합니다.
        /// </summary>
        /// <returns>스크린샷 폴더 경로, 찾지 못한 경우 null</returns>
        public static string? FindScreenshotsFolder()
        {
            var documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var possiblePaths = new List<string>
            {
                // 1. 일반 Documents 경로 (가장 흔한 케이스)
                Path.Combine(documentsFolder, "Escape from Tarkov", "Screenshots"),
                Path.Combine(documentsFolder, "Escape From Tarkov", "Screenshots"),
            };

            // 2. OneDrive 경로들 탐색
            var oneDriveBasePath = Path.Combine(userProfile, "OneDrive");
            if (Directory.Exists(oneDriveBasePath))
            {
                try
                {
                    // OneDrive\문서 (한글 Windows)
                    possiblePaths.Add(Path.Combine(oneDriveBasePath, "문서", "Escape from Tarkov", "Screenshots"));
                    possiblePaths.Add(Path.Combine(oneDriveBasePath, "문서", "Escape From Tarkov", "Screenshots"));

                    // OneDrive\Documents (영문 Windows)
                    possiblePaths.Add(Path.Combine(oneDriveBasePath, "Documents", "Escape from Tarkov", "Screenshots"));
                    possiblePaths.Add(Path.Combine(oneDriveBasePath, "Documents", "Escape From Tarkov", "Screenshots"));

                    // OneDrive 하위의 다른 폴더들도 검사 (예: OneDrive - Personal, OneDrive - Company 등)
                    var oneDriveDirs = Directory.GetDirectories(oneDriveBasePath, "*", SearchOption.TopDirectoryOnly);
                    foreach (var dir in oneDriveDirs)
                    {
                        var dirName = Path.GetFileName(dir);
                        // "문서" 또는 "Documents"로 끝나는 폴더 찾기
                        if (dirName.Equals("문서", StringComparison.OrdinalIgnoreCase) ||
                            dirName.Equals("Documents", StringComparison.OrdinalIgnoreCase))
                        {
                            possiblePaths.Add(Path.Combine(dir, "Escape from Tarkov", "Screenshots"));
                            possiblePaths.Add(Path.Combine(dir, "Escape From Tarkov", "Screenshots"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.SimpleLog($"OneDrive path detection error: {ex.Message}");
                }
            }

            // 3. 존재하는 첫 번째 경로 반환
            foreach (var path in possiblePaths)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Logger.SimpleLog($"Found screenshots folder: {path}");
                        return path;
                    }
                }
                catch (Exception ex)
                {
                    Logger.SimpleLog($"Error checking path {path}: {ex.Message}");
                }
            }

            Logger.SimpleLog("Screenshots folder not found in any known location");
            return null;
        }

        /// <summary>
        /// 스크린샷 폴더의 기본 경로를 반환합니다.
        /// 자동 탐지가 실패했을 때 사용되는 폴백 경로입니다.
        /// </summary>
        /// <returns>기본 스크린샷 폴더 경로</returns>
        public static string GetDefaultScreenshotsFolder()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Escape from Tarkov",
                "Screenshots"
            );
        }
    }
}
