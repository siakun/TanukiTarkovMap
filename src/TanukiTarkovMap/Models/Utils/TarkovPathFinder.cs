using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

/**
TarkovPathFinder - Escape from Tarkov 게임/로그/스크린샷 경로 자동 탐지

Purpose: 설치 형태(공식 런처, 스팀)와 무관하게 게임 폴더와 로그 폴더를 찾아
LogsWatcher(자동 맵 전환)와 설정 기본값 생성(Settings.CreateDefaultSettings)에 공급한다.

Core Functionality:
- FindGameFolder(): 공식 런처 레지스트리 -> 스팀 라이브러리 순으로 게임 폴더 탐지
- GetLogsFolder(gameFolder): 게임 폴더 기준 로그 폴더 해석.
  공식 런처(게임 폴더\Logs)와 스팀(게임 폴더\build\Logs) 레이아웃을 모두 확인
- FindScreenshotsFolder() / GetDefaultScreenshotsFolder(): 문서 폴더 기반 스크린샷 경로 탐지

Detection Flow:
  공식: HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\EscapeFromTarkov
        의 InstallLocation. 폴더가 실제 존재할 때만 채택한다 (언인스톨 잔여 레지스트리 무시)
  스팀: HKLM\SOFTWARE\WOW6432Node\Valve\Steam 의 InstallPath 를 기점으로
        steamapps\libraryfolders.vdf 의 "path" 항목을 파싱해 모든 라이브러리 폴더를 얻고,
        각 라이브러리\steamapps\common\Escape from Tarkov 를 순서대로 확인

Historical Context:
- 2026-07: 스팀판에서 로그 감시(자동 맵 전환)가 조용히 죽는 원인 2개를 수정.
  (1) 스팀판은 게임 본체가 설치 폴더의 build 하위에 있고 로그도 build\Logs 에 쌓이는데
      설치 폴더\Logs 만 확인해 감시가 시작되지 못했다. GetLogsFolder 가 두 레이아웃을
      모두 확인하는 방식으로 해결했다. FindGameFolder 를 고치는 대신 이 지점을 고른 이유:
      게임 폴더 값은 settings.json 에 저장되어 재실행 시 탐지를 건너뛰므로, 로그 해석
      단계에서 흡수해야 이미 저장된 값(래퍼/공식/build 어느 쪽이든)도 수정 없이 유효하다.
  (2) libraryfolders.vdf 를 읽지 않아 스팀 기본 라이브러리 외 드라이브에 설치된
      게임을 찾지 못했다. vdf 파싱으로 모든 라이브러리를 검사하도록 해결했다.

Known Limitations:
- 구형 스팀(2021년 이전)의 config\libraryfolders.vdf 스키마는 지원하지 않는다.
  EFT 스팀 출시(2025년) 시점상 해당 환경은 사실상 없다.

Last Updated: 2026-07-16
*/
namespace TanukiTarkovMap.Models.Utils
{
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

                // 언인스톨 후 남은 잔여 레지스트리가 스팀 탐지를 가리지 않도록 실존 폴더만 채택
                if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
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
                    foreach (var libraryRoot in GetSteamLibraryRoots(steamPath))
                    {
                        var tarkovPath = Path.Combine(libraryRoot, "steamapps", "common", "Escape from Tarkov");

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
        /// 스팀 본체 설치 폴더와 steamapps\libraryfolders.vdf에 등록된 모든 라이브러리 폴더를 반환합니다.
        /// vdf가 없거나 읽지 못하면 스팀 본체 설치 폴더 하나만 반환합니다.
        /// </summary>
        /// <param name="steamInstallPath">레지스트리 InstallPath 값 (스팀 본체 설치 폴더)</param>
        /// <returns>중복 제거된 라이브러리 폴더 목록 (스팀 본체 설치 폴더 포함)</returns>
        private static List<string> GetSteamLibraryRoots(string steamInstallPath)
        {
            var libraryRoots = new List<string> { steamInstallPath };

            try
            {
                var vdfPath = Path.Combine(steamInstallPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdfPath))
                {
                    // vdf 항목 예: "path"    "D:\\SteamLibrary" (백슬래시가 \\ 로 이스케이프됨)
                    var pathMatches = Regex.Matches(File.ReadAllText(vdfPath), "\"path\"\\s+\"([^\"]+)\"");
                    foreach (Match pathMatch in pathMatches)
                    {
                        var libraryPath = pathMatch.Groups[1].Value.Replace("\\\\", "\\");
                        if (!libraryRoots.Contains(libraryPath, StringComparer.OrdinalIgnoreCase))
                        {
                            libraryRoots.Add(libraryPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[TarkovPath] libraryfolders.vdf parse error: {ex.Message}");
            }

            return libraryRoots;
        }

        /// <summary>
        /// 게임 로그 폴더 경로를 반환합니다.
        /// 공식 런처는 게임 폴더 바로 아래 Logs에, 스팀판은 build\Logs에 로그를 쌓으므로
        /// 두 레이아웃을 순서대로 확인하고, 둘 다 없으면 공식 레이아웃 경로를 반환합니다
        /// (존재 여부는 호출 측인 LogsWatcher가 재확인).
        /// </summary>
        /// <param name="gameFolder">게임 설치 폴더</param>
        /// <returns>로그 폴더 경로, 게임 폴더가 없는 경우 null</returns>
        public static string? GetLogsFolder(string? gameFolder)
        {
            if (string.IsNullOrEmpty(gameFolder))
                return null;

            var candidatePaths = new[]
            {
                Path.Combine(gameFolder, "Logs"),
                Path.Combine(gameFolder, "build", "Logs"),
            };

            foreach (var path in candidatePaths)
            {
                if (Directory.Exists(path))
                    return path;
            }

            return candidatePaths[0];
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
