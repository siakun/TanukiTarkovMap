using CommunityToolkit.Mvvm.Messaging;
using NuGet.Versioning;
using TanukiTarkovMap.Messages;
using TanukiTarkovMap.Models.Utils;
using Velopack;
using Velopack.Sources;

/**
UpdateService - Velopack 업데이트 서비스 (자동 갱신과 버전 전환)

Purpose: 업데이트 확인/다운로드가 앱 시작을 막지 않도록 백그라운드에서 처리하고,
적용은 사용자 선택(즉시 재시작) 또는 앱 종료 시점으로 미룬다 (디스코드 방식).
설정에서 원하는 버전을 직접 골라 설치하는 경로도 함께 제공한다.

Architecture: 두 경로가 서로 다른 업데이트 소스를 쓴다.
- 자동 갱신: GithubSource + CheckForUpdatesAsync. 최신 릴리스만 보는 대신 delta를 받는다
- 버전 전환: GitHubReleaseSource + 직접 조립한 UpdateInfo. 임의 태그를 설치할 수 있는 대신
  full 패키지를 받는다

Core Functionality:
- CheckAndDownloadAsync: 앱 시작 후 fire-and-forget. 설정에서 자동 업데이트를 끄면 건너뛴다.
  이전 실행에서 받아만 두고 적용하지 않은 업데이트가 있으면 다시 받지 않고 표시만 되살린다
- GetAvailableVersionsAsync: 설정 화면의 버전 목록을 만든다
- InstallVersionAsync: 고른 버전을 받아 즉시 재시작으로 적용한다. 다운그레이드도 이 경로다

State Management:
- _autoUpdateManager: 자동 갱신용 UpdateManager. 현재 설치 버전 조회에도 함께 쓴다
- _pendingUpdate: 다운로드까지 끝나 적용을 기다리는 업데이트 (null이면 대기 중인 것이 없음)

Method Flow:
  CheckAndDownloadAsync -> CheckForUpdatesAsync -> DownloadUpdatesAsync -> UpdateReadyMessage 발행
  ApplyAndRestartNow (타이틀바 아이콘 클릭) -> ApplyUpdatesAndRestart (즉시 재시작)
  ApplyOnExit (앱 종료 시) -> WaitExitThenApplyUpdates(restart: false) -> 다음 실행이 새 버전
  InstallVersionAsync (설정에서 버전 선택) -> DownloadUpdatesAsync(progress) -> ApplyUpdatesAndRestart

Dependencies:
- Velopack UpdateManager: 릴리스 조회, 패키지 다운로드와 SHA 검증, Update.exe 예약
- GitHubReleaseSource: 임의 태그를 설치 대상으로 만드는 업데이트 소스
- WeakReferenceMessenger: MainWindowViewModel로 UpdateReadyMessage 전달

Historical Context: 이전에는 시작 시 스플래시에서 확인/다운로드를 블로킹으로 수행하고
즉시 강제 재시작했다. 업데이트가 없어도 GitHub API 왕복만큼 시작이 늦어지고,
있으면 다운로드 전체를 기다려야 해서 백그라운드 방식으로 전환했다 (2026-07).

Critical Warnings: 최신이 아닌 버전을 설치할 때는 호출 측에서 자동 업데이트를 꺼야 한다.
켜둔 채로 두면 다음 실행에서 곧바로 최신으로 되돌아가 사용자가 고른 버전이 사라진다.

Known Limitations: 자동 업데이트를 끄는 설정은 이 코드가 들어간 버전부터 유효하다.
AutoUpdateEnabled를 모르는 옛 버전으로 내려가면 그 버전의 업데이트 서비스가 설정을 보지 않고
최신을 받아 오므로, 사용자는 그 버전에 머무를 수 없다. 이미 배포된 코드는 고칠 수 없어
설계로 막을 방법이 없으므로 설정 화면에서 그 사실을 미리 알린다.

Last Updated: 2026-08-14 | .NET 8 / Velopack 0.0.1298 | 버전 전환 기능 도입
*/
namespace TanukiTarkovMap.Models.Services
{
    public class UpdateService
    {
        /// <summary> 업데이트 조회에 사용하는 GitHub 저장소 주소 (App의 버전 표시에도 사용) </summary>
        internal const string GitHubRepoUrl = "https://github.com/Siakun/TanukiTarkovMap";

        private UpdateManager? _autoUpdateManager;
        private UpdateInfo? _pendingUpdate;

        /// <summary>
        /// DI 컨테이너 전용 생성자 - 외부에서 new 사용 금지
        /// ServiceLocator.CreateInstance()를 통해서만 생성
        /// </summary>
        internal UpdateService() { }

        /// <summary> 다운로드까지 완료되어 적용 대기 중인 업데이트가 있는지 여부 </summary>
        public bool UpdateDownloaded => _pendingUpdate != null;

        /// <summary>
        /// Velopack으로 설치된 실행 파일인지 여부.
        /// 포터블 압축본이나 개발 빌드에서는 false이고, 이때는 버전을 바꿀 수 없다
        /// </summary>
        public bool IsManagedInstall
        {
            get
            {
                try { return AutoUpdateManager.IsInstalled; }
                catch { return false; }
            }
        }

        /// <summary> 현재 설치된 버전. 관리되는 설치가 아니면 null </summary>
        public SemanticVersion? CurrentVersion
        {
            get
            {
                try { return AutoUpdateManager.IsInstalled ? AutoUpdateManager.CurrentVersion : null; }
                catch { return null; }
            }
        }

        /// <summary>
        /// 자동 갱신용 UpdateManager. 생성 자체는 로컬 정보만 읽으므로 네트워크를 타지 않는다
        /// </summary>
        private UpdateManager AutoUpdateManager
            => _autoUpdateManager ??= new UpdateManager(new GithubSource(GitHubRepoUrl, null, false));

