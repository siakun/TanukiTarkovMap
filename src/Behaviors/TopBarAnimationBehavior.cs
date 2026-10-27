using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Messages;

namespace TanukiTarkovMap.Behaviors
{

    public class TopBarAnimationBehavior : Behavior<Window>
    {
        private TranslateTransform? _topBarTransform;
        private Border? _windowOutline;
        private DispatcherTimer? _hideDelayTimer;
        private long _revealSuppressedUntil;
        internal const double TopBarHeight = 20.0;
        private const int AnimationDurationMs = 200;
        internal const int HideDelayMs = 1500;
        private const int DodgeRevealGuardMs = 150;

        private static Color ActiveBorderColor => (Color)Application.Current.Resources["ActiveBorderColor"];
        private static Color InactiveBorderColor => (Color)Application.Current.Resources["InactiveBorderColor"];

        public static readonly DependencyProperty TopBarTransformNameProperty =
            DependencyProperty.Register(
                nameof(TopBarTransformName),
                typeof(string),
                typeof(TopBarAnimationBehavior),
                new PropertyMetadata("TopBarTransform"));

        public string TopBarTransformName
        {
            get => (string)GetValue(TopBarTransformNameProperty);
            set => SetValue(TopBarTransformNameProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.Loaded += OnWindowLoaded;
            AssociatedObject.Activated += OnWindowActivated;
            AssociatedObject.Deactivated += OnWindowDeactivated;
            AssociatedObject.MouseEnter += OnWindowMouseEnter;
            AssociatedObject.MouseLeave += OnWindowMouseLeave;
        }

        protected override void OnDetaching()
        {
            CancelHideTimer();

            AssociatedObject.Loaded -= OnWindowLoaded;
            AssociatedObject.Activated -= OnWindowActivated;
            AssociatedObject.Deactivated -= OnWindowDeactivated;
            AssociatedObject.MouseEnter -= OnWindowMouseEnter;
            AssociatedObject.MouseLeave -= OnWindowMouseLeave;

            base.OnDetaching();
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _topBarTransform = AssociatedObject.FindName(TopBarTransformName) as TranslateTransform;
            _windowOutline = AssociatedObject.FindName("WindowOutline") as Border;
        }

        private void OnWindowActivated(object? sender, System.EventArgs e)
        {
            CancelHideTimer();
            AnimateTopBar(0);
            AnimateBorderColor(ActiveBorderColor, AnimationDurationMs);
        }

        private void OnWindowDeactivated(object? sender, System.EventArgs e)
        {
            StartHideTimer();
        }

        private void OnWindowMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (Environment.TickCount64 < _revealSuppressedUntil) return;
            CancelHideTimer();
            AnimateTopBar(0);
            AnimateBorderColor(ActiveBorderColor, AnimationDurationMs);
        }

        private void OnWindowMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            StartHideTimer();
        }

        internal void HideAfterDodge()
        {
            _revealSuppressedUntil = Environment.TickCount64 + DodgeRevealGuardMs;
            CancelHideTimer();
            AnimateTopBar(-TopBarHeight);
            AnimateBorderColor(InactiveBorderColor, AnimationDurationMs);
        }

        private void StartHideTimer()
        {
            CancelHideTimer();

            AnimateBorderColor(InactiveBorderColor, HideDelayMs);

            _hideDelayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(HideDelayMs)
            };
            _hideDelayTimer.Tick += OnHideTimerTick;
            _hideDelayTimer.Start();
        }

        private void CancelHideTimer()
        {
            StopHideTimer();
        }

        private void OnHideTimerTick(object? sender, EventArgs e)
        {
            StopHideTimer();
            AnimateTopBar(-TopBarHeight);
        }

        private void StopHideTimer()
        {
            if (_hideDelayTimer != null)
            {
                _hideDelayTimer.Stop();
                _hideDelayTimer.Tick -= OnHideTimerTick;
                _hideDelayTimer = null;
            }
        }

        private void AnimateTopBar(double targetY)
        {
            try
            {
                bool isHidden = targetY < 0;
                WeakReferenceMessenger.Default.Send(new TopBarHiddenChangedMessage(isHidden));

                _topBarTransform?.BeginAnimation(TranslateTransform.YProperty, CreateSlideAnimation(targetY));
            }
            catch (System.Exception)
            {
            }
        }

        internal static DoubleAnimation CreateSlideAnimation(double targetY) => new()
        {
            To = targetY,
            Duration = TimeSpan.FromMilliseconds(AnimationDurationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };

        private void AnimateBorderColor(Color targetColor, int durationMs)
        {
            if (_windowOutline == null)
                return;

            try
            {
                var currentBrush = _windowOutline.BorderBrush as SolidColorBrush;
                if (currentBrush == null || currentBrush.IsFrozen)
                {
                    var currentColor = currentBrush?.Color ?? InactiveBorderColor;
                    currentBrush = new SolidColorBrush(currentColor);
                    _windowOutline.BorderBrush = currentBrush;
                }

                var animation = new ColorAnimation
                {
                    To = targetColor,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = new CubicEase
                    {
                        EasingMode = EasingMode.EaseInOut
                    }
                };

                currentBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
            }
            catch (System.Exception)
            {
            }
        }

    }
}
