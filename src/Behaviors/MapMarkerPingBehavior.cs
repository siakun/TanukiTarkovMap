using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Xaml.Behaviors;

namespace TanukiTarkovMap.Behaviors;

public class MapMarkerPingBehavior : Behavior<Canvas>
{
    public static readonly DependencyProperty SequenceProperty = DependencyProperty.Register(nameof(Sequence), typeof(int),
        typeof(MapMarkerPingBehavior), new PropertyMetadata(0, OnSequenceChanged));
    public int Sequence { get => (int)GetValue(SequenceProperty); set => SetValue(SequenceProperty, value); }
    private static void OnSequenceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var behavior = (MapMarkerPingBehavior)sender;
        if (behavior.AssociatedObject?.RenderTransform is not ScaleTransform scale || (int)args.NewValue <= 0) return;
        var animation = new DoubleAnimation(1, 2.5, TimeSpan.FromSeconds(0.35))
        {
            AutoReverse = true, FillBehavior = FillBehavior.Stop,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }
    }
}
