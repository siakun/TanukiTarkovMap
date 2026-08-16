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
- 자동 갱신: GithubSource + CheckForUpdatesAsync. 최신 릴리스만 보는 대신 delta를 받는다.
  베타를 켜면 GithubSource가 프리릴리스까지 조회 대상에 넣는다
- 버전 전환: GitHubReleaseSource + 직접 조립한 UpdateInfo. 임의 태그를 설치할 수 있다.
  올라가는 방향이고 사슬이 이어지면 delta를 이어 붙이고, 아니면 full을 받는다

Core Functionality:
- CheckAndDownloadAsync: 앱 시작 후 fire-and-forget. 설정에서 자동 업데이트를 끄면 건너뛴다.
  이전 실행에서 받아만 두고 적용하지 않은 업데이트가 있으면 다시 받지 않고 표시만 되살린다
- GetAvailableVersionsAsync: 설정 화면의 버전 목록을 만든다
- InstallVersionAsync: 고른 버전을 받고 앱의 정상 종료를 요청한다. 다운그레이드도 이 경로다

State Management:
- _autoUpdateManager: 자동 갱신용 UpdateManager. 현재 설치 버전 조회에도 함께 쓴다
- _pendingUpdate: 다운로드까지 끝나 적용을 기다리는 업데이트 (null이면 대기 중인 것이 없음)
- _pendingUpdateManager: 그 업데이트를 받은 소스와 옵션을 가진 UpdateManager
- _restartAfterApply: 즉시 적용을 골랐으면 true, 평소 종료 적용이면 false
- _manualVersionSwitchInProgress: 수동 다운로드 동안 자동 결과가 적용 대상을 덮지 못하게 하는 상태
- _applyScheduled: 같은 패키지에 updater 프로세스를 두 번 띄우지 않게 하는 상태

Method Flow:
  CheckAndDownloadAsync -> CheckForUpdatesAsync -> DownloadUpdatesAsync -> UpdateReadyMessage 발행
  ApplyAndRestartNow (타이틀바 아이콘 클릭) -> RestartRequested -> 앱 정상 종료
  ApplyOnExit (앱 종료 시) -> WaitExitThenApplyUpdates -> 즉시 적용을 골랐으면 재시작
  InstallVersionAsync (설정에서 버전 선택) -> DownloadUpdatesAsync(progress) -> 앱 정상 종료 후 적용

Key Methods:
- CheckAndDownloadAsync(): 자동 업데이트를 확인하고 적용 대기 상태로 저장
- InstallVersionAsync(target, progress, onDownloadSizeResolved, cancelToken): 고른 태그를 받고 정상 종료 요청.
  올라가는 방향이고 사슬이 이어지면 delta로, 아니면 full로 받는다
- ApplyAndRestartNow(): 준비된 자동 업데이트를 재시작 적용으로 전환
- ApplyOnExit(): 저장된 UpdateManager와 자산으로 종료 대기 updater 프로세스 예약

Dependencies:
- Velopack UpdateManager: 릴리스 조회, 패키지 다운로드와 SHA 검증, Update.exe 예약
- GitHubReleaseSource: 임의 태그를 설치 대상으로 만드는 업데이트 소스
- WeakReferenceMessenger: MainWindowViewModel로 UpdateReadyMessage 전달

Design Rationale: 자동 갱신과 임의 버전 전환은 조회 대상과 UpdateOptions가 달라 각 패키지를
받은 UpdateManager를 적용 시점까지 함께 보관한다. 업데이트가 프로세스를 직접 종료하게 두지
않고 App에 종료를 요청해 CEF와 파일 감시 서비스를 먼저 정리한다. 자동 다운로드와 수동 전환이
겹치면 사용자가 고른 버전에 우선권을 주어 마지막 비동기 완료 순서가 적용 대상을 바꾸지 못하게 한다.

Historical Context: 이전에는 시작 시 스플래시에서 확인/다운로드를 블로킹으로 수행하고
즉시 강제 재시작했다. 업데이트가 없어도 GitHub API 왕복만큼 시작이 늦어지고,
있으면 다운로드 전체를 기다려야 해서 백그라운드 방식으로 전환했다 (2026-07).

Critical Warnings: UpdateInfo.BaseRelease를 null로 두면 Velopack이 delta를 통째로 건너뛴다.
문서에는 null일 때 설치된 버전에서 시도한다고 적혀 있지만 구현은 그렇지 않다.
최신이 아닌 버전을 설치할 때는 호출 측에서 자동 업데이트를 꺼야 한다.
켜둔 채로 두면 다음 실행에서 곧바로 최신으로 되돌아가 사용자가 고른 버전이 사라진다.
ApplyUpdatesAndRestart는 Environment.Exit을 호출해 Cef.Shutdown을 건너뛰므로 사용하지 않는다.
Program의 Velopack 시작 시 자동 적용도 끄고, 모든 적용을 ApplyOnExit 한 경로로 모은다.

