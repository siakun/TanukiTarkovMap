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
    /// <summary>
    /// TopBar 자동 숨김/표시 애니메이션 Behavior
    /// 핀 모드(IsAlwaysOnTop)가 활성화된 경우에만 작동
    /// 창 활성화/비활성화 및 마우스 호버에 따라 TopBar 표시/숨김
    /// 마우스가 창을 떠나면 HideDelayMs 후에 숨김 (그 전에 돌아오면 취소)
    /// 보더 색상도 HideDelayMs 동안 점진적으로 변경
    /// </summary>
    public class TopBarAnimationBehavior : Behavior<Window>
    {
        private TranslateTransform? _topBarTransform;
        private Border? _browserContainer;
        private Border? _contentBorder;
        private DispatcherTimer? _hideDelayTimer;
        private const double TopBarHeight = 20.0;
        private const int AnimationDurationMs = 200;
        private const int HideDelayMs = 1500;

        // 보더 색상 (App.xaml 리소스에서 참조)
        private static Color ActiveBorderColor => (Color)Application.Current.Resources["ActiveBorderColor"];
        private static Color InactiveBorderColor => (Color)Application.Current.Resources["InactiveBorderColor"];

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
            _contentBorder = AssociatedObject.FindName("ContentBorder") as Border;
        }

        private static void OnIsAlwaysOnTopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = (TopBarAnimationBehavior)d;
            var newValue = (bool)e.NewValue;

            // 핀 모드가 비활성화되면 TopBar를 보이는 상태로 복원하고 보더 색상 제어 해제
            if (!newValue)
            {
                behavior.CancelHideTimer();
                behavior.AnimateTopBar(0);
                behavior.ClearBorderColorAnimation(); // XAML 트리거 복원
            }
        }

        private void OnWindowActivated(object? sender, System.EventArgs e)
        {
            if (IsAlwaysOnTop)
            {
                CancelHideTimer();
                AnimateTopBar(0); // TopBar 보이기
                AnimateBorderColor(ActiveBorderColor, AnimationDurationMs); // 보더 색상 복원
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
                AnimateBorderColor(ActiveBorderColor, AnimationDurationMs); // 보더 색상 복원
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
        /// 숨김 타이머 시작 (HideDelayMs 후 TopBar 숨김)
        /// 보더 색상도 HideDelayMs 동안 점진적으로 비활성 색상으로 변경
        /// </summary>
        private void StartHideTimer()
        {
            CancelHideTimer();

            // 보더 색상 애니메이션 시작 (HideDelayMs 동안 점진적으로 변경)
            AnimateBorderColor(InactiveBorderColor, HideDelayMs);

            _hideDelayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(HideDelayMs)
            };
            _hideDelayTimer.Tick += OnHideTimerTick;
            _hideDelayTimer.Start();
        }

        /// <summary>
        /// 숨김 타이머 취소 (타이머만 정리, 색상 복원은 호출자가 처리)
        /// </summary>
        private void CancelHideTimer()
        {
            StopHideTimer();
        }

        /// <summary>
        /// 타이머 만료 시 TopBar 숨기기
        /// </summary>
        private void OnHideTimerTick(object? sender, EventArgs e)
        {
            StopHideTimer(); // 타이머만 정리 (색상 복원 없이)
            AnimateTopBar(-TopBarHeight); // TopBar 숨기기
        }

        /// <summary>
        /// 타이머만 정리 (색상 복원 없이)
        /// </summary>
        private void StopHideTimer()
        {
            if (_hideDelayTimer != null)
            {
                _hideDelayTimer.Stop();
                _hideDelayTimer.Tick -= OnHideTimerTick;
                _hideDelayTimer = null;
            }
        }

        /// <summary>
        /// TopBar 애니메이션 실행
        /// </summary>
        /// <param name="targetY">목표 Y 위치 (0: 보이기, -20: 숨기기)</param>
        private void AnimateTopBar(double targetY)
        {
            try
            {
                // TopBar 숨김 상태 메시지 전송 (true = 숨김, false = 보임)
                bool isHidden = targetY < 0;
                WeakReferenceMessenger.Default.Send(new TopBarHiddenChangedMessage(isHidden));

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

        /// <summary>
        /// 보더 색상 애니메이션
        /// </summary>
        /// <param name="targetColor">목표 색상</param>
        /// <param name="durationMs">애니메이션 지속 시간(ms)</param>
        private void AnimateBorderColor(Color targetColor, int durationMs)
        {
            if (_contentBorder == null)
                return;

            try
            {
                // 현재 브러시가 애니메이션 가능한지 확인
                var currentBrush = _contentBorder.BorderBrush as SolidColorBrush;
                if (currentBrush == null || currentBrush.IsFrozen)
                {
                    // Frozen 브러시면 새로운 애니메이션 가능한 브러시로 교체
                    var currentColor = currentBrush?.Color ?? InactiveBorderColor;
                    currentBrush = new SolidColorBrush(currentColor);
                    _contentBorder.BorderBrush = currentBrush;
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
                // 애니메이션 오류 무시
            }
        }

        /// <summary>
        /// 보더 색상 애니메이션 제거 및 XAML 트리거 복원
        /// </summary>
        private void ClearBorderColorAnimation()
        {
            if (_contentBorder == null)
                return;

            try
            {
                // 애니메이션 제거하고 null로 설정하면 XAML Style이 다시 적용됨
                _contentBorder.ClearValue(Border.BorderBrushProperty);
            }
            catch (System.Exception)
            {
                // 오류 무시
            }
        }
    }
}
