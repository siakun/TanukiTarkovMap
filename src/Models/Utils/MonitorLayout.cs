using System.Windows;

namespace TanukiTarkovMap.Models.Utils
{

    public static class MonitorLayout
    {
        public static bool ContainsLogicalPoint(IEnumerable<(Rect Pixels, double Scale)> monitors, Point logical) =>
            monitors.Any(monitor => new Rect(monitor.Pixels.X / monitor.Scale, monitor.Pixels.Y / monitor.Scale,
                monitor.Pixels.Width / monitor.Scale, monitor.Pixels.Height / monitor.Scale).Contains(logical));

        public static bool IsOnAnyMonitor(Point logical) => ContainsLogicalPoint(CurrentMonitors(), logical);

        private static List<(Rect Pixels, double Scale)> CurrentMonitors()
        {
            var monitors = new List<(Rect Pixels, double Scale)>();
            PInvoke.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr handle, IntPtr _, ref RECT bounds, IntPtr _) =>
            {
                var scale = PInvoke.GetDpiForMonitor(handle, PInvoke.MDT_EFFECTIVE_DPI, out var dpi, out _) == 0 && dpi > 0
                    ? dpi / 96.0 : 1.0;
                monitors.Add((new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height), scale));
                return true;
            }, IntPtr.Zero);
            return monitors;
        }
    }
}
