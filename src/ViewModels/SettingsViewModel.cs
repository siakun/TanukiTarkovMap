using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using NuGet.Versioning;
using Siakun.AutoUpdate;
using TanukiTarkovMap.Localization;
using TanukiTarkovMap.Messages;
using TanukiTarkovMap.Core.Data;
using TanukiTarkovMap.Core.Offline;
using TanukiTarkovMap.Models.Offline;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Core.Services;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.Core.Utils;

namespace TanukiTarkovMap.ViewModels
{
    public sealed class VersionItem(ReleaseVersion release, bool isCurrent, bool isLatest) : ObservableObject
    {
        public ReleaseVersion Release { get; } = release;
        public bool IsCurrent { get; } = isCurrent;
        public bool IsLatest { get; } = isLatest;

        public string DisplayName
        {
            get
            {
                var labels = new List<string>();
                if (IsCurrent) labels.Add(Strings.Version_Current);
                if (IsLatest) labels.Add(Strings.Version_Latest);
                if (Release.IsPrerelease) labels.Add(Strings.Version_Beta);

                return labels.Count == 0
                    ? Release.Version.ToString()
                    : $"{Release.Version}   ({string.Join(", ", labels)})";
            }
        }

        public void RefreshText() => OnPropertyChanged(nameof(DisplayName));
    }

    public sealed class LanguageOption(string code) : ObservableObject
    {
        public string Code { get; } = code;

        public string DisplayName => Code.Length == 0
            ? string.Format(Strings.Settings_Language_FollowWindows, AppLanguage.NativeName(AppLanguage.WindowsLanguage))
            : AppLanguage.NativeName(Code);

        public void RefreshText() => OnPropertyChanged(nameof(DisplayName));
    }

