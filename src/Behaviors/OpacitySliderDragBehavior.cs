using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Messages;

namespace TanukiTarkovMap.Behaviors
{

    public class OpacitySliderDragBehavior : Behavior<Slider>
    {
        private Thumb? _thumb;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += OnSliderLoaded;
        }

        protected override void OnDetaching()
        {
            if (_thumb != null)
            {
                _thumb.DragStarted -= OnDragStarted;
                _thumb.DragCompleted -= OnDragCompleted;
            }

            AssociatedObject.Loaded -= OnSliderLoaded;
            base.OnDetaching();
        }

        private void OnSliderLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            AssociatedObject.ApplyTemplate();
            var track = AssociatedObject.Template.FindName("PART_Track", AssociatedObject) as Track;
            _thumb = track?.Thumb;

            if (_thumb != null)
            {
                _thumb.DragStarted += OnDragStarted;
                _thumb.DragCompleted += OnDragCompleted;
            }
        }

        private void OnDragStarted(object sender, DragStartedEventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new OpacitySliderDragMessage(true));
        }

        private void OnDragCompleted(object sender, DragCompletedEventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new OpacitySliderDragMessage(false));
        }
    }
}
