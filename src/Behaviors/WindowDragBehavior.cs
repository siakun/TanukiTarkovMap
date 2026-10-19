using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.Core.Utils;

namespace TanukiTarkovMap.Behaviors
{
    public class WindowDragBehavior : Behavior<UIElement>
    {
        public static readonly DependencyProperty EnableMaximizeOnDoubleClickProperty =
            DependencyProperty.Register(
                nameof(EnableMaximizeOnDoubleClick),
                typeof(bool),
                typeof(WindowDragBehavior),
                new PropertyMetadata(true));

        public bool EnableMaximizeOnDoubleClick
        {
            get => (bool)GetValue(EnableMaximizeOnDoubleClickProperty);
            set => SetValue(EnableMaximizeOnDoubleClickProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.MouseLeftButtonDown += OnMouseLeftButtonDown;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.MouseLeftButtonDown -= OnMouseLeftButtonDown;
            base.OnDetaching();
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            var window = Window.GetWindow(AssociatedObject);
            if (window == null)
                return;

            if (e.ClickCount == 2)
            {
                if (EnableMaximizeOnDoubleClick)
                {
                    if (window.WindowState == WindowState.Maximized)
                        window.WindowState = WindowState.Normal;
                    else
                        window.WindowState = WindowState.Maximized;
                }
                return;
            }

            var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            PInvoke.ReleaseCapture();
            PInvoke.SendMessage(handle, PInvoke.WM_NCLBUTTONDOWN, PInvoke.HT_CAPTION, 0);
        }
    }
}
