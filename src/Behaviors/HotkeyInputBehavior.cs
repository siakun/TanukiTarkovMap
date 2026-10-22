using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Localization;
using TanukiTarkovMap.Core.Data;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.Core.Utils;

namespace TanukiTarkovMap.Behaviors
{
    public class HotkeyInputBehavior : Behavior<Button>
    {
        public static bool IsInInputMode
        {
            get => Volatile.Read(ref _isInInputMode);
            private set => Volatile.Write(ref _isInInputMode, value);
        }
        private static bool _isInInputMode;

        private readonly SolidColorBrush _normalBrush = new(Color.FromRgb(0x3A, 0x3A, 0x3A));
        private readonly SolidColorBrush _inputModeBrush = new(Color.FromRgb(0x6A, 0x6A, 0x2A));

        public static readonly DependencyProperty HotkeyProperty =
            DependencyProperty.Register(
                nameof(Hotkey),
                typeof(string),
                typeof(HotkeyInputBehavior),
                new FrameworkPropertyMetadata(AppSettings.DefaultHotkeyKey, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

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

            if (e.Key == Key.Tab || e.Key == Key.Escape)
            {
                ExitInputMode();
                e.Handled = true;
                return;
            }

            Key actualKey = e.Key == Key.System ? e.SystemKey : e.Key;

            if (actualKey == Key.LeftShift || actualKey == Key.RightShift ||
                actualKey == Key.LeftCtrl || actualKey == Key.RightCtrl ||
                actualKey == Key.LeftAlt || actualKey == Key.RightAlt ||
                actualKey == Key.LWin || actualKey == Key.RWin)
            {
                e.Handled = true;
                return;
            }

            string keyString = BuildHotkeyString(actualKey);
            if (!string.IsNullOrEmpty(keyString))
            {
                Hotkey = keyString;
                ExitInputMode();
            }

            e.Handled = true;
        }

        private string BuildHotkeyString(Key mainKey)
        {
            var parts = new List<string>();

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                parts.Add("Ctrl");
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                parts.Add("Alt");
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                parts.Add("Shift");
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows))
                parts.Add("Win");

            parts.Add(mainKey.ToString());

            return string.Join("+", parts);
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
            AssociatedObject.Content = Strings.Hotkey_PressKey;
            AssociatedObject.Background = _inputModeBrush;
            AssociatedObject.Focus();
            Logger.SimpleLog("[HotkeyInputBehavior] Entered input mode");
        }

        private void ExitInputMode()
        {
            IsInInputMode = false;
            AssociatedObject.Background = _normalBrush;
            AssociatedObject.Content = Hotkey;
            Logger.SimpleLog($"[HotkeyInputBehavior] Exited input mode, Hotkey={Hotkey}");
        }
    }
}
