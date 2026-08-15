using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using NuGet.Versioning;
using TanukiTarkovMap.Messages;
using TanukiTarkovMap.Models.Data;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Models.Utils;

/**
SettingsViewModel - 설정 화면의 모든 값과 동작

Purpose: settings.json에 담기는 값을 화면에 잇고, 값이 바뀌는 즉시 저장한다.
설정 화면에만 있는 작업(캐시 비우기, 버전 전환, 개발자 도구)도 여기서 처리한다.

Architecture: 구획을 설정 화면의 섹션 순서와 맞춰 두었다. 한 기능의 속성과 커맨드가
한자리에 모여 있어야 그 기능만 보고 고칠 수 있다. 예전에는 속성이 파일 앞머리에 모두
몰려 있고 커맨드는 뒤쪽에 흩어져 있어, 버전 전환 하나를 보려 해도 파일 양 끝을 오가야 했다.

Core Functionality:
- 값 저장: 속성이 바뀌면 partial 메서드가 Save()를 불러 그 자리에서 파일에 쓴다
- 브라우저 캐시: 크기 표시와 비우기 예약
- 업데이트: 자동 갱신 스위치, 베타 수신, 버전 목록과 설치
- 개발자 도구: 주소 이동, 업데이트 UI 미리보기

State Management:
- _isLoading: 불러오는 중에는 자동 저장을 멈춘다. 값을 채우다가 파일을 덮어쓰면 안 된다
- _versionListLoaded: 설정을 열 때마다 GitHub을 부르지 않도록 첫 조회만 표시해 둔다
- _versionRefreshRequested: 목록을 읽는 동안 또 요청이 오면 끝난 뒤 한 번만 더 읽는다
- _installTargetBytes: 진행률을 MB로 환산할 때 쓰는 대상 패키지 크기

Dependencies:
- App/Settings: 설정 값의 실제 저장소
- AppPaths: 설정 파일과 브라우저 캐시의 위치, 캐시 크기 조회와 비우기
- UpdateService: 설치 가능한 버전 목록과 버전 설치
- WeakReferenceMessenger: 화면이 열렸다는 신호를 받고, 핫키와 아이콘 변경을 알린다

Last Updated: 2026-08-15 | .NET 8 | 구획을 기능 단위로 재배치
*/
namespace TanukiTarkovMap.ViewModels
{
    /// <summary>
    /// 설정 화면의 버전 목록 항목.
    /// 표시 이름과 상태 판정을 미리 계산해 XAML에서 컨버터 없이 쓴다
    /// </summary>
    public sealed record VersionItem(ReleaseVersion Release, string DisplayName, bool IsCurrent, bool IsLatest);

    public partial class SettingsViewModel : ObservableObject, IRecipient<SettingsOpenedMessage>
    {
        private bool _isLoading = false;

        /// <summary> 버전 목록을 한 번이라도 채웠는지 여부 (설정을 열 때마다 GitHub을 부르지 않으려고 둔다) </summary>
        private bool _versionListLoaded = false;

        /// <summary> 목록을 읽는 동안 다시 요청이 들어왔는지 (다 읽은 뒤 한 번만 더 읽는다) </summary>
        private bool _versionRefreshRequested = false;

        /// <summary> 설치 중인 패키지 크기(byte). 진행률을 MB로 환산할 때 쓴다 </summary>
        private long _installTargetBytes = 0;

        public string AppVersion => App.Version;

        /// <summary>
        /// 저장소 주소. 업데이트가 바라보는 곳과 화면에 보여 주는 곳이 갈라지지 않도록
        /// UpdateService가 쓰는 상수를 그대로 읽는다
        /// </summary>
        public string RepositoryUrl => UpdateService.GitHubRepoUrl;

        public string SettingsFilePath => AppPaths.SettingsFilePath;

