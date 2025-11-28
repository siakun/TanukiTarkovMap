using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TanukiTarkovMap.Views;

namespace TanukiTarkovMap.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private bool _isLoading = false;

        [ObservableProperty] public partial string GameFolder { get; set; } = string.Empty;
        [ObservableProperty] public partial string ScreenshotsFolder { get; set; } = string.Empty;
        [ObservableProperty] public partial bool HotkeyEnabled { get; set; } = true;
        [ObservableProperty] public partial string HotkeyKey { get; set; } = "F11";
        [ObservableProperty] public partial bool AutoDeleteLogs { get; set; } = false;
        [ObservableProperty] public partial bool AutoDeleteScreenshots { get; set; } = false;

        public SettingsViewModel()
        {
            LoadCurrentSettings();
        }

        // 속성 변경 시 자동 저장 (partial 메서드)
        partial void OnGameFolderChanged(string value) => AutoSave();
        partial void OnScreenshotsFolderChanged(string value) => AutoSave();
        partial void OnHotkeyEnabledChanged(bool value) => AutoSaveAndUpdateHotkey();
        partial void OnHotkeyKeyChanged(string value) => AutoSaveAndUpdateHotkey();
        partial void OnAutoDeleteLogsChanged(bool value) => AutoSave();
        partial void OnAutoDeleteScreenshotsChanged(bool value) => AutoSave();

        private void AutoSave()
        {
            if (_isLoading) return;
            Save();
        }

        private void AutoSaveAndUpdateHotkey()
        {
            if (_isLoading) return;
            Save();

            // 핫키 설정 변경 시 MainWindow에서 핫키 재등록
            if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.UpdateHotkeySettings();
            }
        }

        // Commands
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
                HotkeyKey = settings.HotkeyKey ?? "F11";
                AutoDeleteLogs = settings.autoDeleteLogs;
                AutoDeleteScreenshots = settings.autoDeleteScreenshots;
            }
            finally
            {
                _isLoading = false;
            }
        }
    }
}
