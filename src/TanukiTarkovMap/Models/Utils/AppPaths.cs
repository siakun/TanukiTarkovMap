using System.IO;
using TanukiTarkovMap.Models.Migrations;

/**
AppPaths - 앱이 사용자 폴더에 두는 현재 파일 위치와 정리 정책의 단일 출처

Purpose: 설정과 브라우저 프로필의 현재 경로를 한 곳에서 정하고, 크기 측정, 캐시 비우기,
제거 훅처럼 현재 경로와 이전 산출물을 함께 다뤄야 하는 정리 작업을 제공한다.

Architecture: 현재 경로와 지속되는 수명 규칙은 AppPaths가 소유하고, 0.1.0 위치를 아는
일회성 이전 규칙은 Models/Migrations의 두 클래스가 소유한다.
- 설정(Roaming): 앱을 지워도 남아야 한다. Velopack 설치 폴더 밖이라 제거 대상이 아니고,
  다시 설치하면 그대로 이어 쓴다
- 활성 브라우저 데이터(Local): 앱을 지우면 함께 사라져야 한다. Velopack 설치 폴더 안이므로
  Update.exe --uninstall이 그 폴더를 지울 때 같이 정리된다
- 이전 산출물: 마이그레이션 클래스가 경로 목록을 제공하고 AppPaths의 정리 작업이 함께 다룬다

Core Functionality:
- SettingsFilePath: settings.json의 위치
- BrowserCacheFolder: CefSharp에 넘기는 프로필 폴더 (캐시뿐 아니라 쿠키와 IndexedDB도 들어간다)
- PrepareOnStartup(): 설정 이전, 브라우저 프로필 이전, 코드 캐시 정리를 순서대로 실행
- GetBrowserCacheSize() / DeleteBrowserCacheIfRequested(): 설정 화면의 캐시 표시와 비우기.
  지금 쓰는 자리뿐 아니라 예전 자리와 이전이 끊겨 남은 사본까지 함께 센다
- DeleteRoamingBrowserDataOnUninstall(): 제거할 때 Roaming에 남은 브라우저 데이터만 지운다
- WriteSettingsFile() / DeleteSettingsFiles(): 현재 설정을 원자적으로 저장하고 초기화 시 이전 파일도 삭제

State Management:
- _browserCacheFolderForCurrentRun: 기본값은 Local이며 이전 실패 시 그 실행에서만 예전 원본 경로
- BrowserCacheResetRequested: 실행 중 요청하고 Cef.Shutdown 뒤 한 번 처리하는 메모리 상태

Method Flow:
  앱 시작 -> PrepareOnStartup -> SettingsLocationMigration -> BrowserCacheLocationMigration
           -> TrimCodeCacheIfOversized -> InitializeCef
  앱 종료 -> Cef.Shutdown -> DeleteBrowserCacheIfRequested
  앱 제거 -> Velopack 빠른 훅 -> DeleteRoamingBrowserDataOnUninstall

Key Methods:
- PrepareOnStartup(): CEF가 열리기 전에 두 이전 작업과 코드 캐시 정리를 정해진 순서로 호출
- WriteSettingsFile(json): 현재 설정을 임시 파일에서 원자적으로 교체
- DeleteBrowserCacheIfRequested(): 현재 프로필과 이전 산출물, 중단 표시를 종료 시점에 정리
- DeleteRoamingBrowserDataOnUninstall(): RoamingRoot 아래 브라우저 데이터만 골라 제거

Dependencies:
- SettingsLocationMigration: 예전 설정 경로와 단방향 병합 규칙 제공
- BrowserCacheLocationMigration: 예전 브라우저 경로, 작업 경로, 실행 중 프로필 선택 제공
- Logger: 크기 측정과 파일 정리 실패 기록

Historical Context: 경로 계산과 0.1.0 데이터 이전이 한 파일에 섞여 AppPaths의 절반 이상이
한 시점의 이전 사건을 다뤘다. 2026-08-16에 이전 코드를 Models/Migrations로 분리했다.

Design Rationale: 현재 경로는 계속 적용되는 규칙이고 이전은 예전 사용자가 처음 올라올 때의
사건이므로 파일을 나눈다. AppPaths가 마이그레이션의 이전 경로 목록을 받아 정리에 쓰게 해,
이전 지원을 삭제할 때 손봐야 할 결합 지점을 컴파일러가 드러내도록 한다. 별도 러너나 버전
프레임워크는 항목 두 개에 필요하지 않아 PrepareOnStartup()이 호출 순서를 직접 유지한다.

Known Limitations: 브라우저 이전 실패 시 BrowserCacheFolder는 그 실행에서 예전 Roaming 원본을
가리킨다. 이 선택은 BrowserCacheLocationMigration이 하며 다음 시작에는 다시 Local로 초기화한다.

Critical Warnings: PrepareOnStartup()은 CEF 초기화 전에 불러야 한다.
CEF가 캐시 폴더를 열고 나면 폴더를 옮기거나 지울 수 없어 조용히 실패한다.
제거 훅은 30초 안에 끝나야 하므로 LocalRoot 정리는 Velopack에 맡기고 예외를 밖으로 던지지 않는다.

Last Updated: 2026-08-16 | .NET 8 / Velopack 0.0.1298 | 위치 이전 로직 분리
*/
namespace TanukiTarkovMap.Models.Utils
{
    public static class AppPaths
    {
        private const string AppFolderName = "TanukiTarkovMap";

