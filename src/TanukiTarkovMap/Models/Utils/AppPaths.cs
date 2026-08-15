using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

/**
AppPaths - 앱이 사용자 폴더에 두는 파일의 위치를 정하는 단일 출처

Purpose: 설정과 브라우저 캐시를 어느 폴더에 둘지, 예전 위치에 남은 파일을 어떻게 옮길지
한 곳에서 정한다. 이전에는 같은 경로 계산이 Settings, SettingsViewModel, App에 흩어져 있었다.

Architecture: 두 폴더의 역할이 서로 반대다.
- 설정(Roaming): 앱을 지워도 남아야 한다. Velopack 설치 폴더 밖이라 제거 대상이 아니고,
  다시 설치하면 그대로 이어 쓴다
- 브라우저 캐시(Local): 앱을 지우면 함께 사라져야 한다. Velopack 설치 폴더 안이므로
  Update.exe --uninstall이 그 폴더를 지울 때 같이 정리된다

Core Functionality:
- SettingsFilePath: settings.json의 위치
- BrowserCacheFolder: CefSharp에 넘기는 프로필 폴더 (캐시뿐 아니라 쿠키와 IndexedDB도 들어간다)
- PrepareOnStartup(): 예전 위치의 파일을 넘겨받고, 불어난 코드 캐시를 비운다
- GetBrowserCacheSize() / DeleteBrowserCacheIfRequested(): 설정 화면의 캐시 표시와 비우기

Method Flow:
  앱 시작 -> PrepareOnStartup -> (설정 병합, 캐시 이전, 코드 캐시 정리) -> InitializeCef
  앱 종료 -> Cef.Shutdown -> DeleteBrowserCacheIfRequested

Historical Context: 0.1.0까지는 두 폴더가 정반대였다. 설정이 Velopack 설치 폴더 안(Local)에
있어 앱을 제거하면 맵별 창 위치까지 함께 사라졌고, 브라우저 캐시는 Roaming에 쌓여 제거한
뒤에도 수백MB가 남았다. 로밍 프로필을 쓰는 환경에서는 그 캐시가 로그인마다 네트워크를 오갔다.

Design Rationale: 설정은 예전 파일을 남긴 채 현재 파일과 병합한다. 지난 버전은 예전 위치만
보기 때문에 원본이 필요하고, 파일 전체를 덮어쓰면 지난 버전이 모르는 새 설정이 사라진다.
캐시는 쿠키와 IndexedDB까지 담고 있어 같은 볼륨에서는 통째로 옮기고, 이동할 수 없으면
임시 폴더에 완전히 복사한 뒤 새 위치로 전환한다. 버전을 내렸다가 돌아오면 두 프로필이 다시
생길 수 있으므로, 두 폴더가 함께 있다는 이유만으로 어느 쪽도 지우거나 합치지 않는다.

Critical Warnings: PrepareOnStartup()은 CEF 초기화 전에 불러야 한다.
CEF가 캐시 폴더를 열고 나면 폴더를 옮기거나 지울 수 없어 조용히 실패한다.

Last Updated: 2026-08-15 | .NET 8 | 버전 왕복 시 설정과 브라우저 데이터 보존
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
        /// CefSharp에 넘기는 프로필 폴더. 앱을 제거하면 함께 사라진다.
        /// 이름은 캐시지만 쿠키와 IndexedDB 같은 사이트 데이터도 이 안에 들어간다
        /// </summary>
        public static string BrowserCacheFolder => Path.Combine(LocalRoot, "Cache");

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
        /// 앱을 닫을 때 브라우저 캐시를 비울지 여부.
        /// 실행 중에는 CEF가 프로필 파일을 붙들고 있어 그 자리에서 지울 수 없다
        /// </summary>
        public static bool BrowserCacheResetRequested { get; set; }

        /// <summary>
        /// 브라우저 캐시 폴더가 지금 차지하는 크기(byte). 폴더가 없으면 0.
        /// 파일 수천 개를 훑으므로 UI 스레드에서 직접 부르지 않는다
        /// </summary>
        public static long GetBrowserCacheSize()
        {
            try
            {
                var folder = new DirectoryInfo(BrowserCacheFolder);
                if (!folder.Exists) return 0;

                return folder.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[AppPaths] Browser cache size check failed: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 예약돼 있으면 브라우저 캐시를 지운다. Cef.Shutdown() 뒤에 호출해야 파일이 풀려 있다.
        /// 지우지 못해도 다음에 다시 예약하면 되므로 예외를 밖으로 던지지 않는다
        /// </summary>
        public static void DeleteBrowserCacheIfRequested()
        {
            if (!BrowserCacheResetRequested) return;
            BrowserCacheResetRequested = false;

            try
            {
                if (!Directory.Exists(BrowserCacheFolder)) return;

                Directory.Delete(BrowserCacheFolder, recursive: true);
                Logger.SimpleLog("[AppPaths] Browser cache cleared");
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[AppPaths] Browser cache clear failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 앱을 시작할 때 사용자 폴더를 정돈한다. CEF가 캐시 폴더를 열기 전에 호출해야 한다.
        /// 폴더를 옮기는 일과 비우는 일은 순서를 지켜야 하므로 호출 측이 나눠 부르지 않게 묶어 둔다
        /// </summary>
        public static void PrepareOnStartup()
        {
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

                // 예전 위치를 지우지 않으므로 두 파일이 함께 남는다. 지난 버전으로 되돌리면 그 버전이
                // 예전 파일을 고치고, 다시 올라오면 이 자리에서 그 변경을 가져와야 한다.
                // 새 파일이 있다는 이유만으로 건너뛰면 버전을 오갈 때 설정이 두 갈래로 갈라진다
                var current = new FileInfo(SettingsFilePath);
                if (current.Exists && current.LastWriteTimeUtc >= legacy.LastWriteTimeUtc) return;

                var merged = current.Exists
                    ? ReadSettingsObject(SettingsFilePath)
                    : new JsonObject();
                var legacySettings = ReadSettingsObject(LegacySettingsFilePath);

                // 예전 버전이 아는 값만 최신 값으로 덮고, 그 버전에 없던 새 속성은 현재 파일에 남긴다.
                // 중첩된 설정도 같은 규칙으로 합쳐 이후에 필드가 늘어도 되돌아간 버전이 지우지 않는다
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
            try
            {
                if (Directory.Exists(BrowserCacheFolder))
                {
                    DeleteRetiredBrowserCache();

                    if (!Directory.Exists(LegacyBrowserCacheFolder)) return;

                    // 지난 버전은 예전 경로를 사용하므로 버전을 내렸다가 돌아오면 두 폴더가 함께 생긴다.
                    // Chromium 프로필은 안전하게 합칠 수 없고 쿠키와 IndexedDB도 들어 있으므로 보존한다
                    return;
                }

                if (!Directory.Exists(LegacyBrowserCacheFolder)) return;

                Directory.CreateDirectory(LocalRoot);

                try
                {
                    // 쿠키와 IndexedDB까지 들어 있어 버리지 않고 옮긴다.
                    // 같은 볼륨이면 수백MB여도 내용 복사 없이 끝난다
                    Directory.Move(LegacyBrowserCacheFolder, BrowserCacheFolder);
                    Logger.SimpleLog($"[AppPaths] Browser cache moved to {BrowserCacheFolder}");
                }
                catch (IOException moveFailure)
                {
                    // 로밍 폴더가 다른 볼륨이나 네트워크 공유에 있으면 이동할 수 없다. 다른 I/O 오류도
                    // 원본을 지울 근거가 아니므로, 임시 폴더에 완전히 복사한 뒤에만 새 위치를 쓴다
                    Logger.SimpleLog($"[AppPaths] Browser cache move failed ({moveFailure.Message}), copying safely");
                    CopyBrowserCacheTransactionally();
                }
            }
            catch (Exception ex)
            {
                // 원본은 그대로 둔다. 새 위치가 만들어지지 않았으면 다음 실행에서 다시 시도한다
                Logger.SimpleLog($"[AppPaths] Browser cache migration failed: {ex.Message}");
            }
        }

        private static void CopyBrowserCacheTransactionally()
        {
            if (Directory.Exists(BrowserCacheMigrationFolder))
            {
                Directory.Delete(BrowserCacheMigrationFolder, recursive: true);
            }

            try
            {
                CopyDirectory(LegacyBrowserCacheFolder, BrowserCacheMigrationFolder);

                // 임시 폴더와 최종 폴더는 같은 LocalRoot에 있어 이름 변경으로 한 번에 전환된다
                Directory.Move(BrowserCacheMigrationFolder, BrowserCacheFolder);
                Logger.SimpleLog($"[AppPaths] Browser cache copied to {BrowserCacheFolder}");
            }
            catch
            {
                try
                {
                    if (Directory.Exists(BrowserCacheMigrationFolder))
                    {
                        Directory.Delete(BrowserCacheMigrationFolder, recursive: true);
                    }
                }
                catch (Exception cleanupFailure)
                {
                    Logger.SimpleLog($"[AppPaths] Partial browser cache cleanup failed: {cleanupFailure.Message}");
                }

                throw;
            }

            RetireLegacyBrowserCache();
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
            try
            {
                DeleteRetiredBrowserCache();

                // 먼저 활성 경로 밖으로 이름을 바꾼다. 이후 삭제가 중단돼도 지난 버전은
                // 반쯤 지워진 프로필 대신 새 프로필을 만든다
                Directory.Move(LegacyBrowserCacheFolder, RetiredBrowserCacheFolder);
                Directory.Delete(RetiredBrowserCacheFolder, recursive: true);
            }
            catch (Exception ex)
            {
                // 새 위치에는 완전한 복사본이 있다. 예전 복사본은 지우지 못한 상태로만 남긴다
                Logger.SimpleLog($"[AppPaths] Old browser cache cleanup deferred: {ex.Message}");
            }
        }

        private static void DeleteRetiredBrowserCache()
        {
            if (Directory.Exists(RetiredBrowserCacheFolder))
            {
                Directory.Delete(RetiredBrowserCacheFolder, recursive: true);
            }
        }
    }
}
