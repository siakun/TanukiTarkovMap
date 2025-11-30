using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;

namespace TanukiTarkovMap.Behaviors
{
    /// <summary>
    /// Border의 CornerRadius에 맞게 자식 요소를 클리핑하는 Behavior
    /// Border.CornerRadius만으로는 자식 요소가 클리핑되지 않는 WPF 한계를 해결
    /// </summary>
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

            // RectangleGeometry로 둥근 모서리 클리핑 생성
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
