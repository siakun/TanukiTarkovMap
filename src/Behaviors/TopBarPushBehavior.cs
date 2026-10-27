using System.Windows;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;

namespace TanukiTarkovMap.Behaviors
{

    public class TopBarPushBehavior : Behavior<FrameworkElement>
    {
        public static readonly DependencyProperty IsPushedProperty =
            DependencyProperty.Register(
                nameof(IsPushed),
                typeof(bool),
                typeof(TopBarPushBehavior),
                new PropertyMetadata(false, OnIsPushedChanged));

        public bool IsPushed
        {
            get => (bool)GetValue(IsPushedProperty);
            set => SetValue(IsPushedProperty, value);
        }

        private readonly TranslateTransform _offset = new();

        private double TargetY => IsPushed ? TopBarAnimationBehavior.TopBarHeight : 0;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.RenderTransform = _offset;
            _offset.Y = TargetY;
        }

        private static void OnIsPushedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var behavior = (TopBarPushBehavior)sender;
            if (behavior.AssociatedObject == null) return;

            if (!behavior.AssociatedObject.IsLoaded)
            {
                behavior._offset.BeginAnimation(TranslateTransform.YProperty, null);
                behavior._offset.Y = behavior.TargetY;
                return;
            }
            behavior._offset.BeginAnimation(TranslateTransform.YProperty, TopBarAnimationBehavior.CreateSlideAnimation(behavior.TargetY));
        }
    }
}
