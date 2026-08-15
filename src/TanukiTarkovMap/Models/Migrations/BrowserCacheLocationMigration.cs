using System.IO;
using TanukiTarkovMap.Models.Utils;

/**
BrowserCacheLocationMigration - 0.1.0 브라우저 프로필을 Roaming에서 Local로 이전

Purpose: 쿠키와 IndexedDB를 포함한 예전 CEF 프로필을 데이터 손실 없이 현재 위치로 옮기고,
이전 실패나 프로세스 중단 뒤에도 원본 또는 완전한 대상만 사용하게 한다.

Architecture: AppPaths.PrepareOnStartup()이 설정 이전 다음에 Migrate()를 호출한다. 현재 Local
프로필 경로와 삭제 도구는 AppPaths가 소유하고, 이 클래스는 예전 경로와 이전 작업 경로를 소유한다.

Core Functionality:
- Migrate(): 같은 볼륨에서는 폴더를 옮기고, 옮길 수 없으면 임시 폴더에 완전히 복사한 뒤 전환
- RecoverInterruptedBrowserMigration(): 중단 표시를 보고 원본 재시도 또는 완료된 대상 사용 결정
- DataFolders: AppPaths가 크기 측정, 캐시 비우기, 제거 훅에서 함께 다룰 이전 관련 폴더 제공
- MigrationMarkerPath: AppPaths의 명시적 캐시 비우기가 중단 표시까지 지울 수 있도록 경로 제공

State Management:
- LegacyBrowserCacheFolder: 0.1.0까지 사용한 Roaming 프로필 원본
- BrowserCacheMigrationFolder: 다른 볼륨에서 복사할 때 쓰는 Local 임시 폴더
- RetiredBrowserCacheFolder: 복사가 끝난 원본을 활성 경로 밖에서 정리하는 폴더
- MigrationMarkerPath: 이전 도중 프로세스가 끊겼음을 다음 시작에 알리는 디스크 표시
- AppPaths._browserCacheFolderForCurrentRun: 실패한 실행에서만 예전 원본 경로로 변경

Method Flow:
  AppPaths.PrepareOnStartup() -> Migrate() -> 중단 복구 -> 이동 또는 완전 복사 -> 표시 삭제
  이동/복사 실패 -> 미확정 Local 대상 삭제 -> 현재 실행의 프로필을 예전 원본으로 변경
  다음 시작 -> 중단 표시 확인 -> 원본이 남았으면 미확정 대상 삭제 후 처음부터 재시도

Key Methods:
- Migrate(): 이전 필요 여부를 판정하고 성공, 실패, 중단 복구 흐름을 조정
- CopyBrowserCacheTransactionally(): 임시 폴더 복사가 끝난 뒤 이름 변경으로 최종 경로 확정
- RetireLegacyBrowserCache(): 완전한 복사본이 생긴 뒤 예전 원본을 정리 경로로 옮겨 삭제
- DeleteMigrationLeftovers(): 현재 프로필이 확정된 뒤 남은 임시 폴더와 정리 폴더 삭제

Dependencies:
- AppPaths: 현재 경로, 실행 중 프로필 선택 상태, 브라우저 데이터 삭제 제공
- Logger: 이전과 복구 실패를 기록하되 앱 시작은 계속 진행

Design Rationale: Chromium 프로필은 파일 단위로 합칠 수 없으므로 원본을 통째로 이동한다.
다른 볼륨에서는 완전 복사와 같은 Local 볼륨 안의 이름 변경으로 확정 시점을 분리한다.
이전 경로 목록을 이 클래스가 제공해 지원 종료 시 AppPaths의 결합 지점이 컴파일 오류로 드러나게 한다.

Historical Context: 0.1.0까지 브라우저 프로필을 Roaming에 저장해 앱 제거 뒤에도 수백MB가 남고,
로밍 프로필 환경에서는 로그인마다 네트워크를 오갔다. 현재 버전은 Local 설치 루트에 저장한다.

Known Limitations: 중단 표시 없이 예전 프로필과 현재 프로필이 함께 있으면 최신 대상을 판정하거나
안전하게 합칠 수 없다. 두 폴더를 보존하고 AppPaths의 크기 표시와 캐시 비우기에서 함께 다룬다.

Edge Cases: 중단 표시가 있는데 원본과 대상이 모두 보이지 않으면 Roaming 경로가 잠시 끊긴 것으로
보고 빈 Local 프로필을 만들지 않는다. 원본이나 확정된 대상이 다시 보일 때까지 이전을 미룬다.

Critical Warnings: Migrate()는 CEF 초기화 전에만 호출한다. CEF가 프로필 파일을 연 뒤에는
이동과 삭제가 실패하며, 실패 처리에서 원본을 선택해도 이미 열린 프로필 경로를 바꿀 수 없다.

Last Updated: 2026-08-16 | .NET 8 | 브라우저 프로필 위치 이전 로직 분리
*/
namespace TanukiTarkovMap.Models.Migrations
{
    internal static class BrowserCacheLocationMigration
    {
        /// <summary> 0.1.0까지 캐시를 두던 자리 (Roaming) </summary>
        private static string LegacyBrowserCacheFolder => Path.Combine(AppPaths.RoamingRoot, "Cache");

