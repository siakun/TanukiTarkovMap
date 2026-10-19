using System.Windows;
using System.Windows.Interop;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Messages;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.Core.Utils;

namespace TanukiTarkovMap.Behaviors
{
    public class MonitorRefreshRateBehavior : Behavior<Window>
    {
        private IntPtr _windowHandle = IntPtr.Zero;
        private IntPtr _lastMonitorHandle = IntPtr.Zero;
        private int _lastRefreshRate = 0;

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.Loaded += OnWindowLoaded;
            AssociatedObject.LocationChanged += OnLocationChanged;
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            AssociatedObject.Loaded -= OnWindowLoaded;
            AssociatedObject.LocationChanged -= OnLocationChanged;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _windowHandle = new WindowInteropHelper(AssociatedObject).Handle;
            PublishRefreshRate();
        }

        private void OnLocationChanged(object? sender, EventArgs e)
        {
            if (_windowHandle == IntPtr.Zero)
                return;

            if (MonitorRefreshRate.GetMonitorHandle(_windowHandle) == _lastMonitorHandle)
                return;

            PublishRefreshRate();
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            if (_windowHandle == IntPtr.Zero)
                return;

            PublishRefreshRate();
        }

        private void PublishRefreshRate()
        {
            _lastMonitorHandle = MonitorRefreshRate.GetMonitorHandle(_windowHandle);

            int refreshRate = MonitorRefreshRate.GetForWindow(_windowHandle);
            if (refreshRate == _lastRefreshRate)
                return;

            _lastRefreshRate = refreshRate;
            WeakReferenceMessenger.Default.Send(new MonitorRefreshRateChangedMessage(refreshRate));
            Logger.SimpleLog($"[MonitorRefreshRateBehavior] Monitor refresh rate changed: {refreshRate}Hz");
        }
    }
}