        /// <summary>
        /// 코드 캐시를 비우는 기준 크기(MB). 설정 화면 안내 문구도 이 값을 읽어 쓴다
        /// </summary>
        public const int CodeCacheLimitMegabytes = 300;

        internal static string RoamingRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName);

        internal static string LocalRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);

        internal static string _browserCacheFolderForCurrentRun = CanonicalBrowserCacheFolder;

        /// <summary> 설정 폴더. 앱을 제거해도 남는다 </summary>
        public static string SettingsFolder => RoamingRoot;

        /// <summary> settings.json 전체 경로. 저장은 언제나 이 자리에 한다 </summary>
        public static string SettingsFilePath => Path.Combine(SettingsFolder, "settings.json");

        /// <summary>
        /// CefSharp에 넘기는 프로필 폴더. 정상 상태에서는 앱을 제거하면 함께 사라지는 Local 경로다.
        /// 이전이 실패한 실행에서만 데이터 보존을 위해 예전 원본 경로를 돌려준다. 이름은 캐시지만
        /// 쿠키와 IndexedDB 같은 사이트 데이터도 이 안에 들어간다
        /// </summary>
        public static string BrowserCacheFolder => _browserCacheFolderForCurrentRun;

        /// <summary> 현재 버전이 브라우저 프로필을 두는 실제 경로 </summary>
        internal static string CanonicalBrowserCacheFolder => Path.Combine(LocalRoot, "Cache");

        /// <summary>
        /// V8이 컴파일한 자바스크립트 바이트코드가 쌓이는 폴더.
        /// 브라우저 프로필 안에 있지만 이 폴더만 따로 비울 수 있다
        /// </summary>
        private static string CodeCacheFolder => Path.Combine(BrowserCacheFolder, "Default", "Code Cache");

        /// <summary>
        /// 브라우저 데이터가 놓일 수 있는 모든 자리. 크기를 재고 비울 때 함께 다룬다.
        ///
        /// 지금 쓰는 자리 하나만 보면 안 된다. 이전 구현이나 실패 때문에 예전 자리에 별도 프로필이
        /// 남을 수 있고, 이전이 중간에 끊기면 정리 폴더나 복사 폴더에 사본이 생긴다. 이 자리들은
        /// 설정 화면에 잡히지 않아 사용자가 존재를 모르는 채로 수백MB가 쌓인다.
        ///
        /// 예전 자리의 별도 프로필은 안전하게 합칠 수 없어 시작할 때 지우지 않고, 사용자가 비우기를
        /// 눌렀을 때만 지운다. 정리 폴더와 복사 폴더는 현재 경로에 완전한 사본이 있음을 확인한 뒤 치운다
        /// </summary>
        private static IReadOnlyList<string> BrowserDataFolders =>
            [CanonicalBrowserCacheFolder, ..BrowserCacheLocationMigration.DataFolders];

        /// <summary>
        /// 앱을 닫을 때 브라우저 캐시를 비울지 여부.
        /// 실행 중에는 CEF가 프로필 파일을 붙들고 있어 그 자리에서 지울 수 없다
        /// </summary>
        public static bool BrowserCacheResetRequested { get; set; }

        /// <summary>
        /// 브라우저 데이터가 지금 차지하는 크기(byte). 어느 자리에도 없으면 0.
        /// 파일 수천 개를 훑으므로 UI 스레드에서 직접 부르지 않는다
        /// </summary>
        public static long GetBrowserCacheSize()
        {
            long totalBytes = 0;

            // 한 자리가 실패해도 나머지는 센다. 0을 돌려주면 사용자는 쌓인 것이 없다고 읽는다
            foreach (var path in BrowserDataFolders)
            {
                try
                {
                    var folder = new DirectoryInfo(path);
                    if (!folder.Exists) continue;

                    totalBytes += folder.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
                }
                catch (Exception ex)
                {
                    Logger.SimpleLog($"[AppPaths] Browser data size check failed for {path}: {ex.Message}");
                }
            }

            return totalBytes;
        }

        /// <summary>
        /// 앱을 제거하기 전에 Roaming에 보존된 브라우저 데이터만 지운다.
        /// 설정도 같은 루트에 있으므로 RoamingRoot 자체는 절대 지우지 않는다.
        /// Velopack 제거를 막지 않도록 모든 실패를 내부에서 처리한다
        /// </summary>
        public static void DeleteRoamingBrowserDataOnUninstall()
        {
            try
            {
                foreach (var path in BrowserDataFolders.Where(IsUnderRoamingRoot))
                {
                    try
                    {
                        if (DeleteBrowserDataDirectory(path))
                        {
                            Logger.SimpleLog($"[AppPaths] Roaming browser data removed at {path}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.SimpleLog($"[AppPaths] Roaming browser data removal failed for {path}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[AppPaths] Roaming browser data cleanup failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 예약돼 있으면 브라우저 데이터를 지운다. Cef.Shutdown() 뒤에 호출해야 파일이 풀려 있다.
        /// 지우지 못해도 다음에 다시 예약하면 되므로 예외를 밖으로 던지지 않는다.
        ///
        /// 지금 쓰는 자리만 지우면 예전 자리에 남은 프로필은 크기 표시에만 잡히고 사라지지 않아,
        /// 사용자가 비우기를 눌러도 숫자가 그대로인 것처럼 보인다
        /// </summary>
        public static void DeleteBrowserCacheIfRequested()
        {
            if (!BrowserCacheResetRequested) return;
            BrowserCacheResetRequested = false;

            foreach (var path in BrowserDataFolders)
            {
                try
                {
                    if (DeleteBrowserDataDirectory(path))
                    {
                        Logger.SimpleLog($"[AppPaths] Browser data cleared at {path}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.SimpleLog($"[AppPaths] Browser data clear failed for {path}: {ex.Message}");
                }
            }

            try
            {
                File.Delete(BrowserCacheLocationMigration.MigrationMarkerPath);
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[AppPaths] Browser migration marker cleanup failed: {ex.Message}");
            }

            _browserCacheFolderForCurrentRun = CanonicalBrowserCacheFolder;
        }

        /// <summary>
        /// 앱을 시작할 때 사용자 폴더를 정돈한다. CEF가 캐시 폴더를 열기 전에 호출해야 한다.
        /// 폴더를 옮기는 일과 비우는 일은 순서를 지켜야 하므로 호출 측이 나눠 부르지 않게 묶어 둔다
        /// </summary>
        public static void PrepareOnStartup()
        {
            _browserCacheFolderForCurrentRun = CanonicalBrowserCacheFolder;
            SettingsLocationMigration.Migrate();
            BrowserCacheLocationMigration.Migrate();
            TrimCodeCacheIfOversized();
        }

        /// <summary>
        /// 코드 캐시가 상한을 넘으면 그 폴더만 비운다.
        ///
        /// 맵 타일이 담긴 HTTP 캐시는 맵 종류만큼만 쌓여 스스로 포화하지만(맵 하나 21MB, 11종 약 230MB),
        /// 코드 캐시는 맵을 열 때마다 0.8MB씩 붙어 상한이 없다. 9개월 만에 300MB를 넘긴 실측이 근거다.
        /// 지워도 자바스크립트를 다시 컴파일할 뿐이라 첫 페이지 로드가 잠깐 느려지는 것으로 끝나고,
        /// 맵 타일은 그대로 남아 체감 차이가 없다. HTTP 캐시에 상한을 걸지 않는 이유도 같다.
        /// 상한을 걸면 타일이 밀려나 매번 다시 받는다
        /// </summary>
        private static void TrimCodeCacheIfOversized()
        {
            try
            {
                var folder = new DirectoryInfo(CodeCacheFolder);
                if (!folder.Exists) return;

                var sizeInBytes = folder.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
                if (sizeInBytes <= CodeCacheLimitMegabytes * 1024L * 1024L) return;

                folder.Delete(recursive: true);
                Logger.SimpleLog($"[AppPaths] Code cache trimmed ({sizeInBytes / 1024 / 1024} MB over {CodeCacheLimitMegabytes} MB limit)");
            }
            catch (Exception ex)
            {
                // 비우지 못해도 다음 실행에서 다시 시도한다
                Logger.SimpleLog($"[AppPaths] Code cache trim failed: {ex.Message}");
            }
        }

        internal static void WriteSettingsFile(string json)
        {
            Directory.CreateDirectory(SettingsFolder);

            var temporaryPath = Path.Combine(
                SettingsFolder,
                $".{Path.GetFileName(SettingsFilePath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                File.WriteAllText(temporaryPath, json);
                File.Move(temporaryPath, SettingsFilePath, overwrite: true);
            }
            finally
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception ex)
                {
                    Logger.SimpleLog($"[AppPaths] Temporary settings cleanup failed: {ex.Message}");
                }
            }
        }

        internal static void DeleteSettingsFiles()
        {
            foreach (var path in SettingsLocationMigration.LegacySettingsFiles.Append(SettingsFilePath))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                    Logger.SimpleLog($"[AppPaths] Settings deletion failed for {path}: {ex.Message}");
                }
            }
        }

        internal static bool DeleteBrowserDataDirectory(string path)
        {
            if (!Directory.Exists(path)) return false;

            Directory.Delete(path, recursive: true);
            return true;
        }

        private static bool IsUnderRoamingRoot(string path)
        {
            var relativePath = Path.GetRelativePath(RoamingRoot, path);

            return relativePath != "."
                   && !relativePath.Equals("..", StringComparison.Ordinal)
                   && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                   && !Path.IsPathRooted(relativePath);
        }
    }
}