    public partial class SettingsViewModel : ObservableObject,
        IRecipient<SettingsOpenedMessage>,
        IRecipient<LanguageChangedMessage>
    {
        private bool _isLoading = false;

        private bool _versionListLoaded = false;

        private bool _versionRefreshRequested = false;

        private long _installTargetBytes = 0;

        public string AppVersion => App.Version;

        public string RepositoryUrl => UpdateServiceFactory.RepositoryUrl;

        public string SettingsFilePath => AppPaths.SettingsFilePath;

        public AppSettings Defaults { get; } = new();

        public SettingsViewModel()
        {
            LoadCurrentSettings();

            CacheResetScheduled = AppPaths.BrowserCacheResetRequested;

            ServiceLocator.RaidShareService.Changed += OnRaidShareChanged;

            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        public void Receive(SettingsOpenedMessage message)
        {
            _ = RefreshCacheSizeCommand.ExecuteAsync(null);

            if (_versionListLoaded) return;

            _versionListLoaded = true;
            _ = RefreshVersionsCommand.ExecuteAsync(null);
        }

        public void Receive(LanguageChangedMessage message)
        {
            OnPropertyChanged(string.Empty);
            foreach (var option in LanguageOptions) option.RefreshText();
            foreach (var version in AvailableVersions) version.RefreshText();
        }

        [RelayCommand]
        private void Save()
        {
            App.GameFolder = GameFolder;
            App.ScreenshotsFolder = ScreenshotsFolder;

            var settings = App.GetSettings();
            settings.Language = SelectedLanguage?.Code ?? string.Empty;
            settings.GameFolder = GameFolder;
            settings.ScreenshotsFolder = ScreenshotsFolder;
            settings.HotkeyEnabled = HotkeyEnabled;
            settings.HotkeyKey = HotkeyKey;
            settings.autoDeleteLogs = AutoDeleteLogs;
            settings.autoDeleteScreenshots = AutoDeleteScreenshots;
            settings.GoonTrackerEnabled = GoonTrackerEnabled;
            settings.AutoMapSwitchEnabled = AutoMapSwitchEnabled;
            settings.ScreenshotMapSyncEnabled = ScreenshotMapSyncEnabled;
            settings.AutoUpdateEnabled = AutoUpdateEnabled;
            settings.PrereleaseEnabled = PrereleaseEnabled;
            settings.GameFocusHotkeyEnabled = GameFocusHotkeyEnabled;
            settings.RaidFocusModeEnabled = RaidFocusModeEnabled;

            App.SetSettings(settings);
            Settings.Save();
        }

        [RelayCommand]
        private void Cancel()
        {
            LoadCurrentSettings();
        }

        [RelayCommand]
        private void ResetSettings()
        {
            App.ResetSettings();
            LoadCurrentSettings();
            WeakReferenceMessenger.Default.Send(new HotkeySettingsChangedMessage());
            ServiceLocator.RaidShareService.Configure(RaidShareSettings.From(App.GetSettings()));
        }

        private void LoadCurrentSettings()
        {
            _isLoading = true;
            try
            {
                GameFolder = App.GameFolder ?? string.Empty;
                ScreenshotsFolder = App.ScreenshotsFolder ?? string.Empty;

                var settings = App.GetSettings();
                SelectedLanguage = LanguageOptions.FirstOrDefault(option => option.Code == settings.Language)
                                   ?? LanguageOptions[0];
                HotkeyEnabled = settings.HotkeyEnabled;
                HotkeyKey = settings.HotkeyKey ?? AppSettings.DefaultHotkeyKey;
                AutoDeleteLogs = settings.autoDeleteLogs;
                AutoDeleteScreenshots = settings.autoDeleteScreenshots;
                GoonTrackerEnabled = settings.GoonTrackerEnabled;
                AutoMapSwitchEnabled = settings.AutoMapSwitchEnabled;
                ScreenshotMapSyncEnabled = settings.ScreenshotMapSyncEnabled;
                AutoUpdateEnabled = settings.AutoUpdateEnabled;
                PrereleaseEnabled = settings.PrereleaseEnabled;
                GameFocusHotkeyEnabled = settings.GameFocusHotkeyEnabled;
                RaidFocusModeEnabled = settings.RaidFocusModeEnabled;
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void AutoSave()
        {
            if (_isLoading) return;
            Save();
        }

        public IReadOnlyList<LanguageOption> LanguageOptions { get; } =
            [new LanguageOption(string.Empty), .. AppLanguage.Supported.Select(code => new LanguageOption(code))];

        [ObservableProperty] public partial LanguageOption? SelectedLanguage { get; set; }

        partial void OnSelectedLanguageChanged(LanguageOption? value)
        {
            if (_isLoading || value == null) return;
            Save();
            AppLanguage.Apply(value.Code);
        }

        [ObservableProperty] public partial string GameFolder { get; set; } = string.Empty;
        [ObservableProperty] public partial string ScreenshotsFolder { get; set; } = string.Empty;

        partial void OnGameFolderChanged(string value) => AutoSaveAndRestartLogWatcher();
        partial void OnScreenshotsFolderChanged(string value) => AutoSaveAndRestartScreenshotWatcher();

        private void AutoSaveAndRestartLogWatcher()
        {
            if (_isLoading) return;
            Save();

            ServiceLocator.LogsWatcher.Restart();
        }

        private void AutoSaveAndRestartScreenshotWatcher()
        {
            if (_isLoading) return;
            Save();

            ServiceLocator.ScreenshotsWatcher.Restart();
        }

        [RelayCommand]
        private void BrowseGameFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = Strings.Dialog_SelectGameFolder,
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
                Title = Strings.Dialog_SelectScreenshotsFolder,
                InitialDirectory = !string.IsNullOrEmpty(ScreenshotsFolder) ? ScreenshotsFolder : null,
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                ScreenshotsFolder = dialog.FolderName;
            }
        }

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

            WeakReferenceMessenger.Default.Send(new HotkeySettingsChangedMessage());
        }

        [ObservableProperty] public partial bool AutoMapSwitchEnabled { get; set; } = true;
        [ObservableProperty] public partial bool ScreenshotMapSyncEnabled { get; set; } = true;
        [ObservableProperty] public partial bool GoonTrackerEnabled { get; set; } = false;

        partial void OnAutoMapSwitchEnabledChanged(bool value) => AutoSave();
        partial void OnScreenshotMapSyncEnabledChanged(bool value) => AutoSave();
        partial void OnGoonTrackerEnabledChanged(bool value) => AutoSaveAndUpdateGoonTracker();

        private void AutoSaveAndUpdateGoonTracker()
        {
            if (_isLoading) return;
            Save();

            ServiceLocator.GoonTrackerService.Enabled = GoonTrackerEnabled;
        }

        public string RaidShareStatusText
        {
            get
            {
                var service = ServiceLocator.RaidShareService;
                return service.Status switch
                {
                    RaidShareStatus.Disabled => string.Empty,
                    RaidShareStatus.NeedsSettings => Strings.Share_Status_NeedsSettings,
                    RaidShareStatus.Connecting => Strings.Share_Status_Connecting,
                    RaidShareStatus.Connected when service.JoinedRaid is { } raid => string.Format(
                        Strings.Share_Status_InRaid, ModeName(raid.Mode), service.MemberCount),
                    RaidShareStatus.Connected when service.RaidRefusal == "mode-not-allowed" => string.Format(
                        Strings.Share_Status_ModeNotAllowed, ModeName(service.CurrentRaid?.Mode ?? string.Empty)),
                    RaidShareStatus.Connected when service.RaidRefusal is { } refusal
                        => string.Format(Strings.Share_Status_RaidRefused, refusal),
                    RaidShareStatus.Connected => Strings.Share_Status_Connected,
                    RaidShareStatus.Unauthorized => Strings.Share_Status_Unauthorized,
                    _ => string.Format(Strings.Share_Status_Unreachable, service.Failure ?? "-"),
                };
            }
        }

        private static string ModeName(string mode) => mode switch
        {
            "Pve" => "PvE",
            "Regular" => "PvP",
            "PvpSeason" => "PvP Season",
            _ => mode,
        };

        private void OnRaidShareChanged(object? sender, EventArgs e) =>
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => OnPropertyChanged(nameof(RaidShareStatusText)));

