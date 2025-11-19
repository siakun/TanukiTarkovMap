using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TanukiTarkovMap.ViewModels
{
    public partial class PipWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private double _pipWidth;

        [ObservableProperty]
        private double _pipHeight;

        [ObservableProperty]
        private double _pipLeft;

        [ObservableProperty]
        private double _pipTop;

        [ObservableProperty]
        private bool _isTopmost;

        // Commands
        [RelayCommand]
        private void ToggleTopmost()
        {
            IsTopmost = !IsTopmost;
        }

        [RelayCommand]
        private void ResetPosition()
        {
            PipLeft = 0;
            PipTop = 0;
        }

        [RelayCommand]
        private void ResetSize()
        {
            PipWidth = 300;
            PipHeight = 250;
        }

        public PipWindowViewModel()
        {
            // Initialize default PiP window size
            PipWidth = 300;
            PipHeight = 250;
            IsTopmost = true;
        }
    }
}
