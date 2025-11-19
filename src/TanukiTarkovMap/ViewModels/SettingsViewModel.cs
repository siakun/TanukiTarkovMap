using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace TanukiTarkovMap.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private string _gameFolder;
        private string _screenshotsFolder;
        private bool _pipEnabled;
        private bool _pipRememberPosition;
        private bool _pipHotkeyEnabled;
        private string _pipHotkeyKey;
        private bool _autoDeleteLogs;
        private bool _autoDeleteScreenshots;

        public string GameFolder
        {
            get => _gameFolder;
            set
            {
                _gameFolder = value;
                OnPropertyChanged();
            }
        }

        public string ScreenshotsFolder
        {
            get => _screenshotsFolder;
            set
            {
                _screenshotsFolder = value;
                OnPropertyChanged();
            }
        }

        public bool PipEnabled
        {
            get => _pipEnabled;
            set
            {
                _pipEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool PipRememberPosition
        {
            get => _pipRememberPosition;
            set
            {
                _pipRememberPosition = value;
                OnPropertyChanged();
            }
        }

        public bool PipHotkeyEnabled
        {
            get => _pipHotkeyEnabled;
            set
            {
                _pipHotkeyEnabled = value;
                OnPropertyChanged();
            }
        }

        public string PipHotkeyKey
        {
            get => _pipHotkeyKey;
            set
            {
                _pipHotkeyKey = value;
                OnPropertyChanged();
            }
        }

        public bool AutoDeleteLogs
        {
            get => _autoDeleteLogs;
            set
            {
                _autoDeleteLogs = value;
                OnPropertyChanged();
            }
        }

        public bool AutoDeleteScreenshots
        {
            get => _autoDeleteScreenshots;
            set
            {
                _autoDeleteScreenshots = value;
                OnPropertyChanged();
            }
        }

        // Commands for Save, Cancel, Browse folders etc.
        public ICommand SaveCommand { get; set; }
        public ICommand CancelCommand { get; set; }
        public ICommand BrowseGameFolderCommand { get; set; }
        public ICommand BrowseScreenshotsFolderCommand { get; set; }

        public SettingsViewModel()
        {
            // Initialize default values
            PipEnabled = false;
            PipRememberPosition = true;
            PipHotkeyEnabled = false;
            PipHotkeyKey = "F11";
            AutoDeleteLogs = false;
            AutoDeleteScreenshots = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}