Known Limitations: 자동 업데이트를 끄는 설정은 이 코드가 들어간 버전부터 유효하다.
AutoUpdateEnabled를 모르는 옛 버전으로 내려가면 그 버전의 업데이트 서비스가 설정을 보지 않고
최신을 받아 오므로, 사용자는 그 버전에 머무를 수 없다. 이미 배포된 코드는 고칠 수 없어
설계로 막을 방법이 없으므로 설정 화면에서 그 사실을 미리 알린다.

Edge Cases: 수동 패키지를 받는 도중 앱을 닫으면 이전 자동 업데이트를 대신 적용하지 않는다.
수동 전환이 끝나거나 실패하기 전까지 자동 다운로드 결과도 적용 대상을 덮지 못한다.

Last Updated: 2026-08-16 | .NET 8 / Velopack 0.0.1298 | 버전 전환에 delta 사슬 적용
*/
namespace TanukiTarkovMap.Models.Services
{
    public class UpdateService
    {
        /// <summary> 업데이트 조회에 사용하는 GitHub 저장소 주소 (App의 버전 표시에도 사용) </summary>
        internal const string GitHubRepoUrl = "https://github.com/siakun/TanukiTarkovMap";

        private UpdateManager? _autoUpdateManager;
        private UpdateInfo? _pendingUpdate;
        private UpdateManager? _pendingUpdateManager;
        private bool _restartAfterApply;
        private bool _applyScheduled;
        private bool _manualVersionSwitchInProgress;
        private bool _pendingIsManualVersionSwitch;
        private readonly object _pendingUpdateLock = new();

        /// <summary> _autoUpdateManager를 만들 때 쓴 베타 수신 여부 (설정이 바뀌면 다시 만들어야 한다) </summary>
        private bool _managerAcceptsPrerelease;

        /// <summary>
        /// DI 컨테이너 전용 생성자 - 외부에서 new 사용 금지
        /// ServiceLocator.CreateInstance()를 통해서만 생성
        /// </summary>
        internal UpdateService() { }

        /// <summary>
        /// 즉시 적용을 고른 뒤 앱의 정상 종료 절차를 시작해 달라는 요청.
        /// 업데이트 서비스가 WPF와 CEF를 직접 참조하지 않도록 App이 받아 처리한다
        /// </summary>
        public event EventHandler? RestartRequested;

        /// <summary> 다운로드까지 완료되어 적용 대기 중인 업데이트가 있는지 여부 </summary>
        public bool UpdateDownloaded
        {
            get
            {
                lock (_pendingUpdateLock) return _pendingUpdate != null;
            }
        }

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
        /// 자동 갱신용 UpdateManager. 생성 자체는 로컬 정보만 읽으므로 네트워크를 타지 않는다.
        /// 베타 수신 여부가 소스의 조회 대상을 바꾸므로, 설정이 달라지면 다시 만든다
        /// </summary>
        private UpdateManager AutoUpdateManager
        {
            get
            {
                var acceptPrerelease = App.GetSettings().PrereleaseEnabled;
                if (_autoUpdateManager == null || _managerAcceptsPrerelease != acceptPrerelease)
                {
                    _autoUpdateManager = new UpdateManager(new GithubSource(GitHubRepoUrl, null, acceptPrerelease));
                    _managerAcceptsPrerelease = acceptPrerelease;
                }

                return _autoUpdateManager;
            }
        }

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
                    var isDowngrade = AutoUpdateManager.CurrentVersion != null
                                      && pendingAsset.Version.CompareTo(AutoUpdateManager.CurrentVersion) < 0;
                    if (!TrySetPendingUpdate(
                        AutoUpdateManager,
                        new UpdateInfo(pendingAsset, isDowngrade),
                        restartAfterApply: false,
                        isManualVersionSwitch: false))
                    {
                        return;
                    }

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
                if (!TrySetPendingUpdate(
                        AutoUpdateManager,
                        updateInfo,
                        restartAfterApply: false,
                        isManualVersionSwitch: false))
                {
                    Logger.SimpleLog("[UpdateService] Automatic update result ignored while a version switch is in progress");
                    return;
                }

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
        /// 베타를 켜지 않았으면 프리릴리스를 빼서, 목록에 보이는 것과 자동 갱신이 따라가는 대상을
        /// 같게 맞춘다. 네트워크 실패는 호출 측에서 사용자에게 알린다
        /// </summary>
        public async Task<IReadOnlyList<ReleaseVersion>> GetAvailableVersionsAsync(CancellationToken cancelToken = default)
        {
            var releases = await GitHubReleaseSource.FetchVersionsAsync(GitHubRepoUrl, cancelToken);
            if (App.GetSettings().PrereleaseEnabled) return releases;

            return releases.Where(release => !release.IsPrerelease).ToArray();
        }

