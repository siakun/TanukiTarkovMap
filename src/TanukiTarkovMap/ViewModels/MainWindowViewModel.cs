using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TanukiTarkovMap.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private string _currentMap;
        private bool _isPipMode;
        private double _windowWidth;
        private double _windowHeight;

        public string CurrentMap
        {
            get => _currentMap;
            set
            {
                _currentMap = value;
                OnPropertyChanged();
            }
        }

        public bool IsPipMode
        {
            get => _isPipMode;
            set
            {
                _isPipMode = value;
                OnPropertyChanged();
            }
        }

        public double WindowWidth
        {
            get => _windowWidth;
            set
            {
                _windowWidth = value;
                OnPropertyChanged();
            }
        }

        public double WindowHeight
        {
            get => _windowHeight;
            set
            {
                _windowHeight = value;
                OnPropertyChanged();
            }
        }

        // Commands will be added here as needed

        public MainWindowViewModel()
        {
            // Initialize default values
            WindowWidth = 800;
            WindowHeight = 600;
            IsPipMode = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}