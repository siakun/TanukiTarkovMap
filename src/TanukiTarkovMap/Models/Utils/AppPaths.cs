using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

/**
AppPaths - 앱이 사용자 폴더에 두는 파일의 위치를 정하는 단일 출처

Purpose: 설정과 브라우저 캐시를 어느 폴더에 둘지, 예전 위치에 남은 파일을 어떻게 옮길지
한 곳에서 정한다. 이전에는 같은 경로 계산이 Settings, SettingsViewModel, App에 흩어져 있었다.

Architecture: 설정과 브라우저 데이터의 수명을 경로와 제거 훅으로 나눈다.
- 설정(Roaming): 앱을 지워도 남아야 한다. Velopack 설치 폴더 밖이라 제거 대상이 아니고,
  다시 설치하면 그대로 이어 쓴다
- 활성 브라우저 데이터(Local): 앱을 지우면 함께 사라져야 한다. Velopack 설치 폴더 안이므로
  Update.exe --uninstall이 그 폴더를 지울 때 같이 정리된다
- 이전 원본과 작업 사본(Roaming/임시): 완료를 확인하기 전에는 보존한다. 확정된 사본은
  다음 시작에 정리하고, 별도 프로필은 사용자가 캐시를 비우거나 앱을 제거할 때 정리한다

Core Functionality:
- SettingsFilePath: settings.json의 위치
- BrowserCacheFolder: CefSharp에 넘기는 프로필 폴더 (캐시뿐 아니라 쿠키와 IndexedDB도 들어간다)
- PrepareOnStartup(): 예전 위치의 파일을 넘겨받고 불어난 코드 캐시를 비운다
- GetBrowserCacheSize() / DeleteBrowserCacheIfRequested(): 설정 화면의 캐시 표시와 비우기.
  지금 쓰는 자리뿐 아니라 예전 자리와 이전이 끊겨 남은 사본까지 함께 센다
- DeleteRoamingBrowserDataOnUninstall(): 제거할 때 Roaming에 남은 브라우저 데이터만 지운다

State Management:
- _browserCacheFolderForCurrentRun: 이전 성공 시 Local, 실패 시 그 실행에서만 예전 원본 경로
- BrowserCacheResetRequested: 실행 중 요청하고 Cef.Shutdown 뒤 한 번 처리하는 메모리 상태
- Cache.migration-pending: 프로세스 중단 뒤 원본과 미확정 대상을 구분하는 디스크 표시

Method Flow:
  앱 시작 -> PrepareOnStartup -> (설정 병합, 캐시 이전, 코드 캐시 정리) -> InitializeCef
  앱 종료 -> Cef.Shutdown -> DeleteBrowserCacheIfRequested
  앱 제거 -> Velopack 빠른 훅 -> DeleteRoamingBrowserDataOnUninstall

Key Methods:
- PrepareOnStartup(): CEF가 열리기 전에 설정과 브라우저 경로를 이전
- WriteSettingsFile(json): 현재 설정을 임시 파일에서 원자적으로 교체
- MigrateBrowserCache(): 원본을 이동하거나 완전 복사하고 실패하면 원본 경로로 되돌림
- RecoverInterruptedBrowserMigration(): 중단 표시와 남은 폴더를 확인해 재시도 또는 완료 처리

Dependencies:
- System.Text.Json.Nodes: 예전 파일과 현재 파일의 설정 속성 재귀 병합
- Logger: 이전과 정리 실패를 다음 실행에서 진단할 수 있게 기록

Historical Context: 0.1.0까지는 두 폴더가 정반대였다. 설정이 Velopack 설치 폴더 안(Local)에
있어 앱을 제거하면 맵별 창 위치까지 함께 사라졌고, 브라우저 캐시는 Roaming에 쌓여 제거한
뒤에도 수백MB가 남았다. 로밍 프로필을 쓰는 환경에서는 그 캐시가 로그인마다 네트워크를 오갔다.

Design Rationale: 설정은 예전 파일을 남긴 채 현재 파일과 병합한다. 현재 파일을 쓰지 못해도
SettingsReadPaths가 예전 파일을 읽어 실패한 이전 때문에 기본값으로 돌아가지 않게 한다.
캐시는 쿠키와 IndexedDB까지 담고 있어 같은 볼륨에서는 통째로 옮기고, 이동할 수 없으면
임시 폴더에 완전히 복사한 뒤 새 위치로 전환한다. 이전이 실패하면 실패 중 생긴 Local 대상을
지우고 그 실행에서는 원본 프로필을 계속 사용해, 빈 새 폴더가 다음 시작의 재시도를 막지 않게 한다.

Edge Cases: 이전 도중 프로세스가 끊기면 Cache.migration-pending을 남긴다. 다음 시작에
예전 원본이 있으면 미확정 Local 대상을 지우고 다시 이전하며, 원본 경로가 잠시 끊겼으면
빈 프로필을 만들지 않고 그 경로가 돌아올 때까지 이전을 미룬다.

Known Limitations: 중단 표시 없이 두 실제 Chromium 프로필이 함께 있으면 어느 쪽이 최신인지
안전하게 판정하거나 합칠 수 없다. 시작할 때는 둘 다 보존하고 크기 표시와 캐시 비우기에서 다룬다.

Critical Warnings: PrepareOnStartup()은 CEF 초기화 전에 불러야 한다.
CEF가 캐시 폴더를 열고 나면 폴더를 옮기거나 지울 수 없어 조용히 실패한다.
제거 훅은 30초 안에 끝나야 하므로 LocalRoot 정리는 Velopack에 맡기고 예외를 밖으로 던지지 않는다.

Last Updated: 2026-08-15 | .NET 8 / Velopack 0.0.1298 | 브라우저 데이터 이전 실패 복구
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

        private static string RoamingRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName);

        private static string LocalRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);

        private static string _browserCacheFolderForCurrentRun = CanonicalBrowserCacheFolder;

        /// <summary> 설정 폴더. 앱을 제거해도 남는다 </summary>
        public static string SettingsFolder => RoamingRoot;

        /// <summary> settings.json 전체 경로. 저장은 언제나 이 자리에 한다 </summary>
        public static string SettingsFilePath => Path.Combine(SettingsFolder, "settings.json");

        /// <summary>
        /// 설정을 읽을 후보. 더 최근 파일을 먼저 읽고 실패하면 다른 위치를 시도한다.
        /// 이전에 실패해 두 파일이 함께 남아도 최신 설정을 읽으며, 손상된 한 파일 때문에
        /// 정상인 다른 파일까지 버리고 기본값을 만들지 않는다
        /// </summary>
        internal static IReadOnlyList<string> SettingsReadPaths
        {
            get
            {
                var current = new FileInfo(SettingsFilePath);
                var legacy = new FileInfo(LegacySettingsFilePath);

                if (current.Exists && legacy.Exists)
                {
                    return current.LastWriteTimeUtc >= legacy.LastWriteTimeUtc
                        ? [current.FullName, legacy.FullName]
                        : [legacy.FullName, current.FullName];
                }

                if (current.Exists) return [current.FullName];
                if (legacy.Exists) return [legacy.FullName];
                return [];
            }
        }

        /// <summary>
        /// CefSharp에 넘기는 프로필 폴더. 정상 상태에서는 앱을 제거하면 함께 사라지는 Local 경로다.
        /// 이전이 실패한 실행에서만 데이터 보존을 위해 예전 원본 경로를 돌려준다. 이름은 캐시지만
        /// 쿠키와 IndexedDB 같은 사이트 데이터도 이 안에 들어간다
        /// </summary>
        public static string BrowserCacheFolder => _browserCacheFolderForCurrentRun;

        /// <summary> 현재 버전이 브라우저 프로필을 두는 실제 경로 </summary>
        private static string CanonicalBrowserCacheFolder => Path.Combine(LocalRoot, "Cache");

        /// <summary>
        /// V8이 컴파일한 자바스크립트 바이트코드가 쌓이는 폴더.
        /// 브라우저 프로필 안에 있지만 이 폴더만 따로 비울 수 있다
        /// </summary>
        private static string CodeCacheFolder => Path.Combine(BrowserCacheFolder, "Default", "Code Cache");

        /// <summary> 0.1.0까지 설정을 두던 자리 (Velopack 설치 폴더 안) </summary>
        private static string LegacySettingsFilePath => Path.Combine(LocalRoot, "settings.json");

        /// <summary> 0.1.0까지 캐시를 두던 자리 (Roaming) </summary>
        private static string LegacyBrowserCacheFolder => Path.Combine(RoamingRoot, "Cache");

        /// <summary> 다른 볼륨에서 프로필을 복사할 때만 쓰는 새 위치의 임시 폴더 </summary>
        private static string BrowserCacheMigrationFolder => Path.Combine(LocalRoot, "Cache.migrating");

        /// <summary> 복사가 끝난 예전 프로필을 활성 경로 밖에서 정리하는 폴더 </summary>
        private static string RetiredBrowserCacheFolder => Path.Combine(RoamingRoot, "Cache.migrated");

        /// <summary>
        /// 원본을 정리하기 전에 프로세스가 끊겼음을 다음 시작에 알리는 표시.
        /// 대상과 원본이 함께 있어도 어느 쪽이 이전 결과인지 추측하지 않게 한다
        /// </summary>
        private static string BrowserCacheMigrationMarkerPath => Path.Combine(LocalRoot, "Cache.migration-pending");

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
            [CanonicalBrowserCacheFolder, LegacyBrowserCacheFolder, RetiredBrowserCacheFolder, BrowserCacheMigrationFolder];

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
                File.Delete(BrowserCacheMigrationMarkerPath);
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
            MigrateSettings();
            MigrateBrowserCache();
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

        private static void MigrateSettings()
        {
            try
            {
                var legacy = new FileInfo(LegacySettingsFilePath);
                if (!legacy.Exists) return;

                // 예전 위치를 지우지 않으므로 이전에 실패하면 두 파일이 함께 남을 수 있다.
                // 새 파일보다 예전 파일이 더 최근일 때만 병합해 오래된 값으로 덮지 않는다
                var current = new FileInfo(SettingsFilePath);
                if (current.Exists && current.LastWriteTimeUtc >= legacy.LastWriteTimeUtc) return;

                var merged = current.Exists
                    ? ReadSettingsObject(SettingsFilePath)
                    : new JsonObject();
                var legacySettings = ReadSettingsObject(LegacySettingsFilePath);

                // 예전 파일의 값만 최신 값으로 덮고, 그 파일에 없던 새 속성은 현재 파일에 남긴다.
                // 중첩된 설정도 같은 규칙으로 합쳐 이후에 필드가 늘어도 이전할 수 있게 한다
                MergeJsonObjects(merged, legacySettings);
                WriteSettingsFile(merged.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                Logger.SimpleLog($"[AppPaths] Settings merged from {LegacySettingsFilePath} (newer)");
            }
            catch (Exception ex)
            {
                // SettingsReadPaths가 더 최신인 예전 파일을 먼저 돌려주고, 그 파일까지 읽지 못하면
                // 현재 파일을 다시 시도한다. 실패한 이전 때문에 곧바로 기본값을 만들지 않는다
                Logger.SimpleLog($"[AppPaths] Settings migration failed: {ex.Message}");
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
            foreach (var path in new[] { LegacySettingsFilePath, SettingsFilePath })
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

        private static JsonObject ReadSettingsObject(string path)
        {
            var root = JsonNode.Parse(File.ReadAllText(path));
            return root as JsonObject
                   ?? throw new InvalidDataException($"설정 파일의 최상위 값이 객체가 아닙니다: {path}");
        }

        private static void MergeJsonObjects(JsonObject current, JsonObject legacy)
        {
            foreach (var (name, legacyValue) in legacy)
            {
                if (legacyValue is JsonObject legacyObject && current[name] is JsonObject currentObject)
                {
                    MergeJsonObjects(currentObject, legacyObject);
                    continue;
                }

                current[name] = legacyValue?.DeepClone();
            }
        }

        private static void MigrateBrowserCache()
        {
            if (!RecoverInterruptedBrowserMigration()) return;

            var canonicalExistedAtStart = Directory.Exists(CanonicalBrowserCacheFolder);

            try
            {
                if (canonicalExistedAtStart)
                {
                    DeleteMigrationLeftovers();
                    return;
                }

                if (!Directory.Exists(LegacyBrowserCacheFolder)) return;

                Directory.CreateDirectory(LocalRoot);
                File.WriteAllText(BrowserCacheMigrationMarkerPath, string.Empty);

                try
                {
                    // 쿠키와 IndexedDB까지 들어 있어 버리지 않고 옮긴다.
                    // 같은 볼륨이면 수백MB여도 내용 복사 없이 끝난다
                    Directory.Move(LegacyBrowserCacheFolder, CanonicalBrowserCacheFolder);
                    Logger.SimpleLog($"[AppPaths] Browser cache moved to {CanonicalBrowserCacheFolder}");
                }
                catch (IOException moveFailure)
                {
                    // 로밍 폴더가 다른 볼륨이나 네트워크 공유에 있으면 이동할 수 없다. 다른 I/O 오류도
                    // 원본을 지울 근거가 아니므로, 임시 폴더에 완전히 복사한 뒤에만 새 위치를 쓴다
                    Logger.SimpleLog($"[AppPaths] Browser cache move failed ({moveFailure.Message}), copying safely");
                    CopyBrowserCacheTransactionally();
                }

                File.Delete(BrowserCacheMigrationMarkerPath);
            }
            catch (Exception ex)
            {
                // 이번 시도에서 생긴 새 프로필은 원본이 남아 있을 때만 지운다. 그렇지 않으면 CEF가
                // 빈 새 경로를 만들어 다음 실행부터 이전이 끝난 것으로 오인한다
                if (!canonicalExistedAtStart
                    && Directory.Exists(LegacyBrowserCacheFolder))
                {
                    try
                    {
                        DeleteBrowserDataDirectory(CanonicalBrowserCacheFolder);
                    }
                    catch (Exception cleanupFailure)
                    {
                        Logger.SimpleLog($"[AppPaths] Failed browser migration target cleanup failed: {cleanupFailure.Message}");
                    }

                    _browserCacheFolderForCurrentRun = LegacyBrowserCacheFolder;

                    if (!Directory.Exists(CanonicalBrowserCacheFolder))
                    {
                        try
                        {
                            File.Delete(BrowserCacheMigrationMarkerPath);
                        }
                        catch (Exception markerCleanupFailure)
                        {
                            Logger.SimpleLog($"[AppPaths] Browser migration marker cleanup failed: {markerCleanupFailure.Message}");
                        }
                    }
                }

                // 원본을 이번 실행의 프로필로 골랐으므로 다음 실행에서도 이전을 다시 시도할 수 있다
                Logger.SimpleLog($"[AppPaths] Browser cache migration failed: {ex.Message}");
            }
        }

        private static bool RecoverInterruptedBrowserMigration()
        {
            if (!File.Exists(BrowserCacheMigrationMarkerPath)) return true;

            try
            {
                var canonicalExists = Directory.Exists(CanonicalBrowserCacheFolder);
                var legacyExists = Directory.Exists(LegacyBrowserCacheFolder);

                if (!canonicalExists && !legacyExists)
                {
                    // 로밍 경로가 잠시 끊긴 경우 원본이 없다고 단정해 빈 프로필을 만들지 않는다.
                    // 표시를 남겨 다음 시작에서 원본이나 확정된 대상을 다시 확인한다
                    _browserCacheFolderForCurrentRun = LegacyBrowserCacheFolder;
                    Logger.SimpleLog("[AppPaths] Interrupted browser migration deferred until the legacy profile is accessible");
                    return false;
                }

                if (legacyExists)
                {
                    // 표시가 있는 동안 대상과 원본이 함께 남았다면 대상은 복사 뒤 아직 확정하지 못한
                    // 결과다. 원본만 보존하고 대상을 치운 뒤 처음부터 다시 이전한다
                    DeleteBrowserDataDirectory(CanonicalBrowserCacheFolder);
                    DeleteBrowserDataDirectory(BrowserCacheMigrationFolder);
                }

                File.Delete(BrowserCacheMigrationMarkerPath);
                Logger.SimpleLog("[AppPaths] Interrupted browser cache migration recovered");
                return true;
            }
            catch (Exception ex)
            {
                // 원본이 남아 있으면 이번 실행도 원본을 사용한다. 표시와 실패한 대상은 그대로 두어
                // 다음 시작에서 어느 쪽을 보존해야 하는지 다시 추측하지 않게 한다
                if (Directory.Exists(LegacyBrowserCacheFolder))
                {
                    _browserCacheFolderForCurrentRun = LegacyBrowserCacheFolder;
                }

                Logger.SimpleLog($"[AppPaths] Interrupted browser cache migration recovery failed: {ex.Message}");
                return false;
            }
        }

        private static void CopyBrowserCacheTransactionally()
        {
            if (Directory.Exists(BrowserCacheMigrationFolder))
            {
                DeleteBrowserDataDirectory(BrowserCacheMigrationFolder);
            }

            try
            {
                CopyDirectory(LegacyBrowserCacheFolder, BrowserCacheMigrationFolder);

                // 임시 폴더와 최종 폴더는 같은 LocalRoot에 있어 이름 변경으로 한 번에 전환된다
                Directory.Move(BrowserCacheMigrationFolder, CanonicalBrowserCacheFolder);
                Logger.SimpleLog($"[AppPaths] Browser cache copied to {CanonicalBrowserCacheFolder}");

                // 실제 데이터가 새 경로에 완전히 복사된 뒤에만 원본을 정리한다
                RetireLegacyBrowserCache();
            }
            catch
            {
                try
                {
                    if (Directory.Exists(BrowserCacheMigrationFolder))
                    {
                        DeleteBrowserDataDirectory(BrowserCacheMigrationFolder);
                    }
                }
                catch (Exception cleanupFailure)
                {
                    Logger.SimpleLog($"[AppPaths] Partial browser cache cleanup failed: {cleanupFailure.Message}");
                }

                throw;
            }
        }

        private static void CopyDirectory(string sourcePath, string destinationPath)
        {
            Directory.CreateDirectory(destinationPath);

            foreach (var filePath in Directory.EnumerateFiles(sourcePath))
            {
                var destinationFile = Path.Combine(destinationPath, Path.GetFileName(filePath));
                File.Copy(filePath, destinationFile, overwrite: false);
            }

            foreach (var directoryPath in Directory.EnumerateDirectories(sourcePath))
            {
                var attributes = File.GetAttributes(directoryPath);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException($"재분석 지점은 브라우저 프로필과 함께 복사할 수 없습니다: {directoryPath}");
                }

                var destinationDirectory = Path.Combine(destinationPath, Path.GetFileName(directoryPath));
                CopyDirectory(directoryPath, destinationDirectory);
            }
        }

        private static void RetireLegacyBrowserCache()
        {
            DeleteMigrationLeftovers();

            // 먼저 활성 경로 밖으로 이름을 바꾼다. 이후 삭제가 중단돼도 완전한 새 프로필을 쓰고,
            // 남은 사본은 다음 시작에서 이전 찌꺼기로 판별해 정리할 수 있다
            Directory.Move(LegacyBrowserCacheFolder, RetiredBrowserCacheFolder);

            try
            {
                DeleteBrowserDataDirectory(RetiredBrowserCacheFolder);
            }
            catch (Exception ex)
            {
                // 새 위치에는 완전한 복사본이 있다. 정리 사본만 다음 시작까지 남긴다
                Logger.SimpleLog($"[AppPaths] Old browser cache cleanup deferred: {ex.Message}");
            }
        }

        /// <summary>
        /// 이전이 중간에 끊겨 남은 사본을 치운다. 두 폴더 모두 내용이 새 위치에 이미 있으므로
        /// 지워도 잃는 것이 없다. 남겨 두면 복사본 하나만큼의 용량이 계속 붙어 있다
        /// </summary>
        private static void DeleteMigrationLeftovers()
        {
            foreach (var path in new[] { RetiredBrowserCacheFolder, BrowserCacheMigrationFolder })
            {
                if (Directory.Exists(path))
                {
                    DeleteBrowserDataDirectory(path);
                }
            }
        }

        private static bool DeleteBrowserDataDirectory(string path)
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
