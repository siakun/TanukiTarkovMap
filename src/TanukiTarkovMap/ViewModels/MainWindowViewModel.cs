using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TanukiTarkovMap.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _currentMap;

        [ObservableProperty]
        private bool _isPipMode;

        [ObservableProperty]
        private double _windowWidth;

        [ObservableProperty]
        private double _windowHeight;

        // Commands
        [RelayCommand]
        private void TogglePipMode()
        {
            IsPipMode = !IsPipMode;
        }

        [RelayCommand]
        private void ChangeMap(string mapName)
        {
            CurrentMap = mapName;
        }

        public MainWindowViewModel()
        {
            // Initialize default values
            WindowWidth = 800;
            WindowHeight = 600;
            IsPipMode = false;
        }
    }
}
