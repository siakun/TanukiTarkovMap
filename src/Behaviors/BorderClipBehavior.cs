using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;

namespace TanukiTarkovMap.Behaviors
{
    public class BorderClipBehavior : Behavior<Border>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.SizeChanged += OnSizeChanged;
            AssociatedObject.Loaded += OnLoaded;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.SizeChanged -= OnSizeChanged;
            AssociatedObject.Loaded -= OnLoaded;
            base.OnDetaching();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateClip();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateClip();
        }

        private void UpdateClip()
        {
            var border = AssociatedObject;
            if (border.ActualWidth <= 0 || border.ActualHeight <= 0)
                return;

            var cornerRadius = border.CornerRadius;

            var clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, border.ActualWidth, border.ActualHeight),
                RadiusX = cornerRadius.TopLeft,
                RadiusY = cornerRadius.TopLeft
            };

            border.Clip = clip;
        }
    }
}