        /// <summary> 다른 볼륨에서 프로필을 복사할 때만 쓰는 새 위치의 임시 폴더 </summary>
        private static string BrowserCacheMigrationFolder => Path.Combine(AppPaths.LocalRoot, "Cache.migrating");

        /// <summary> 복사가 끝난 예전 프로필을 활성 경로 밖에서 정리하는 폴더 </summary>
        private static string RetiredBrowserCacheFolder => Path.Combine(AppPaths.RoamingRoot, "Cache.migrated");

        /// <summary>
        /// 원본을 정리하기 전에 프로세스가 끊겼음을 다음 시작에 알리는 표시.
        /// 대상과 원본이 함께 있어도 어느 쪽이 이전 결과인지 추측하지 않게 한다
        /// </summary>
        internal static string MigrationMarkerPath => Path.Combine(AppPaths.LocalRoot, "Cache.migration-pending");

        /// <summary> AppPaths가 현재 프로필과 함께 관리해야 하는 이전 관련 폴더 목록 </summary>
        internal static IReadOnlyList<string> DataFolders =>
            [LegacyBrowserCacheFolder, RetiredBrowserCacheFolder, BrowserCacheMigrationFolder];

        internal static void Migrate()
        {
            if (!RecoverInterruptedBrowserMigration()) return;

            var canonicalExistedAtStart = Directory.Exists(AppPaths.CanonicalBrowserCacheFolder);

            try
            {
                if (canonicalExistedAtStart)
                {
                    DeleteMigrationLeftovers();
                    return;
                }

                if (!Directory.Exists(LegacyBrowserCacheFolder)) return;

                Directory.CreateDirectory(AppPaths.LocalRoot);
                File.WriteAllText(MigrationMarkerPath, string.Empty);

                try
                {
                    // 쿠키와 IndexedDB까지 들어 있어 버리지 않고 옮긴다.
                    // 같은 볼륨이면 수백MB여도 내용 복사 없이 끝난다
                    Directory.Move(LegacyBrowserCacheFolder, AppPaths.CanonicalBrowserCacheFolder);
                    Logger.SimpleLog($"[AppPaths] Browser cache moved to {AppPaths.CanonicalBrowserCacheFolder}");
                }
                catch (IOException moveFailure)
                {
                    // 로밍 폴더가 다른 볼륨이나 네트워크 공유에 있으면 이동할 수 없다. 다른 I/O 오류도
                    // 원본을 지울 근거가 아니므로, 임시 폴더에 완전히 복사한 뒤에만 새 위치를 쓴다
                    Logger.SimpleLog($"[AppPaths] Browser cache move failed ({moveFailure.Message}), copying safely");
                    CopyBrowserCacheTransactionally();
                }

                File.Delete(MigrationMarkerPath);
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
                        AppPaths.DeleteBrowserDataDirectory(AppPaths.CanonicalBrowserCacheFolder);
                    }
                    catch (Exception cleanupFailure)
                    {
                        Logger.SimpleLog($"[AppPaths] Failed browser migration target cleanup failed: {cleanupFailure.Message}");
                    }

                    AppPaths._browserCacheFolderForCurrentRun = LegacyBrowserCacheFolder;

                    if (!Directory.Exists(AppPaths.CanonicalBrowserCacheFolder))
                    {
                        try
                        {
                            File.Delete(MigrationMarkerPath);
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
            if (!File.Exists(MigrationMarkerPath)) return true;

            try
            {
                var canonicalExists = Directory.Exists(AppPaths.CanonicalBrowserCacheFolder);
                var legacyExists = Directory.Exists(LegacyBrowserCacheFolder);

                if (!canonicalExists && !legacyExists)
                {
                    // 로밍 경로가 잠시 끊긴 경우 원본이 없다고 단정해 빈 프로필을 만들지 않는다.
                    // 표시를 남겨 다음 시작에서 원본이나 확정된 대상을 다시 확인한다
                    AppPaths._browserCacheFolderForCurrentRun = LegacyBrowserCacheFolder;
                    Logger.SimpleLog("[AppPaths] Interrupted browser migration deferred until the legacy profile is accessible");
                    return false;
                }

                if (legacyExists)
                {
                    // 표시가 있는 동안 대상과 원본이 함께 남았다면 대상은 복사 뒤 아직 확정하지 못한
                    // 결과다. 원본만 보존하고 대상을 치운 뒤 처음부터 다시 이전한다
                    AppPaths.DeleteBrowserDataDirectory(AppPaths.CanonicalBrowserCacheFolder);
                    AppPaths.DeleteBrowserDataDirectory(BrowserCacheMigrationFolder);
                }

                File.Delete(MigrationMarkerPath);
                Logger.SimpleLog("[AppPaths] Interrupted browser cache migration recovered");
                return true;
            }
            catch (Exception ex)
            {
                // 원본이 남아 있으면 이번 실행도 원본을 사용한다. 표시와 실패한 대상은 그대로 두어
                // 다음 시작에서 어느 쪽을 보존해야 하는지 다시 추측하지 않게 한다
                if (Directory.Exists(LegacyBrowserCacheFolder))
                {
                    AppPaths._browserCacheFolderForCurrentRun = LegacyBrowserCacheFolder;
                }

                Logger.SimpleLog($"[AppPaths] Interrupted browser cache migration recovery failed: {ex.Message}");
                return false;
            }
        }

        private static void CopyBrowserCacheTransactionally()
        {
            if (Directory.Exists(BrowserCacheMigrationFolder))
            {
                AppPaths.DeleteBrowserDataDirectory(BrowserCacheMigrationFolder);
            }

            try
            {
                CopyDirectory(LegacyBrowserCacheFolder, BrowserCacheMigrationFolder);

                // 임시 폴더와 최종 폴더는 같은 LocalRoot에 있어 이름 변경으로 한 번에 전환된다
                Directory.Move(BrowserCacheMigrationFolder, AppPaths.CanonicalBrowserCacheFolder);
                Logger.SimpleLog($"[AppPaths] Browser cache copied to {AppPaths.CanonicalBrowserCacheFolder}");

                // 실제 데이터가 새 경로에 완전히 복사된 뒤에만 원본을 정리한다
                RetireLegacyBrowserCache();
            }
            catch
            {
                try
                {
                    if (Directory.Exists(BrowserCacheMigrationFolder))
                    {
                        AppPaths.DeleteBrowserDataDirectory(BrowserCacheMigrationFolder);
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
                AppPaths.DeleteBrowserDataDirectory(RetiredBrowserCacheFolder);
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
                    AppPaths.DeleteBrowserDataDirectory(path);
                }
            }
        }
    }
}
