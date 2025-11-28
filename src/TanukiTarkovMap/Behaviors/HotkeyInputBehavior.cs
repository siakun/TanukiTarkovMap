using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;

namespace TanukiTarkovMap.Behaviors
{
    /// <summary>
    /// 핫키 입력 버튼에 대한 Behavior
    /// 버튼 클릭 시 키 입력 모드로 전환하고, 키 입력 시 해당 키를 캡처
    /// </summary>
    public class HotkeyInputBehavior : Behavior<Button>
    {
        private bool _isInputMode = false;
        private readonly SolidColorBrush _normalBrush = new(Color.FromRgb(0x3A, 0x3A, 0x3A));
        private readonly SolidColorBrush _inputModeBrush = new(Color.FromRgb(0x6A, 0x6A, 0x2A));

        public static readonly DependencyProperty HotkeyProperty =
            DependencyProperty.Register(
                nameof(Hotkey),
                typeof(string),
                typeof(HotkeyInputBehavior),
                new FrameworkPropertyMetadata("F11", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        /// <summary>
        /// 바인딩된 핫키 값
        /// </summary>
        public string Hotkey
        {
            get => (string)GetValue(HotkeyProperty);
            set => SetValue(HotkeyProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Click += OnClick;
            AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
            AssociatedObject.LostFocus += OnLostFocus;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.Click -= OnClick;
            AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
            AssociatedObject.LostFocus -= OnLostFocus;
            base.OnDetaching();
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            AssociatedObject.Content = "키를 눌러주세요...";
            AssociatedObject.Background = _inputModeBrush;
            AssociatedObject.Focus();
            _isInputMode = true;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isInputMode)
                return;

            // Tab 키는 포커스 이동을 위해 허용
            if (e.Key == Key.Tab)
                return;

            string keyString = e.Key.ToString();
            if (!string.IsNullOrEmpty(keyString))
            {
                Hotkey = keyString;
                ExitInputMode();
                AssociatedObject.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }

            e.Handled = true;
        }

        private void OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (_isInputMode)
            {
                ExitInputMode();
            }
        }

        private void ExitInputMode()
        {
            _isInputMode = false;
            AssociatedObject.Background = _normalBrush;
            // Content는 바인딩으로 자동 복원됨
        }
    }
}