        public SettingsViewModel()
        {
            LoadCurrentSettings();

            // 설정 창을 닫았다 열어도 예약 상태가 그대로 보이도록 실제 값에서 읽는다
            CacheResetScheduled = AppPaths.BrowserCacheResetRequested;

            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        /// <summary>
        /// 설정 화면이 열릴 때 화면을 볼 때만 필요한 값을 채운다.
        ///
        /// 버전 목록은 처음 한 번만 읽고, 이후 새 릴리스는 사용자가 새로고침으로 다시 읽는다.
        /// 조회는 GitHub API만 쓰므로 설치 형태와 무관하게 채운다. 포터블이나 개발 빌드에서도
        /// 어떤 버전이 있는지는 볼 수 있어야 하고, 설치만 막으면 된다
        /// </summary>
        public void Receive(SettingsOpenedMessage message)
        {
            // 캐시는 쓰는 동안 계속 불어나므로 열 때마다 다시 잰다
            _ = RefreshCacheSizeCommand.ExecuteAsync(null);

            if (_versionListLoaded) return;

            _versionListLoaded = true;
            _ = RefreshVersionsCommand.ExecuteAsync(null);
        }

        #region 저장과 불러오기
        /// <summary>
        /// 화면의 값을 settings.json에 쓴다.
        /// 속성이 바뀔 때마다 partial 메서드를 거쳐 불리므로 확인 버튼이 따로 없다
        /// </summary>
        [RelayCommand]
        private void Save()
        {
            // 경로 설정 저장
            App.GameFolder = GameFolder;
            App.ScreenshotsFolder = ScreenshotsFolder;

            var settings = App.GetSettings();
            settings.GameFolder = GameFolder;
            settings.ScreenshotsFolder = ScreenshotsFolder;
            settings.HotkeyEnabled = HotkeyEnabled;
            settings.HotkeyKey = HotkeyKey;
            settings.autoDeleteLogs = AutoDeleteLogs;
            settings.autoDeleteScreenshots = AutoDeleteScreenshots;
            settings.GoonTrackerEnabled = GoonTrackerEnabled;
            settings.AutoMapSwitchEnabled = AutoMapSwitchEnabled;
            settings.AutoUpdateEnabled = AutoUpdateEnabled;
            settings.PrereleaseEnabled = PrereleaseEnabled;

            App.SetSettings(settings);
            Models.Services.Settings.Save();
        }

        [RelayCommand]
        private void Cancel()
        {
            // Cancel logic - reload from current settings
            LoadCurrentSettings();
        }

        [RelayCommand]
        private void ResetSettings()
        {
            // Reset to default settings
            App.ResetSettings();
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            _isLoading = true;
            try
            {
                GameFolder = App.GameFolder ?? string.Empty;
                ScreenshotsFolder = App.ScreenshotsFolder ?? string.Empty;

                var settings = App.GetSettings();
                HotkeyEnabled = settings.HotkeyEnabled;
                HotkeyKey = settings.HotkeyKey ?? AppSettings.DefaultHotkeyKey;
                AutoDeleteLogs = settings.autoDeleteLogs;
                AutoDeleteScreenshots = settings.autoDeleteScreenshots;
                GoonTrackerEnabled = settings.GoonTrackerEnabled;
                AutoMapSwitchEnabled = settings.AutoMapSwitchEnabled;
                AutoUpdateEnabled = settings.AutoUpdateEnabled;
                PrereleaseEnabled = settings.PrereleaseEnabled;
            }
            finally
            {
                _isLoading = false;
            }
        }

        /// <summary>
        /// 값을 불러오는 중에는 저장하지 않는다.
        /// 채우는 도중에 파일을 쓰면 아직 못 채운 값이 기본값으로 덮인다
        /// </summary>
        private void AutoSave()
        {
            if (_isLoading) return;
            Save();
        }
        #endregion

        #region 타르코프 경로
        [ObservableProperty] public partial string GameFolder { get; set; } = string.Empty;
        [ObservableProperty] public partial string ScreenshotsFolder { get; set; } = string.Empty;

        partial void OnGameFolderChanged(string value) => AutoSaveAndRestartLogWatcher();
        partial void OnScreenshotsFolderChanged(string value) => AutoSaveAndRestartScreenshotWatcher();

        private void AutoSaveAndRestartLogWatcher()
        {
            if (_isLoading) return;
            Save();

            // 게임 폴더 변경 시 LogsWatcher 재시작
            Models.FileSystem.LogsWatcher.Restart();
        }

        private void AutoSaveAndRestartScreenshotWatcher()
        {
            if (_isLoading) return;
            Save();

            // 스크린샷 폴더 변경 시 ScreenshotsWatcher 재시작
            Models.FileSystem.ScreenshotsWatcher.Restart();
        }

        [RelayCommand]
        private void BrowseGameFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Escape From Tarkov game folder",
                InitialDirectory = !string.IsNullOrEmpty(GameFolder) ? GameFolder : null,
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                GameFolder = dialog.FolderName;
            }
        }