        /// <summary>
        /// 백그라운드에서 업데이트를 확인하고 있으면 다운로드까지 마친다.
        /// 완료 시 UpdateReadyMessage를 발행한다. 실패해도 앱 동작에는 영향이 없다
        /// </summary>
        public async Task CheckAndDownloadAsync()
        {
            try
            {
                // Velopack으로 설치되지 않은 경우 (개발 모드/포터블) 스킵
                if (!AutoUpdateManager.IsInstalled)
                {
                    Logger.SimpleLog("[UpdateService] Skipped (not installed via Velopack)");
                    return;
                }

                // 지난 실행에서 받아만 두고 적용하지 않은 업데이트는 다시 받지 않는다.
                // 자동 업데이트를 끈 뒤에도 이미 받아 둔 것은 적용할 수 있어야 하므로 설정보다 먼저 본다
                var pendingAsset = AutoUpdateManager.UpdatePendingRestart;
                if (pendingAsset != null)
                {
                    _pendingUpdate = new UpdateInfo(pendingAsset, isDowngrade: false);

                    var pendingVersion = pendingAsset.Version.ToString();
                    Logger.SimpleLog($"[UpdateService] Update v{pendingVersion} was already downloaded, waiting to apply");
                    WeakReferenceMessenger.Default.Send(new UpdateReadyMessage(pendingVersion));
                    return;
                }

                if (!App.GetSettings().AutoUpdateEnabled)
                {
                    Logger.SimpleLog("[UpdateService] Skipped (auto update disabled in settings)");
                    return;
                }

                Logger.SimpleLog("[UpdateService] Checking for updates in background...");
                var updateInfo = await AutoUpdateManager.CheckForUpdatesAsync();
                if (updateInfo == null)
                {
                    Logger.SimpleLog("[UpdateService] Already up to date");
                    return;
                }

                var targetVersion = updateInfo.TargetFullRelease.Version.ToString();
                Logger.SimpleLog($"[UpdateService] Update found: v{targetVersion}, downloading in background...");

                await AutoUpdateManager.DownloadUpdatesAsync(updateInfo);
                _pendingUpdate = updateInfo;

                Logger.SimpleLog($"[UpdateService] Update v{targetVersion} ready (will apply on exit)");
                WeakReferenceMessenger.Default.Send(new UpdateReadyMessage(targetVersion));
            }
            catch (Exception ex)
            {
                // 업데이트 실패는 치명적이지 않다. 다음 실행에서 다시 시도된다
                Logger.SimpleLog($"[UpdateService] Update check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 설치할 수 있는 버전 목록을 최신 순으로 가져온다 (설정 화면의 버전 목록).
        /// 네트워크 실패는 호출 측에서 사용자에게 알린다
        /// </summary>
        public Task<IReadOnlyList<ReleaseVersion>> GetAvailableVersionsAsync(CancellationToken cancelToken = default)
            => GitHubReleaseSource.FetchVersionsAsync(GitHubRepoUrl, cancelToken);

        /// <summary>
        /// 지정한 버전을 내려받아 즉시 재시작으로 적용한다. 다운그레이드도 이 경로를 쓴다.
        /// 다운로드가 끝나면 프로세스가 교체되므로 이 메서드는 정상 흐름에서 반환하지 않는다
        /// </summary>
        public async Task InstallVersionAsync(
            ReleaseVersion target,
            Action<int>? progress = null,
            CancellationToken cancelToken = default)
        {
            var source = new GitHubReleaseSource(target);

            // 낮은 버전 설치는 UpdateOptions로 명시해야 Velopack이 막지 않는다
            var manager = new UpdateManager(source, new UpdateOptions { AllowVersionDowngrade = true });

            if (!manager.IsInstalled)
            {
                throw new InvalidOperationException("Velopack으로 설치된 실행 파일에서만 버전을 바꿀 수 있습니다.");
            }

            var asset = await source.GetTargetAssetAsync();
            var isDowngrade = manager.CurrentVersion != null
                              && target.Version.CompareTo(manager.CurrentVersion) < 0;

            Logger.SimpleLog($"[UpdateService] Installing v{target.Version} (downgrade: {isDowngrade})");

            var updateInfo = new UpdateInfo(asset, isDowngrade);
            await manager.DownloadUpdatesAsync(updateInfo, progress, cancelToken);

            Logger.SimpleLog($"[UpdateService] v{target.Version} downloaded, restarting to apply");
            manager.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease);
        }

        /// <summary>
        /// 대기 중인 업데이트를 즉시 적용하고 재시작한다 (타이틀바 업데이트 아이콘 클릭 시)
        /// </summary>
        public void ApplyAndRestartNow()
        {
            if (_pendingUpdate == null)
                return;

            Logger.SimpleLog("[UpdateService] Applying update now and restarting...");
            AutoUpdateManager.ApplyUpdatesAndRestart(_pendingUpdate.TargetFullRelease);
        }

        /// <summary>
        /// 앱 종료 시 대기 중인 업데이트가 있으면 종료 후 조용히 적용되도록 예약한다.
        /// 다음 실행부터 새 버전으로 시작된다
        /// </summary>
        public void ApplyOnExit()
        {
            if (_pendingUpdate == null)
                return;

            try
            {
                Logger.SimpleLog("[UpdateService] Scheduling update apply after exit");
                AutoUpdateManager.WaitExitThenApplyUpdates(_pendingUpdate.TargetFullRelease, silent: true, restart: false);
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[UpdateService] ApplyOnExit failed: {ex.Message}");
            }
        }
    }
}
