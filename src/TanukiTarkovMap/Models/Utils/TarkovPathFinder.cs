using System.IO;
using Microsoft.Win32;

namespace TanukiTarkovMap.Models.Utils
{
    /// <summary>
    /// Escape from Tarkov 게임 관련 경로를 자동으로 탐지하는 유틸리티 클래스
    /// </summary>
    public static class TarkovPathFinder
    {
        private static bool _gameFolderLoggedOnce = false;
        private static bool _screenshotsFolderLoggedOnce = false;

        /// <summary>
        /// Windows 레지스트리에서 Escape from Tarkov 게임 설치 경로를 찾습니다.
        /// 공식 버전(레지스트리) -> 스팀 버전(스팀 경로) 순으로 탐지합니다.
        /// </summary>
        /// <returns>게임 설치 경로, 찾지 못한 경우 null</returns>
        public static string? FindGameFolder()
        {
            // 1. 공식 홈페이지 버전 탐지 (레지스트리)
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                    "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\EscapeFromTarkov"
                );

                var installPath = key?.GetValue("InstallLocation")?.ToString();

                if (!string.IsNullOrEmpty(installPath))
                {
                    if (!_gameFolderLoggedOnce)
                    {
                        Logger.SimpleLog($"[TarkovPath] Game folder found (Official): {installPath}");
                        _gameFolderLoggedOnce = true;
                    }
                    return installPath;
                }
            }
            catch (Exception ex)
            {
                if (!_gameFolderLoggedOnce)
                {
                    Logger.SimpleLog($"[TarkovPath] Error finding official game folder: {ex.Message}");
                }
            }

            // 2. 스팀 버전 탐지 (스팀 설치 경로)
            try
            {
                using RegistryKey? steamKey = Registry.LocalMachine.OpenSubKey(
                    "SOFTWARE\\WOW6432Node\\Valve\\Steam"
                );

                var steamPath = steamKey?.GetValue("InstallPath")?.ToString();

                if (!string.IsNullOrEmpty(steamPath))
                {
                    var tarkovPath = Path.Combine(steamPath, "steamapps", "common", "Escape from Tarkov");

                    if (Directory.Exists(tarkovPath))
                    {
                        if (!_gameFolderLoggedOnce)
                        {
                            Logger.SimpleLog($"[TarkovPath] Game folder found (Steam): {tarkovPath}");
                            _gameFolderLoggedOnce = true;
                        }
                        return tarkovPath;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_gameFolderLoggedOnce)
                {
                    Logger.SimpleLog($"[TarkovPath] Error finding Steam game folder: {ex.Message}");
                }
            }

            if (!_gameFolderLoggedOnce)
            {
                Logger.SimpleLog("[TarkovPath] Game folder not found (checked both Official and Steam)");
                _gameFolderLoggedOnce = true;
            }
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
                    if (!_screenshotsFolderLoggedOnce)
                    {
                        Logger.SimpleLog($"[TarkovPath] OneDrive path detection error: {ex.Message}");
                    }
                }
            }

            // 3. 존재하는 첫 번째 경로 반환
            foreach (var path in possiblePaths)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        if (!_screenshotsFolderLoggedOnce)
                        {
                            Logger.SimpleLog($"[TarkovPath] Screenshots folder found: {path}");
                            _screenshotsFolderLoggedOnce = true;
                        }
                        return path;
                    }
                }
                catch (Exception ex)
                {
                    if (!_screenshotsFolderLoggedOnce)
                    {
                        Logger.SimpleLog($"[TarkovPath] Error checking path {path}: {ex.Message}");
                    }
                }
            }

            if (!_screenshotsFolderLoggedOnce)
            {
                Logger.SimpleLog("[TarkovPath] Screenshots folder not found in any known location");
                _screenshotsFolderLoggedOnce = true;
            }
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
