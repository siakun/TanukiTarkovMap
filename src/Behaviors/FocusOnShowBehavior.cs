using System.Windows;
using System.Windows.Threading;
using Microsoft.Xaml.Behaviors;

namespace TanukiTarkovMap.Behaviors
{

    public class FocusOnShowBehavior : Behavior<UIElement>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.IsVisibleChanged += OnIsVisibleChanged;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.IsVisibleChanged -= OnIsVisibleChanged;
            base.OnDetaching();
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is not true) return;
            AssociatedObject.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (AssociatedObject?.IsVisible == true) AssociatedObject.Focus();
            });
        }
    }
}