        private long? _browserCacheBytes;

        private bool _measuringBrowserCache;

        public string BrowserCacheSizeText => _measuringBrowserCache
            ? Strings.Cache_Measuring
            : _browserCacheBytes switch
            {
                null => string.Empty,
                { } bytes when bytes > 0 => $"{bytes / 1024d / 1024d:N1} MB",
                _ => Strings.Cache_Empty,
            };

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CacheResetButtonText))]
        public partial bool CacheResetScheduled { get; set; } = false;

        public string CacheResetButtonText => CacheResetScheduled ? Strings.Cache_CancelReset : Strings.Cache_Reset;

        public string CodeCacheLimitNotice =>
            string.Format(Strings.Cache_CodeCacheLimitNotice, AppPaths.CodeCacheLimitMegabytes);

        [RelayCommand]
        private async Task RefreshCacheSize()
        {
            _measuringBrowserCache = true;
            OnPropertyChanged(nameof(BrowserCacheSizeText));

            _browserCacheBytes = await Task.Run(AppPaths.GetBrowserCacheSize);
            _measuringBrowserCache = false;
            OnPropertyChanged(nameof(BrowserCacheSizeText));
        }

        [RelayCommand]
        private void ToggleCacheReset()
        {
            CacheResetScheduled = !CacheResetScheduled;
            AppPaths.BrowserCacheResetRequested = CacheResetScheduled;

            Logger.SimpleLog($"[SettingsViewModel] Browser cache reset scheduled: {CacheResetScheduled}");
        }

        public string LocalMapDataStatus
        {
            get
            {
                try
                {
                    var collected = MapResources.CollectedAt;
                    return string.Format(Strings.LocalMap_DataDate, $"{collected.ToLocalTime():yyyy-MM-dd}");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
                    or KeyNotFoundException or InvalidOperationException or FormatException)
                {
                    return Strings.LocalMap_DataMissing;
                }
            }
        }

        [ObservableProperty] public partial bool AutoUpdateEnabled { get; set; } = true;
        [ObservableProperty] public partial bool GameFocusHotkeyEnabled { get; set; } = true;
        [ObservableProperty] public partial bool RaidFocusModeEnabled { get; set; } = false;
        partial void OnGameFocusHotkeyEnabledChanged(bool value) => AutoSaveAndUpdateHotkey();
        partial void OnRaidFocusModeEnabledChanged(bool value) => AutoSaveAndUpdateHotkey();

        [ObservableProperty] public partial bool PrereleaseEnabled { get; set; } = false;

        public ObservableCollection<VersionItem> AvailableVersions { get; } = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(InstallSelectedVersionCommand))]
        public partial VersionItem? SelectedVersion { get; set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RefreshVersionsCommand))]
        public partial bool IsVersionListLoading { get; set; } = false;

        private Func<string>? _updateStatus;

        public string UpdateStatusMessage => _updateStatus?.Invoke() ?? string.Empty;

        private void ShowUpdateStatus(Func<string>? status)
        {
            _updateStatus = status;
            OnPropertyChanged(nameof(UpdateStatusMessage));
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(InstallSelectedVersionCommand))]
        [NotifyCanExecuteChangedFor(nameof(RefreshVersionsCommand))]
        [NotifyPropertyChangedFor(nameof(InstallProgressText))]
        public partial bool IsInstalling { get; set; } = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(InstallProgressText))]
        public partial int InstallProgress { get; set; } = 0;

        public string InstallProgressText
        {
            get
            {
                if (!IsInstalling) return string.Empty;
                if (InstallProgress >= 100) return Strings.Update_Installing;

                var totalMegabytes = _installTargetBytes / 1024d / 1024d;
                var receivedMegabytes = totalMegabytes * InstallProgress / 100d;
                return $"{InstallProgress}% ({receivedMegabytes:F1} / {totalMegabytes:F1} MB)";
            }
        }

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

            _ = RefreshVersionsCommand.ExecuteAsync(null);
        }

        private bool CanRefreshVersions() => !IsVersionListLoading && !IsInstalling;

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

        private async Task LoadVersionListAsync()
        {
            try
            {
                var updateService = ServiceLocator.UpdateService;
                var releases = await updateService.GetAvailableVersionsAsync();
                var installedVersion = updateService.CurrentVersion;

                var latestRelease = releases.FirstOrDefault();

                AvailableVersions.Clear();
                foreach (var release in releases)
                {
                    var isCurrent = installedVersion != null
                                    && release.Version.CompareTo(installedVersion) == 0;
                    var isLatest = latestRelease != null
                                   && release.Version.CompareTo(latestRelease.Version) == 0;

                    AvailableVersions.Add(new VersionItem(release, isCurrent, isLatest));
                }

                SelectedVersion = AvailableVersions.FirstOrDefault(item => item.IsCurrent)
                                  ?? AvailableVersions.FirstOrDefault();
                ShowUpdateStatus(AvailableVersions.Count > 0 ? null : () => Strings.Update_NoVersions);
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode == HttpStatusCode.Forbidden || ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                ShowUpdateStatus(() => Strings.Update_RateLimited);
                Logger.SimpleLog($"[SettingsViewModel] Version list rate limited: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowUpdateStatus(() => Strings.Update_ListFailed);
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

            if (!selected.IsLatest && AutoUpdateEnabled)
            {
                AutoUpdateEnabled = false;
            }

            IsInstalling = true;
            ShowUpdateStatus(null);

            _installTargetBytes = selected.Release.PackageSize;
            ReportInstallProgress(0);

            try
            {
                var reporter = new Progress<int>(ReportInstallProgress);
                var sizeReporter = new Progress<long>(bytes => _installTargetBytes = bytes);

                await ServiceLocator.UpdateService.InstallVersionAsync(
                    selected.Release,
                    ((IProgress<int>)reporter).Report,
                    ((IProgress<long>)sizeReporter).Report);
            }
            catch (Exception ex)
            {
                InstallProgress = 0;
                IsInstalling = false;
                var version = selected.Release.Version;
                var reason = ex.Message;
                ShowUpdateStatus(() => string.Format(Strings.Update_InstallFailed, version, reason));
                Logger.SimpleLog($"[SettingsViewModel] Version install failed: {ex}");
            }
        }

        private void ReportInstallProgress(int percent)
        {
            InstallProgress = percent;

            OnPropertyChanged(nameof(InstallProgressText));
        }

        [ObservableProperty] public partial string CustomUrl { get; set; } = "https://tarkov-market.com/pilot";

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

        [RelayCommand]
        private async Task PreviewDownloadProgress()
        {
            if (IsInstalling) return;

            IsInstalling = true;
            ShowUpdateStatus(null);
            _installTargetBytes = 253_893_613;

            try
            {
                for (var percent = 0; percent <= 100; percent += 2)
                {
                    ReportInstallProgress(percent);
                    await Task.Delay(60);
                }

                await Task.Delay(1500);
            }
            finally
            {
                IsInstalling = false;
                InstallProgress = 0;
            }
        }
    }
}
