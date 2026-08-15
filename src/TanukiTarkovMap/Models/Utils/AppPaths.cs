using System.IO;

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
  앱 시작 -> PrepareOnStartup -> (설정 복사, 캐시 이동, 코드 캐시 정리) -> InitializeCef
  앱 종료 -> Cef.Shutdown -> DeleteBrowserCacheIfRequested

Historical Context: 0.1.0까지는 두 폴더가 정반대였다. 설정이 Velopack 설치 폴더 안(Local)에
있어 앱을 제거하면 맵별 창 위치까지 함께 사라졌고, 브라우저 캐시는 Roaming에 쌓여 제거한
뒤에도 수백MB가 남았다. 로밍 프로필을 쓰는 환경에서는 그 캐시가 로그인마다 네트워크를 오갔다.

Design Rationale: 설정은 옮기지 않고 복사한다. 이 앱에는 지난 버전으로 되돌리는 기능이 있고
되돌아간 버전은 예전 위치만 보기 때문에, 원본을 지우면 다운그레이드한 사용자가 설정을 잃는다.
캐시는 사이트가 저장한 쿠키와 IndexedDB까지 담고 있어 지우지 않고 통째로 옮긴다.
두 폴더가 같은 볼륨(AppData 아래)이라 이동은 내용 복사 없이 끝난다.

Critical Warnings: PrepareOnStartup()은 CEF 초기화 전에 불러야 한다.
CEF가 캐시 폴더를 열고 나면 폴더를 옮기거나 지울 수 없어 조용히 실패한다.

Last Updated: 2026-08-14 | .NET 8 | 설정과 캐시 폴더의 역할 교환
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

        /// <summary> settings.json 전체 경로 </summary>
        public static string SettingsFilePath => Path.Combine(SettingsFolder, "settings.json");

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
                // 이미 넘어왔거나 넘길 것이 없으면 손대지 않는다
                if (File.Exists(SettingsFilePath)) return;
                if (!File.Exists(LegacySettingsFilePath)) return;

                Directory.CreateDirectory(SettingsFolder);

                // 옮기지 않고 복사한다. 지난 버전으로 되돌리면 그 버전은 예전 위치만 보기 때문이다
                File.Copy(LegacySettingsFilePath, SettingsFilePath, overwrite: false);
                Logger.SimpleLog($"[AppPaths] Settings copied to {SettingsFilePath}");
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[AppPaths] Settings migration failed: {ex.Message}");
            }
        }

        private static void MigrateBrowserCache()
        {
            try
            {
                if (Directory.Exists(BrowserCacheFolder)) return;
                if (!Directory.Exists(LegacyBrowserCacheFolder)) return;

                Directory.CreateDirectory(LocalRoot);

                // 쿠키와 IndexedDB까지 들어 있어 버리지 않고 옮긴다.
                // 같은 볼륨이라 수백MB여도 내용 복사 없이 끝난다
                Directory.Move(LegacyBrowserCacheFolder, BrowserCacheFolder);
                Logger.SimpleLog($"[AppPaths] Browser cache moved to {BrowserCacheFolder}");
            }
            catch (Exception ex)
            {
                // 옮기지 못하면 CEF가 새 위치에 프로필을 새로 만든다. 방문 기록만 잃고 동작은 이어진다
                Logger.SimpleLog($"[AppPaths] Browser cache migration failed: {ex.Message}");
            }
        }
    }
}
