using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace TanukiTarkovMap.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _gameFolder;

        [ObservableProperty]
        private string _screenshotsFolder;

        [ObservableProperty]
        private bool _pipEnabled;

        [ObservableProperty]
        private bool _pipRememberPosition;

        [ObservableProperty]
        private bool _pipHotkeyEnabled;

        [ObservableProperty]
        private string _pipHotkeyKey;

        [ObservableProperty]
        private bool _autoDeleteLogs;

        [ObservableProperty]
        private bool _autoDeleteScreenshots;

        // Commands
        [RelayCommand]
        private void Save()
        {
            // Save settings logic will be implemented here
            // For now, just save to Env
            Models.Services.Env.GameFolder = GameFolder;
            Models.Services.Env.ScreenshotsFolder = ScreenshotsFolder;

            var settings = Models.Services.Env.GetSettings();
            settings.PipEnabled = PipEnabled;
            settings.PipRememberPosition = PipRememberPosition;

            Models.Services.Env.SetSettings(settings);
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
            Models.Services.Env.ResetSettings();
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
            GameFolder = Models.Services.Env.GameFolder;
            ScreenshotsFolder = Models.Services.Env.ScreenshotsFolder;

            var settings = Models.Services.Env.GetSettings();
            PipEnabled = settings.PipEnabled;
            PipRememberPosition = settings.PipRememberPosition;
        }
    }
}