        [RelayCommand]
        private void BrowseScreenshotsFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Screenshots folder",
                InitialDirectory = !string.IsNullOrEmpty(ScreenshotsFolder) ? ScreenshotsFolder : null,
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                ScreenshotsFolder = dialog.FolderName;
            }
        }
        #endregion

        #region 단축키와 파일 자동 정리
        [ObservableProperty] public partial bool HotkeyEnabled { get; set; } = true;
        [ObservableProperty] public partial string HotkeyKey { get; set; } = AppSettings.DefaultHotkeyKey;
        [ObservableProperty] public partial bool AutoDeleteLogs { get; set; } = false;
        [ObservableProperty] public partial bool AutoDeleteScreenshots { get; set; } = false;

        partial void OnHotkeyEnabledChanged(bool value) => AutoSaveAndUpdateHotkey();
        partial void OnHotkeyKeyChanged(string value) => AutoSaveAndUpdateHotkey();
        partial void OnAutoDeleteLogsChanged(bool value) => AutoSave();
        partial void OnAutoDeleteScreenshotsChanged(bool value) => AutoSave();

        private void AutoSaveAndUpdateHotkey()
        {
            if (_isLoading) return;
            Save();

            // 핫키 설정 변경 메시지 발송 (MainWindow에서 수신하여 핫키 재등록)
            WeakReferenceMessenger.Default.Send(new HotkeySettingsChangedMessage());
        }
        #endregion

        #region 자동 맵 전환과 Goon Tracker
        [ObservableProperty] public partial bool AutoMapSwitchEnabled { get; set; } = true;
        [ObservableProperty] public partial bool GoonTrackerEnabled { get; set; } = true;

        partial void OnAutoMapSwitchEnabledChanged(bool value) => AutoSave();
        partial void OnGoonTrackerEnabledChanged(bool value) => AutoSaveAndUpdateGoonTracker();

        private void AutoSaveAndUpdateGoonTracker()
        {
            if (_isLoading) return;
            Save();

            // GoonTrackerService 활성화/비활성화
            ServiceLocator.GoonTrackerService.Enabled = GoonTrackerEnabled;
        }
        #endregion

        #region 브라우저 캐시
        /// <summary> 브라우저 캐시가 차지하는 크기 (예: 620.5 MB) </summary>
        [ObservableProperty] public partial string BrowserCacheSizeText { get; set; } = string.Empty;

        /// <summary>
        /// 앱을 닫을 때 캐시를 비우도록 예약했는지 여부.
        /// 실행 중에는 CEF가 프로필 파일을 붙들고 있어 그 자리에서 지울 수 없다
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CacheResetButtonText))]
        public partial bool CacheResetScheduled { get; set; } = false;

        public string CacheResetButtonText => CacheResetScheduled ? "비우기 취소" : "캐시 비우기";

        /// <summary> 코드 캐시 자동 정리 안내. 기준 값은 AppPaths가 정하므로 여기서 다시 적지 않는다 </summary>
        public string CodeCacheLimitNotice =>
            $"페이지 스크립트 캐시가 {AppPaths.CodeCacheLimitMegabytes}MB를 넘으면 시작할 때 자동으로 정리합니다. 맵 타일은 그대로 두므로 느려지지 않습니다";

        /// <summary>
        /// 브라우저 캐시 크기를 다시 잰다. 파일 수천 개를 훑으므로 백그라운드에서 돈다
        /// </summary>
        [RelayCommand]
        private async Task RefreshCacheSize()
        {
            BrowserCacheSizeText = "확인 중...";

            var sizeInBytes = await Task.Run(AppPaths.GetBrowserCacheSize);
            BrowserCacheSizeText = sizeInBytes > 0
                ? $"{sizeInBytes / 1024d / 1024d:N1} MB"
                : "비어 있음";
        }

        /// <summary>
        /// 캐시 비우기를 예약하거나 되돌린다.
        /// 실행 중에는 지울 수 없어 실제 삭제는 앱을 닫을 때 일어난다
        /// </summary>
        [RelayCommand]
        private void ToggleCacheReset()
        {
            CacheResetScheduled = !CacheResetScheduled;
            AppPaths.BrowserCacheResetRequested = CacheResetScheduled;

            Logger.SimpleLog($"[SettingsViewModel] Browser cache reset scheduled: {CacheResetScheduled}");
        }
        #endregion

        #region 업데이트와 버전 전환
        [ObservableProperty] public partial bool AutoUpdateEnabled { get; set; } = true;
        [ObservableProperty] public partial bool PrereleaseEnabled { get; set; } = false;

        /// <summary> 설치할 수 있는 버전 목록 (최신 순) </summary>
        public ObservableCollection<VersionItem> AvailableVersions { get; } = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(InstallSelectedVersionCommand))]
        public partial VersionItem? SelectedVersion { get; set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RefreshVersionsCommand))]
        public partial bool IsVersionListLoading { get; set; } = false;

        /// <summary>
        /// 목록 조회나 설치가 뜻대로 되지 않았을 때 그 사정을 알리는 문구 (정상이면 빈 문자열).
        /// 진행률 표시는 설치가 끝나면 사라지므로 실패는 계속 남는 이 자리에 적는다
        /// </summary>
        [ObservableProperty] public partial string UpdateStatusMessage { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(InstallSelectedVersionCommand))]
        [NotifyCanExecuteChangedFor(nameof(RefreshVersionsCommand))]
        public partial bool IsInstalling { get; set; } = false;

        /// <summary> 다운로드 진행률 (0~100). Velopack이 정수 퍼센트만 알려준다 </summary>
        [ObservableProperty] public partial int InstallProgress { get; set; } = 0;

        /// <summary> 진행 상황 문구 (예: 52% (133.2 / 253.9 MB)) </summary>
        [ObservableProperty] public partial string InstallProgressText { get; set; } = string.Empty;

        /// <summary>
        /// 버전을 바꿀 수 있는 설치인지 여부.
        /// 포터블 압축본과 개발 빌드는 설치 관리자가 없어 교체할 수 없다
        /// </summary>
        public bool CanSwitchVersion
        {
            get
            {
                try { return ServiceLocator.UpdateService.IsManagedInstall; }
                catch { return false; }
            }
        }

        partial void OnAutoUpdateEnabledChanged(bool value) => AutoSave();

        partial void OnPrereleaseEnabledChanged(bool value)
        {
            if (_isLoading) return;
            Save();

            // 목록에 베타가 나타나거나 사라져야 하므로 다시 읽는다
            _ = RefreshVersionsCommand.ExecuteAsync(null);
        }

        private bool CanRefreshVersions() => !IsVersionListLoading && !IsInstalling;

        /// <summary>
        /// 버전 목록을 다시 읽는다.
        ///
        /// CanExecute는 버튼을 비활성화할 뿐, 설정이 바뀔 때처럼 코드에서 ExecuteAsync를 직접
        /// 부르는 길은 막지 못한다. 베타 체크박스를 연달아 누르면 누른 횟수만큼 요청이 나가
        /// GitHub의 시간당 한도(인증 없이 IP당 60회)를 넘긴다. 이미 읽는 중이면 표시만 남기고
        /// 물러났다가, 끝난 뒤 마지막 설정으로 한 번만 더 읽는다
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanRefreshVersions))]
        private async Task RefreshVersions()
        {
            if (IsVersionListLoading)
            {
                _versionRefreshRequested = true;
                return;
            }

            IsVersionListLoading = true;
            try
            {
                do
                {
                    _versionRefreshRequested = false;
                    await LoadVersionListAsync();
                }
                while (_versionRefreshRequested);
            }
            finally
            {
                IsVersionListLoading = false;
            }
        }

        /// <summary>
        /// 목록을 한 번 읽어 화면에 채운다.
        /// 읽는 동안에는 상태 문구를 바꾸지 않는다. 그 문구가 나타났다 사라지면서
        /// 설정 패널 높이가 늘었다 줄어 화면이 덜컥거리기 때문이다
        /// </summary>
        private async Task LoadVersionListAsync()
        {
            try
            {
                var updateService = ServiceLocator.UpdateService;
                var releases = await updateService.GetAvailableVersionsAsync();
                var installedVersion = updateService.CurrentVersion;

                // 자동 업데이트가 따라가는 대상은 목록의 첫 항목이다.
                // 베타를 끄면 서비스가 프리릴리스를 이미 빼고 주므로 여기서 또 거르지 않는다
                var latestRelease = releases.FirstOrDefault();

                AvailableVersions.Clear();
                foreach (var release in releases)
                {
                    var isCurrent = installedVersion != null
                                    && release.Version.CompareTo(installedVersion) == 0;
                    var isLatest = latestRelease != null
                                   && release.Version.CompareTo(latestRelease.Version) == 0;

                    AvailableVersions.Add(new VersionItem(
                        release,
                        BuildDisplayName(release, isCurrent, isLatest),
                        isCurrent,
                        isLatest));
                }

                // 설치 버전을 기본으로 두고, 그 버전을 목록에서 찾지 못하면(포터블/개발 빌드) 최신을 보여준다
                SelectedVersion = AvailableVersions.FirstOrDefault(item => item.IsCurrent)
                                  ?? AvailableVersions.FirstOrDefault();
                UpdateStatusMessage = AvailableVersions.Count > 0
                    ? string.Empty
                    : "설치할 수 있는 버전이 없습니다";
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode == HttpStatusCode.Forbidden || ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // 인증 없이 쓰는 GitHub API는 IP마다 시간당 60회로 묶여 있다. 연결 문제와 구분해서 알린다
                UpdateStatusMessage = "GitHub 요청 한도를 넘었습니다. 한 시간쯤 뒤에 다시 시도하세요";
                Logger.SimpleLog($"[SettingsViewModel] Version list rate limited: {ex.Message}");
            }
            catch (Exception ex)
            {
                UpdateStatusMessage = "버전 목록을 가져오지 못했습니다. 연결을 확인하고 다시 시도하세요";
                Logger.SimpleLog($"[SettingsViewModel] Version list load failed: {ex.Message}");
            }
        }

        private bool CanInstallSelectedVersion()
            => CanSwitchVersion && !IsInstalling && SelectedVersion != null && !SelectedVersion.IsCurrent;

        [RelayCommand(CanExecute = nameof(CanInstallSelectedVersion))]
        private async Task InstallSelectedVersion()
        {
            var selected = SelectedVersion;
            if (selected == null) return;

            // 최신이 아닌 버전을 골랐다면 자동 업데이트를 꺼야 한다.
            // 켜둔 채로 두면 다음 실행에서 곧바로 최신으로 되돌아가 선택이 사라진다
            if (!selected.IsLatest && AutoUpdateEnabled)
            {
                AutoUpdateEnabled = false;
            }

            IsInstalling = true;
            UpdateStatusMessage = string.Empty;
            _installTargetBytes = selected.Release.PackageSize;
            ReportInstallProgress(0);

            try
            {
                // Progress<T>는 만들어진 스레드의 컨텍스트로 보고를 돌려주므로 UI 스레드 마샬링이 필요 없다
                var reporter = new Progress<int>(ReportInstallProgress);
                await ServiceLocator.UpdateService.InstallVersionAsync(
                    selected.Release,
                    ((IProgress<int>)reporter).Report);
            }
            catch (Exception ex)
            {
                // 성공하면 UpdateService가 App의 정상 종료를 요청한다. 여기서는 다운로드나
                // 패키지 준비가 실패했을 때만 설치 상태를 되돌린다
                InstallProgress = 0;
                InstallProgressText = string.Empty;
                IsInstalling = false;
                UpdateStatusMessage = $"v{selected.Release.Version} 설치에 실패했습니다: {ex.Message}";
                Logger.SimpleLog($"[SettingsViewModel] Version install failed: {ex}");
            }
        }

        private void ReportInstallProgress(int percent)
        {
            InstallProgress = percent;

            if (percent >= 100)
            {
                // 내려받은 뒤에도 검증과 압축 해제가 남아 있어 100%에서 잠시 멈춘 것처럼 보인다
                InstallProgressText = "설치하는 중입니다. 곧 다시 시작합니다";
                return;
            }

            var totalMegabytes = _installTargetBytes / 1024d / 1024d;
            var receivedMegabytes = totalMegabytes * percent / 100d;
            InstallProgressText = $"{percent}% ({receivedMegabytes:F1} / {totalMegabytes:F1} MB)";
        }

        private static string BuildDisplayName(ReleaseVersion release, bool isCurrent, bool isLatest)
        {
            var labels = new List<string>();
            if (isCurrent) labels.Add("현재");
            if (isLatest) labels.Add("최신");
            if (release.IsPrerelease) labels.Add("베타");

            return labels.Count == 0
                ? release.Version.ToString()
                : $"{release.Version}   ({string.Join(", ", labels)})";
        }
        #endregion

        #region 개발자 도구
        [ObservableProperty] public partial string CustomUrl { get; set; } = "https://tarkov-market.com/pilot";

        /// <summary>
        /// 타이틀 바 업데이트 아이콘을 강제로 켤지 여부.
        /// 개발 빌드에서는 Velopack 업데이트가 잡히지 않아 아이콘이 뜰 일이 없다.
        /// 켜 둔 채 배포본을 쓰면 가짜 아이콘이 남으므로 설정 파일에 저장하지 않는다
        /// </summary>
        [ObservableProperty] public partial bool UpdateIconAlwaysVisible { get; set; } = false;

        partial void OnUpdateIconAlwaysVisibleChanged(bool value)
            => WeakReferenceMessenger.Default.Send(new UpdateIconPreviewMessage(value));

        [RelayCommand]
        private void NavigateToPilot()
        {
            WeakReferenceMessenger.Default.Send(new NavigateToUrlMessage(App.WebsiteUrl));
        }

        [RelayCommand]
        private void NavigateToCustomUrl()
        {
            if (!string.IsNullOrWhiteSpace(CustomUrl))
            {
                WeakReferenceMessenger.Default.Send(new NavigateToUrlMessage(CustomUrl));
            }
        }

        /// <summary>
        /// 저장소를 기본 브라우저로 연다. 앱 안의 CefSharp로 열면 맵 자리를 빼앗기므로
        /// 바깥 브라우저에 넘긴다
        /// </summary>
        [RelayCommand]
        private void OpenRepository()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = RepositoryUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[SettingsViewModel] Failed to open repository: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenSettingsFolder()
        {
            var folder = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
        }

        /// <summary>
        /// 다운로드 진행률 표시를 실제 설치 없이 재생한다.
        /// 254MB짜리 패키지를 받아 보지 않고도 진행 문구와 막대를 확인할 수 있다
        /// </summary>
        [RelayCommand]
        private async Task PreviewDownloadProgress()
        {
            if (IsInstalling) return;

            IsInstalling = true;
            UpdateStatusMessage = string.Empty;
            _installTargetBytes = 253_893_613;   // v0.1.0 전체 패키지 크기

            try
            {
                for (var percent = 0; percent <= 100; percent += 2)
                {
                    ReportInstallProgress(percent);
                    await Task.Delay(60);
                }

                // 100%에 도달한 뒤 검증과 압축 해제 문구가 보이는 구간까지 재현한다
                await Task.Delay(1500);
            }
            finally
            {
                IsInstalling = false;
                InstallProgress = 0;
                InstallProgressText = string.Empty;
            }
        }
        #endregion
    }
}
