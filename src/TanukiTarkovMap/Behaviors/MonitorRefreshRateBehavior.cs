using System.Windows;
using System.Windows.Interop;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Messages;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Behaviors
{
    /// <summary>
    /// 창이 위치한 모니터를 추적해 그 모니터의 현재 주사율을 Messenger로 발행하는 Behavior
    ///
    /// 발행 시점:
    /// - 창 로드 직후 (초기값)
    /// - 창 이동으로 모니터가 바뀌었을 때 (LocationChanged)
    /// - 디스플레이 설정이 바뀌었을 때 (SystemEvents.DisplaySettingsChanged, 주사율/해상도/모니터 구성 변경)
    ///
    /// 수신자: WebBrowserViewModel (CefSharp OSR의 WindowlessFrameRate를 모니터 주사율에 맞춤)
    /// </summary>
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
            // SystemEvents는 static 이벤트라 해제하지 않으면 Behavior 인스턴스가 누수된다
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

            // 창 드래그 중 매번 호출되므로, 모니터 핸들이 바뀐 경우에만 주사율을 재조회한다
            if (MonitorRefreshRate.GetMonitorHandle(_windowHandle) == _lastMonitorHandle)
                return;

            PublishRefreshRate();
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            // 같은 모니터라도 주사율 자체가 바뀔 수 있으므로 무조건 재조회
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