        /// <summary>
        /// 지정한 버전을 내려받고 앱의 정상 종료를 요청한다. 다운그레이드도 이 경로를 쓴다
        /// </summary>
        public async Task InstallVersionAsync(
            ReleaseVersion target,
            Action<int>? progress = null,
            Action<long>? onDownloadSizeResolved = null,
            CancellationToken cancelToken = default)
        {
            lock (_pendingUpdateLock)
            {
                if (_manualVersionSwitchInProgress)
                {
                    throw new InvalidOperationException("다른 버전 설치가 이미 진행 중입니다.");
                }

                _manualVersionSwitchInProgress = true;
            }

            try
            {
                // 사슬을 뒤로 따라갈 때 기준 버전에 해당하는 릴리스를 목록에서 찾아야 한다.
                // delta로 갈지는 피드를 읽어 봐야 알 수 있으므로 여기서는 재료만 준비한다
                var versions = await FetchVersionsSafelyAsync(cancelToken);

                var source = new GitHubReleaseSource(target, versions);
                if (onDownloadSizeResolved != null)
                {
                    // Velopack이 delta를 포기하고 full로 돌아서면 화면의 총량도 함께 바꾼다
                    source.FullFallbackStarted += onDownloadSizeResolved;
                }

                // 낮은 버전 설치는 UpdateOptions로 명시해야 Velopack이 막지 않는다
                var manager = new LocalPackageAwareUpdateManager(
                    source, new UpdateOptions { AllowVersionDowngrade = true });

                if (!manager.IsInstalled)
                {
                    throw new InvalidOperationException("Velopack으로 설치된 실행 파일에서만 버전을 바꿀 수 있습니다.");
                }

                var isDowngrade = manager.CurrentVersion != null
                                  && target.Version.CompareTo(manager.CurrentVersion) < 0;

                // delta를 붙일 기준은 지금 설치된 패키지다. 그 파일이 없으면(포터블 설치 등)
                // 붙일 대상이 없으므로 full로 받는다
                var baseRelease = manager.GetLocalBasePackage();
                if (baseRelease != null && manager.CurrentVersion != null
                    && baseRelease.Version.CompareTo(manager.CurrentVersion) != 0)
                {
                    // 지난 실행에서 받아 둔 더 새 패키지가 남아 있으면 사슬의 출발점과 어긋난다
                    Logger.SimpleLog(
                        $"[UpdateService] Local package v{baseRelease.Version} differs from running v{manager.CurrentVersion}, skipping deltas");
                    baseRelease = null;
                }

                // 다운그레이드에는 delta가 없다. 헛되이 피드를 훑지 않도록 여기서 끊는다
                var plan = await source.ResolvePlanAsync(isDowngrade ? null : baseRelease, cancelToken);

                Logger.SimpleLog(
                    $"[UpdateService] Installing v{target.Version} (downgrade: {isDowngrade}, deltas: {plan.Deltas.Length})");

                // BaseRelease가 null이면 Velopack은 delta를 통째로 건너뛴다. 문서에는 null일 때
                // 설치된 버전에서 시도한다고 적혀 있지만 구현은 그렇지 않다
                var updateInfo = baseRelease != null && plan.Deltas.Length > 0
                    ? new UpdateInfo(plan.TargetFull, isDowngrade, baseRelease, plan.Deltas)
                    : new UpdateInfo(plan.TargetFull, isDowngrade);

                // 실제로 받을 크기가 정해진 뒤에 알린다. 사슬을 못 쓰게 되면 full 크기다
                onDownloadSizeResolved?.Invoke(
                    updateInfo.DeltasToTarget.Length > 0
                        ? updateInfo.DeltasToTarget.Sum(delta => delta.Size)
                        : target.PackageSize);

                await manager.DownloadUpdatesAsync(updateInfo, progress, cancelToken);

                Logger.SimpleLog($"[UpdateService] v{target.Version} downloaded, restarting to apply");
                TrySetPendingUpdate(
                    manager,
                    updateInfo,
                    restartAfterApply: true,
                    isManualVersionSwitch: true);
                RequestRestart();
            }
            catch
            {
                lock (_pendingUpdateLock) _manualVersionSwitchInProgress = false;
                throw;
            }
        }

        /// <summary>
        /// Velopack이 protected로 감춘 Locator에 닿기 위한 최소한의 상속.
        ///
        /// delta를 적용하려면 UpdateInfo.BaseRelease에 로컬 패키지를 넣어야 하는데, 그 값을
        /// 얻는 통로가 Locator뿐이다. UpdateManager를 통째로 대신하려는 것이 아니라 이 한
        /// 가지만 열어 준다
        /// </summary>
        private sealed class LocalPackageAwareUpdateManager : UpdateManager
        {
            public LocalPackageAwareUpdateManager(IUpdateSource source, UpdateOptions options)
                : base(source, options) { }

