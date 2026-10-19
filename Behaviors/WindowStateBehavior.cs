using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.Core.Utils;
using TanukiTarkovMap.ViewModels;

namespace TanukiTarkovMap.Behaviors
{
    public class WindowStateBehavior : Behavior<Window>
    {
        private MainWindowViewModel? _viewModel;
        private Grid? _mainGrid;
        private Button? _maximizeRestoreButton;
        private bool _isClampingLocation = false;
        private bool _isInitializing = true;

        public static readonly DependencyProperty MainGridNameProperty =
            DependencyProperty.Register(
                nameof(MainGridName),
                typeof(string),
                typeof(WindowStateBehavior),
                new PropertyMetadata("MainGrid"));

        public string MainGridName
        {
            get => (string)GetValue(MainGridNameProperty);
            set => SetValue(MainGridNameProperty, value);
        }

        public static readonly DependencyProperty MaximizeRestoreButtonNameProperty =
            DependencyProperty.Register(
                nameof(MaximizeRestoreButtonName),
                typeof(string),
                typeof(WindowStateBehavior),
                new PropertyMetadata("MaximizeRestoreButton"));

        public string MaximizeRestoreButtonName
        {
            get => (string)GetValue(MaximizeRestoreButtonNameProperty);
            set => SetValue(MaximizeRestoreButtonNameProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.Loaded += OnWindowLoaded;
            AssociatedObject.StateChanged += OnStateChanged;
            AssociatedObject.LocationChanged += OnLocationChanged;
            AssociatedObject.SizeChanged += OnSizeChanged;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            AssociatedObject.Loaded -= OnWindowLoaded;
            AssociatedObject.StateChanged -= OnStateChanged;
            AssociatedObject.LocationChanged -= OnLocationChanged;
            AssociatedObject.SizeChanged -= OnSizeChanged;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _viewModel = AssociatedObject.DataContext as MainWindowViewModel;

            _mainGrid = AssociatedObject.FindName(MainGridName) as Grid;
            _maximizeRestoreButton = AssociatedObject.FindName(MaximizeRestoreButtonName) as Button;

            if (_mainGrid == null)
            {
                Logger.SimpleLog($"[WindowStateBehavior] MainGrid '{MainGridName}' not found");
            }

            if (_maximizeRestoreButton == null)
            {
                Logger.SimpleLog($"[WindowStateBehavior] MaximizeRestoreButton '{MaximizeRestoreButtonName}' not found");
            }

            AssociatedObject.Dispatcher.InvokeAsync(() =>
            {
                _isInitializing = false;
                Logger.SimpleLog("[WindowStateBehavior] Initialization complete");
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void OnStateChanged(object? sender, EventArgs e)
        {
            var window = AssociatedObject;

            if (window.WindowState == WindowState.Maximized)
            {
                if (_mainGrid != null)
                {
                    var thickness = SystemParameters.WindowResizeBorderThickness;
                    _mainGrid.Margin = new Thickness(
                        thickness.Left,
                        thickness.Top,
                        thickness.Right,
                        thickness.Bottom);
                }

                if (_maximizeRestoreButton != null)
                {
                    _maximizeRestoreButton.Content = "❐";
                }
            }
            else
            {
                if (_mainGrid != null)
                {
                    _mainGrid.Margin = new Thickness(0);
                }

                if (_maximizeRestoreButton != null)
                {
                    _maximizeRestoreButton.Content = "□";
                }
            }
        }

        private void OnLocationChanged(object? sender, EventArgs e)
        {
            if (_isClampingLocation) return;
            if (_isInitializing) return;

            try
            {
                _isClampingLocation = true;
                NotifyWindowBoundsChanged();
            }
            finally
            {
                _isClampingLocation = false;
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isClampingLocation) return;
            if (_isInitializing) return;

            NotifyWindowBoundsChanged();
        }

        private void NotifyWindowBoundsChanged()
        {
            if (_viewModel == null) return;

            var window = AssociatedObject;
            var bounds = new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight);

            _viewModel.OnWindowBoundsChanged(this, new Views.WindowBoundsChangedEventArgs
            {
                Bounds = bounds
            });
        }
    }
}
