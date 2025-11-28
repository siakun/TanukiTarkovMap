using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Behaviors
{
    /// <summary>
    /// Compact 모드에서 창 드래그 이동 Behavior
    /// WebView2 영역 외부를 클릭한 경우에만 드래그 허용
    /// </summary>
    public class CompactModeDragBehavior : Behavior<UIElement>
    {
        /// <summary>
        /// Compact 모드 활성화 여부 바인딩
        /// </summary>
        public static readonly DependencyProperty IsCompactModeProperty =
            DependencyProperty.Register(
                nameof(IsCompactMode),
                typeof(bool),
                typeof(CompactModeDragBehavior),
                new PropertyMetadata(false));

        public bool IsCompactMode
        {
            get => (bool)GetValue(IsCompactModeProperty);
            set => SetValue(IsCompactModeProperty, value);
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
            // Compact 모드일 때만 드래그 가능
            if (!IsCompactMode || e.ButtonState != MouseButtonState.Pressed)
                return;

            var window = Window.GetWindow(AssociatedObject);
            if (window == null)
                return;

            // WebView2 영역 외부를 클릭한 경우만 드래그
            var position = e.GetPosition(window);
            var hitElement = window.InputHitTest(position) as DependencyObject;

            // WebView2가 아닌 경우에만 드래그 허용
            bool isWebView2 = false;
            while (hitElement != null)
            {
                if (hitElement is WebView2)
                {
                    isWebView2 = true;
                    break;
                }
                hitElement = System.Windows.Media.VisualTreeHelper.GetParent(hitElement);
            }

            if (!isWebView2)
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                PInvoke.ReleaseCapture();
                PInvoke.SendMessage(handle, PInvoke.WM_NCLBUTTONDOWN, PInvoke.HT_CAPTION, 0);
            }
        }
    }
}
