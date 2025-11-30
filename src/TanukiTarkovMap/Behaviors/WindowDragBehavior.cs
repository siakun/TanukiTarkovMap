using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Behaviors
{
    /// <summary>
    /// 타이틀바 드래그로 창 이동하는 Behavior
    /// 더블클릭 시 최대화/복원 동작 포함
    /// </summary>
    public class WindowDragBehavior : Behavior<UIElement>
    {
        /// <summary>
        /// 더블클릭 시 최대화/복원 기능 활성화 여부
        /// </summary>
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

            // 더블클릭 처리
            if (e.ClickCount == 2)
            {
                if (EnableMaximizeOnDoubleClick)
                {
                    if (window.WindowState == WindowState.Maximized)
                        window.WindowState = WindowState.Normal;
                    else
                        window.WindowState = WindowState.Maximized;
                }
                // 더블클릭 시 WM_NCLBUTTONDOWN을 보내지 않음 (Windows 기본 최대화 방지)
                return;
            }

            // 단일 클릭 시 드래그 이동
            var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            PInvoke.ReleaseCapture();
            PInvoke.SendMessage(handle, PInvoke.WM_NCLBUTTONDOWN, PInvoke.HT_CAPTION, 0);
        }
    }
}
