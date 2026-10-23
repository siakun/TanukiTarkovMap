using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Messages;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.Core.Utils;
using TanukiTarkovMap.ViewModels;

namespace TanukiTarkovMap.Behaviors
{

    public class TrayWindowBehavior : Behavior<Window>
    {
        private MainWindowViewModel? _viewModel;
        private TranslateTransform? _topBarTransform;
        private Border? _browserContainer;
        private volatile bool _windowShown;

        public bool IsWindowShown => _windowShown;

        public static readonly DependencyProperty TopBarTransformNameProperty =
            DependencyProperty.Register(
                nameof(TopBarTransformName),
                typeof(string),
                typeof(TrayWindowBehavior),
                new PropertyMetadata("TopBarTransform"));

        public string TopBarTransformName
        {
            get => (string)GetValue(TopBarTransformNameProperty);
            set => SetValue(TopBarTransformNameProperty, value);
        }

        public static readonly DependencyProperty BrowserContainerNameProperty =
            DependencyProperty.Register(
                nameof(BrowserContainerName),
                typeof(string),
                typeof(TrayWindowBehavior),
                new PropertyMetadata("BrowserContainer"));

        public string BrowserContainerName
        {
            get => (string)GetValue(BrowserContainerNameProperty);
            set => SetValue(BrowserContainerNameProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += OnWindowLoaded;
            AssociatedObject.IsVisibleChanged += OnIsVisibleChanged;
            AssociatedObject.StateChanged += OnStateChanged;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.Loaded -= OnWindowLoaded;
            AssociatedObject.IsVisibleChanged -= OnIsVisibleChanged;
            AssociatedObject.StateChanged -= OnStateChanged;
            base.OnDetaching();
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _windowShown = IsShown();
            if (e.NewValue is true) KeepWindowOnScreen();
        }

        private void OnStateChanged(object? sender, EventArgs e)
        {
            _windowShown = IsShown();
            KeepWindowOnScreen();
        }

        private bool IsShown() => AssociatedObject.IsVisible && AssociatedObject.WindowState != WindowState.Minimized;

        private void KeepWindowOnScreen()
        {
            if (!AssociatedObject.IsVisible || AssociatedObject.WindowState != WindowState.Normal) return;
            try
            {
                MonitorLayout.KeepWindowOnScreen(new WindowInteropHelper(AssociatedObject).Handle);
            }
            catch (Exception ex)
            {
                Logger.Error("[TrayWindowBehavior] Failed to keep window on screen", ex);
            }
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _viewModel = AssociatedObject.DataContext as MainWindowViewModel;
            _topBarTransform = AssociatedObject.FindName(TopBarTransformName) as TranslateTransform;
            _browserContainer = AssociatedObject.FindName(BrowserContainerName) as Border;
        }

        public void ShowFromTray()
        {
            try
            {
                var window = AssociatedObject;

                if (_viewModel != null)
                {
                    _viewModel.IsSettingsOpen = false;
                }

                var showActivated = window.ShowActivated;
                try
                {
                    window.ShowActivated = false;
                    window.Show();
                    window.WindowState = WindowState.Normal;
                }
                finally
                {
                    window.ShowActivated = showActivated;
                }

                var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                PInvoke.ShowWindow(handle, PInvoke.SW_SHOWNOACTIVATE);

                var insertAfter = window.Topmost ? PInvoke.HWND_TOPMOST : PInvoke.HWND_TOP;
                if (!PInvoke.SetWindowPos(handle, insertAfter, 0, 0, 0, 0,
                    PInvoke.SWP_NOMOVE | PInvoke.SWP_NOSIZE | PInvoke.SWP_NOACTIVATE | PInvoke.SWP_SHOWWINDOW))
                    Logger.SimpleLog("[TrayWindowBehavior] Failed to raise restored window");

                if (_topBarTransform != null && _browserContainer != null)
                {
                    _topBarTransform.Y = -20;
                    _browserContainer.Margin = new Thickness(0, 0, 0, 0);

                    WeakReferenceMessenger.Default.Send(new TopBarHiddenChangedMessage(true));
                }

                Logger.SimpleLog("[TrayWindowBehavior] Window shown without stealing focus");
            }
            catch (Exception ex)
            {
                Logger.Error("[TrayWindowBehavior] Failed to show window", ex);
            }
        }

        public void HideToTray()
        {
            try
            {
                AssociatedObject.Hide();
                Logger.SimpleLog("[TrayWindowBehavior] Window hidden to tray");
            }
            catch (Exception ex)
            {
                Logger.Error("[TrayWindowBehavior] Failed to hide window", ex);
            }
        }

        public void ToggleVisibility()
        {
            if (IsShown())
            {
                HideToTray();
            }
            else
            {
                ShowFromTray();
            }
        }
    }
}
