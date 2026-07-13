using CommunityToolkit.Mvvm.Messaging;
using TanukiTarkovMap.Messages;
using TanukiTarkovMap.Models.Utils;
using Velopack;
using Velopack.Sources;

/**
UpdateService - Velopack 백그라운드 업데이트 서비스

Purpose: 업데이트 확인/다운로드가 앱 시작을 막지 않도록 백그라운드에서 처리하고,
적용은 사용자 선택(즉시 재시작) 또는 앱 종료 시점으로 미룬다 (디스코드 방식)

Architecture: App 시작 완료 후 CheckAndDownloadAsync()가 fire-and-forget으로 실행된다.
다운로드가 끝나면 UpdateReadyMessage를 발행해 TopBar에 표시를 띄우고, 사용자가
(a) 표시를 클릭하면 즉시 재시작 적용, (b) 그냥 종료하면 종료 후 조용히 적용된다.

Method Flow:
  CheckAndDownloadAsync -> CheckForUpdatesAsync -> DownloadUpdatesAsync -> UpdateReadyMessage 발행
  ApplyAndRestartNow (TopBar 클릭) -> ApplyUpdatesAndRestart (즉시 재시작)
  ApplyOnExit (앱 종료 시) -> WaitExitThenApplyUpdates(restart: false) -> 다음 실행이 새 버전

State Management:
- _pendingUpdate: 다운로드까지 완료된 업데이트 (null이면 적용 대기 중인 업데이트 없음)

Dependencies:
- Velopack UpdateManager/GithubSource: 릴리스 조회, delta/full 패키지 다운로드, Update.exe 예약
- WeakReferenceMessenger: MainWindowViewModel로 UpdateReadyMessage 전달

Historical Context: 이전에는 시작 시 스플래시에서 확인/다운로드를 블로킹으로 수행하고
즉시 강제 재시작했다. 업데이트가 없어도 GitHub API 왕복만큼 시작이 늦어지고,
있으면 다운로드 전체를 기다려야 해서 백그라운드 방식으로 전환했다 (2026-07).
*/
namespace TanukiTarkovMap.Models.Services
{
    public class UpdateService
    {
        /// <summary> 업데이트 조회에 사용하는 GitHub 저장소 주소 (App의 버전 표시에도 사용) </summary>
        internal const string GitHubRepoUrl = "https://github.com/siakun/TanukiTarkovMap";

        private UpdateManager? _updateManager;
        private UpdateInfo? _pendingUpdate;

        /// <summary>
        /// DI 컨테이너 전용 생성자 - 외부에서 new 사용 금지
        /// ServiceLocator.CreateInstance()를 통해서만 생성
        /// </summary>
        internal UpdateService() { }

        /// <summary> 다운로드까지 완료되어 적용 대기 중인 업데이트가 있는지 여부 </summary>
        public bool UpdateDownloaded => _pendingUpdate != null;

        /// <summary>
        /// 백그라운드에서 업데이트를 확인하고 있으면 다운로드까지 마친다.
        /// 완료 시 UpdateReadyMessage를 발행한다. 실패해도 앱 동작에는 영향이 없다
        /// </summary>
        public async Task CheckAndDownloadAsync()
        {
            try
            {
                _updateManager = new UpdateManager(new GithubSource(GitHubRepoUrl, null, false));

                // Velopack으로 설치되지 않은 경우 (개발 모드/포터블) 스킵
                if (!_updateManager.IsInstalled)
                {
                    Logger.SimpleLog("[UpdateService] Skipped (not installed via Velopack)");
                    return;
                }

                Logger.SimpleLog("[UpdateService] Checking for updates in background...");
                var updateInfo = await _updateManager.CheckForUpdatesAsync();
                if (updateInfo == null)
                {
                    Logger.SimpleLog("[UpdateService] Already up to date");
                    return;
                }

                var targetVersion = updateInfo.TargetFullRelease.Version.ToString();
                Logger.SimpleLog($"[UpdateService] Update found: v{targetVersion}, downloading in background...");

                await _updateManager.DownloadUpdatesAsync(updateInfo);
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
        /// 대기 중인 업데이트를 즉시 적용하고 재시작한다 (TopBar 업데이트 표시 클릭 시)
        /// </summary>
        public void ApplyAndRestartNow()
        {
            if (_updateManager == null || _pendingUpdate == null)
                return;

            Logger.SimpleLog("[UpdateService] Applying update now and restarting...");
            _updateManager.ApplyUpdatesAndRestart(_pendingUpdate.TargetFullRelease);
        }

        /// <summary>
        /// 앱 종료 시 대기 중인 업데이트가 있으면 종료 후 조용히 적용되도록 예약한다.
        /// 다음 실행부터 새 버전으로 시작된다
        /// </summary>
        public void ApplyOnExit()
        {
            if (_updateManager == null || _pendingUpdate == null)
                return;

            try
            {
                Logger.SimpleLog("[UpdateService] Scheduling update apply after exit");
                _updateManager.WaitExitThenApplyUpdates(_pendingUpdate.TargetFullRelease, silent: true, restart: false);
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[UpdateService] ApplyOnExit failed: {ex.Message}");
            }
        }
    }
}
