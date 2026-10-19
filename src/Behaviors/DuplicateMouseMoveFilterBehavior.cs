using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace TanukiTarkovMap.Behaviors
{
    public class DuplicateMouseMoveFilterBehavior : Behavior<UIElement>
    {
        private Point _lastMovePosition = new Point(double.NaN, double.NaN);

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewMouseDown += OnPreviewMouseDown;
            AssociatedObject.PreviewMouseMove += OnPreviewMouseMove;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.PreviewMouseDown -= OnPreviewMouseDown;
            AssociatedObject.PreviewMouseMove -= OnPreviewMouseMove;
            base.OnDetaching();
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _lastMovePosition = e.GetPosition(AssociatedObject);
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            var movePosition = e.GetPosition(AssociatedObject);

            bool buttonPressed = e.LeftButton == MouseButtonState.Pressed
                || e.RightButton == MouseButtonState.Pressed
                || e.MiddleButton == MouseButtonState.Pressed;

            if (buttonPressed && movePosition == _lastMovePosition)
            {
                e.Handled = true;
                return;
            }

            _lastMovePosition = movePosition;
        }
    }
}
