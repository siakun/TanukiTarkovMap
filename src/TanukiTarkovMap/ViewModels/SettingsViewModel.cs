using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace TanukiTarkovMap.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty] public partial string GameFolder { get; set; }
        [ObservableProperty] public partial string ScreenshotsFolder { get; set; }
        [ObservableProperty] public partial bool PipEnabled { get; set; }
        [ObservableProperty] public partial bool PipRememberPosition { get; set; }
        [ObservableProperty] public partial bool PipHotkeyEnabled { get; set; }
        [ObservableProperty] public partial string PipHotkeyKey { get; set; }
        [ObservableProperty] public partial bool AutoDeleteLogs { get; set; }
        [ObservableProperty] public partial bool AutoDeleteScreenshots { get; set; }

        // Commands
        [RelayCommand]
        private void Save()
        {
            // Save settings logic will be implemented here
            // For now, just save to App
            App.GameFolder = GameFolder;
            App.ScreenshotsFolder = ScreenshotsFolder;

            var settings = App.GetSettings();
            settings.PipEnabled = PipEnabled;
            settings.PipRememberPosition = PipRememberPosition;

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

        public SettingsViewModel()
        {
            // Initialize default values
            PipEnabled = false;
            PipRememberPosition = true;
            PipHotkeyEnabled = false;
            PipHotkeyKey = "F11";
            AutoDeleteLogs = false;
            AutoDeleteScreenshots = false;

            // Load current settings
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            GameFolder = App.GameFolder;
            ScreenshotsFolder = App.ScreenshotsFolder;

            var settings = App.GetSettings();
            PipEnabled = settings.PipEnabled;
            PipRememberPosition = settings.PipRememberPosition;
        }
    }
}
