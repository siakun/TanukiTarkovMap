using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace TanukiTarkovMap.Behaviors
{
    public enum WindowControlAction
    {
        Minimize,
        MaximizeRestore,
        Close
    }

    public class WindowControlBehavior : Behavior<Button>
    {
        public static readonly DependencyProperty ActionProperty =
            DependencyProperty.Register(
                nameof(Action),
                typeof(WindowControlAction),
                typeof(WindowControlBehavior),
                new PropertyMetadata(WindowControlAction.Close));

        public WindowControlAction Action
        {
            get => (WindowControlAction)GetValue(ActionProperty);
            set => SetValue(ActionProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Click += OnClick;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.Click -= OnClick;
            base.OnDetaching();
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(AssociatedObject);
            if (window == null)
                return;

            switch (Action)
            {
                case WindowControlAction.Minimize:
                    window.WindowState = WindowState.Minimized;
                    break;

                case WindowControlAction.MaximizeRestore:
                    window.WindowState = window.WindowState == WindowState.Maximized
                        ? WindowState.Normal
                        : WindowState.Maximized;
                    break;

                case WindowControlAction.Close:
                    window.Close();
                    break;
            }
        }
    }
}
