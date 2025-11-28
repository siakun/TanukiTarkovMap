using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Behaviors
{
    /// <summary>
    /// 핫키 입력 버튼에 대한 Behavior
    /// 버튼 클릭 시 키 입력 모드로 전환하고, 키 입력 시 해당 키를 캡처
    /// </summary>
    public class HotkeyInputBehavior : Behavior<Button>
    {
        /// <summary>
        /// 현재 입력 모드 여부 (전역 핫키 비활성화용)
        /// </summary>
        public static bool IsInInputMode { get; private set; } = false;

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
            EnterInputMode();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!IsInInputMode)
                return;

            // Tab, Escape 키는 입력 모드 취소
            if (e.Key == Key.Tab || e.Key == Key.Escape)
            {
                ExitInputMode();
                e.Handled = true;
                return;
            }

            // 시스템 키 (Alt 등)는 실제 키로 변환
            Key actualKey = e.Key == Key.System ? e.SystemKey : e.Key;

            // 수정자 키만 눌린 경우 무시
            if (actualKey == Key.LeftShift || actualKey == Key.RightShift ||
                actualKey == Key.LeftCtrl || actualKey == Key.RightCtrl ||
                actualKey == Key.LeftAlt || actualKey == Key.RightAlt ||
                actualKey == Key.LWin || actualKey == Key.RWin)
            {
                e.Handled = true;
                return;
            }

            string keyString = actualKey.ToString();
            if (!string.IsNullOrEmpty(keyString))
            {
                Hotkey = keyString;
                ExitInputMode();
            }

            e.Handled = true;
        }

        private void OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (IsInInputMode)
            {
                ExitInputMode();
            }
        }

        private void EnterInputMode()
        {
            IsInInputMode = true;
            AssociatedObject.Content = "키를 눌러주세요...";
            AssociatedObject.Background = _inputModeBrush;
            AssociatedObject.Focus();
            Logger.SimpleLog("[HotkeyInputBehavior] Entered input mode");
        }

        private void ExitInputMode()
        {
            IsInInputMode = false;
            AssociatedObject.Background = _normalBrush;
            // Content를 현재 Hotkey 값으로 복원
            AssociatedObject.Content = Hotkey;
            Logger.SimpleLog($"[HotkeyInputBehavior] Exited input mode, Hotkey={Hotkey}");
        }
    }
}
