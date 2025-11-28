using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace TanukiTarkovMap.Behaviors
{
    /// <summary>
    /// 창 제어 버튼 동작 (최소화, 최대화/복원, 닫기)
    /// </summary>
    public enum WindowControlAction
    {
        Minimize,
        MaximizeRestore,
        Close
    }

    /// <summary>
    /// 버튼 클릭 시 창 제어 동작을 수행하는 Behavior
    /// </summary>
    public class WindowControlBehavior : Behavior<Button>
    {
        /// <summary>
        /// 수행할 창 제어 동작
        /// </summary>
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
