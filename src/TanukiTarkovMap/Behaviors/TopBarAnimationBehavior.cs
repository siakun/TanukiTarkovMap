using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Xaml.Behaviors;

namespace TanukiTarkovMap.Behaviors
{
    /// <summary>
    /// TopBar 자동 숨김/표시 애니메이션 Behavior
    /// 핀 모드(IsAlwaysOnTop)가 활성화된 경우에만 작동
    /// 창 활성화/비활성화 및 마우스 호버에 따라 TopBar 표시/숨김
    /// 마우스가 창을 떠나면 2.5초 후에 숨김 (그 전에 돌아오면 취소)
    /// </summary>
    public class TopBarAnimationBehavior : Behavior<Window>
    {
        private TranslateTransform? _topBarTransform;
        private Border? _browserContainer;
        private DispatcherTimer? _hideDelayTimer;
        private const double TopBarHeight = 20.0;
        private const int AnimationDurationMs = 200;
        private const int HideDelayMs = 2500;

        #region Dependency Properties

        /// <summary>
        /// 핀 모드(항상 위) 활성화 여부
        /// </summary>
        public static readonly DependencyProperty IsAlwaysOnTopProperty =
            DependencyProperty.Register(
                nameof(IsAlwaysOnTop),
                typeof(bool),
                typeof(TopBarAnimationBehavior),
                new PropertyMetadata(false, OnIsAlwaysOnTopChanged));

        public bool IsAlwaysOnTop
        {
            get => (bool)GetValue(IsAlwaysOnTopProperty);
            set => SetValue(IsAlwaysOnTopProperty, value);
        }

        /// <summary>
        /// TopBar의 TranslateTransform 이름 (XAML에서 x:Name으로 지정)
        /// </summary>
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

        /// <summary>
        /// BrowserContainer 이름 (XAML에서 x:Name으로 지정)
        /// </summary>
        public static readonly DependencyProperty BrowserContainerNameProperty =
            DependencyProperty.Register(
                nameof(BrowserContainerName),
                typeof(string),
                typeof(TopBarAnimationBehavior),
                new PropertyMetadata("BrowserContainer"));

        public string BrowserContainerName
        {
            get => (string)GetValue(BrowserContainerNameProperty);
            set => SetValue(BrowserContainerNameProperty, value);
        }

        #endregion

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
            // XAML에서 정의된 요소 찾기
            _topBarTransform = AssociatedObject.FindName(TopBarTransformName) as TranslateTransform;
            _browserContainer = AssociatedObject.FindName(BrowserContainerName) as Border;
        }

        private static void OnIsAlwaysOnTopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = (TopBarAnimationBehavior)d;
            var newValue = (bool)e.NewValue;

            // 핀 모드가 비활성화되면 TopBar를 보이는 상태로 복원
            if (!newValue)
            {
                behavior.AnimateTopBar(0);
            }
        }

        private void OnWindowActivated(object? sender, System.EventArgs e)
        {
            if (IsAlwaysOnTop)
            {
                CancelHideTimer();
                AnimateTopBar(0); // TopBar 보이기
            }
        }

        private void OnWindowDeactivated(object? sender, System.EventArgs e)
        {
            if (IsAlwaysOnTop)
            {
                // 창이 비활성화되면 타이머 시작 (마우스가 돌아오면 취소됨)
                StartHideTimer();
            }
        }

        private void OnWindowMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (IsAlwaysOnTop)
            {
                CancelHideTimer();
                AnimateTopBar(0); // TopBar 보이기
            }
        }

        private void OnWindowMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (IsAlwaysOnTop)
            {
                StartHideTimer();
            }
        }

        /// <summary>
        /// 숨김 타이머 시작 (2.5초 후 TopBar 숨김)
        /// </summary>
        private void StartHideTimer()
        {
            CancelHideTimer();

            _hideDelayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(HideDelayMs)
            };
            _hideDelayTimer.Tick += OnHideTimerTick;
            _hideDelayTimer.Start();
        }

        /// <summary>
        /// 숨김 타이머 취소
        /// </summary>
        private void CancelHideTimer()
        {
            if (_hideDelayTimer != null)
            {
                _hideDelayTimer.Stop();
                _hideDelayTimer.Tick -= OnHideTimerTick;
                _hideDelayTimer = null;
            }
        }

        /// <summary>
        /// 타이머 만료 시 TopBar 숨기기
        /// </summary>
        private void OnHideTimerTick(object? sender, EventArgs e)
        {
            CancelHideTimer();
            AnimateTopBar(-TopBarHeight); // TopBar 숨기기
        }

        /// <summary>
        /// TopBar 애니메이션 실행
        /// </summary>
        /// <param name="targetY">목표 Y 위치 (0: 보이기, -20: 숨기기)</param>
        private void AnimateTopBar(double targetY)
        {
            try
            {
                // TopBar 위치 애니메이션
                if (_topBarTransform != null)
                {
                    var topBarAnimation = new DoubleAnimation
                    {
                        To = targetY,
                        Duration = TimeSpan.FromMilliseconds(AnimationDurationMs),
                        EasingFunction = new CubicEase
                        {
                            EasingMode = EasingMode.EaseInOut
                        }
                    };
                    _topBarTransform.BeginAnimation(TranslateTransform.YProperty, topBarAnimation);
                }

                // BrowserContainer Margin.Top 애니메이션
                // targetY = 0 (보이기) -> Margin.Top = 20
                // targetY = -20 (숨기기) -> Margin.Top = 0
                if (_browserContainer != null)
                {
                    var targetMarginTop = targetY == 0 ? TopBarHeight : 0.0;
                    var marginAnimation = new ThicknessAnimation
                    {
                        To = new Thickness(0, targetMarginTop, 0, 0),
                        Duration = TimeSpan.FromMilliseconds(AnimationDurationMs),
                        EasingFunction = new CubicEase
                        {
                            EasingMode = EasingMode.EaseInOut
                        }
                    };
                    _browserContainer.BeginAnimation(Border.MarginProperty, marginAnimation);
                }
            }
            catch (System.Exception)
            {
                // 애니메이션 오류 무시
            }
        }
    }
}