            /// <summary> 패키지 폴더에 있는 가장 최신 full 패키지. 없으면 null </summary>
            public VelopackAsset? GetLocalBasePackage()
            {
                try { return Locator.GetLatestLocalFullPackage(); }
                catch (Exception ex)
                {
                    Logger.SimpleLog($"[UpdateService] Local base package lookup failed: {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// 설치된 버전에서 대상까지의 delta 사슬을 만든다. 쓸 수 없으면 빈 목록.
        ///
        /// 목록 조회가 실패해도 설치를 막지 않는다. 사슬이 없으면 full로 받으면 되고,
        /// 그것이 이 기능이 없던 때의 동작이다
        /// </summary>
        private static async Task<IReadOnlyList<ReleaseVersion>> FetchVersionsSafelyAsync(CancellationToken cancelToken)
        {
            try
            {
                return await GitHubReleaseSource.FetchVersionsAsync(GitHubRepoUrl, cancelToken);
            }
            catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
            {
                // 토큰을 함께 본다. HttpClient는 자기 Timeout이 지나도 같은 예외를 던지는데,
                // 그것은 사용자의 취소가 아니라 조회 실패다
                throw;
            }
            catch (Exception ex)
            {
                // 목록이 없으면 사슬을 못 만들 뿐이다. full로 받으면 되고 그것이 이전 동작이다
                Logger.SimpleLog($"[UpdateService] Version list lookup failed ({ex.Message}), using full package");
                return [];
            }
        }

        /// <summary>
        /// 대기 중인 업데이트를 즉시 적용하고 재시작한다 (타이틀바 업데이트 아이콘 클릭 시)
        /// </summary>
        public void ApplyAndRestartNow()
        {
            lock (_pendingUpdateLock)
            {
                if (_pendingUpdate == null || _manualVersionSwitchInProgress) return;
                _restartAfterApply = true;
            }

            Logger.SimpleLog("[UpdateService] Applying update now and restarting...");
            RequestRestart();
        }

        /// <summary>
        /// 앱 종료 시 대기 중인 업데이트가 있으면 프로세스가 끝난 뒤 적용되도록 예약한다.
        /// 즉시 적용을 골랐으면 다시 시작하고, 평소 종료라면 다음 실행부터 새 버전을 쓴다
        /// </summary>
        public void ApplyOnExit()
        {
            UpdateManager manager;
            UpdateInfo update;
            bool restartAfterApply;

            lock (_pendingUpdateLock)
            {
                if (_pendingUpdate == null || _pendingUpdateManager == null || _applyScheduled) return;

                // 수동 다운로드가 끝나기 전에 사용자가 앱을 닫으면 이전 자동 업데이트를 대신
                // 적용하지 않는다. 사용자가 고른 패키지가 준비되지 않았으므로 다음 실행에서 다시 고른다
                if (_manualVersionSwitchInProgress && !_pendingIsManualVersionSwitch) return;

                manager = _pendingUpdateManager;
                update = _pendingUpdate;
                restartAfterApply = _restartAfterApply;
                _applyScheduled = true;
            }

            try
            {
                Logger.SimpleLog($"[UpdateService] Scheduling update apply after exit (restart: {restartAfterApply})");
                manager.WaitExitThenApplyUpdates(
                    update.TargetFullRelease,
                    silent: true,
                    restart: restartAfterApply);
            }
            catch (Exception ex)
            {
                lock (_pendingUpdateLock) _applyScheduled = false;
                Logger.SimpleLog($"[UpdateService] ApplyOnExit failed: {ex.Message}");
            }
        }

        private bool TrySetPendingUpdate(
            UpdateManager manager,
            UpdateInfo update,
            bool restartAfterApply,
            bool isManualVersionSwitch)
        {
            lock (_pendingUpdateLock)
            {
                // 시작 시 자동 확인이 먼저 출발했더라도 사용자가 고른 버전이 적용 대상을 결정한다
                if (!isManualVersionSwitch && _manualVersionSwitchInProgress) return false;

                _pendingUpdateManager = manager;
                _pendingUpdate = update;
                _restartAfterApply = restartAfterApply;
                _pendingIsManualVersionSwitch = isManualVersionSwitch;
                _applyScheduled = false;
                return true;
            }
        }

        private void RequestRestart()
        {
            var handler = RestartRequested;
            if (handler == null)
            {
                Logger.SimpleLog("[UpdateService] Restart requested before the application lifecycle handler was ready");
                return;
            }

            handler(this, EventArgs.Empty);
        }
    }
}
