using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TanukiTarkovMap.ViewModels
{
    public class PipWindowViewModel : INotifyPropertyChanged
    {
        private double _pipWidth;
        private double _pipHeight;
        private double _pipLeft;
        private double _pipTop;
        private bool _isTopmost;

        public double PipWidth
        {
            get => _pipWidth;
            set
            {
                _pipWidth = value;
                OnPropertyChanged();
            }
        }

        public double PipHeight
        {
            get => _pipHeight;
            set
            {
                _pipHeight = value;
                OnPropertyChanged();
            }
        }

        public double PipLeft
        {
            get => _pipLeft;
            set
            {
                _pipLeft = value;
                OnPropertyChanged();
            }
        }

        public double PipTop
        {
            get => _pipTop;
            set
            {
                _pipTop = value;
                OnPropertyChanged();
            }
        }

        public bool IsTopmost
        {
            get => _isTopmost;
            set
            {
                _isTopmost = value;
                OnPropertyChanged();
            }
        }

        public PipWindowViewModel()
        {
            // Initialize default PiP window size
            PipWidth = 300;
            PipHeight = 250;
            IsTopmost = true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}