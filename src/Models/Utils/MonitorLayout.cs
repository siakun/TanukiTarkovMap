using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using TanukiTarkovMap.Core.Utils;

namespace TanukiTarkovMap.Models.Utils
{
    public readonly record struct ConnectedMonitor(PixelRect Bounds, PixelRect WorkArea, double Scale);

    public static class MonitorLayout
    {
        public static bool ContainsLogicalPoint(IEnumerable<ConnectedMonitor> monitors, Point logical) =>
            monitors.Any(monitor => LogicalBounds(monitor).Contains(logical));

        public static bool IsOnAnyMonitor(Point logical) => ContainsLogicalPoint(CurrentMonitors(), logical);

        public static (PixelRect Bounds, double Scale)? RestoreBounds(IReadOnlyList<ConnectedMonitor> monitors, Rect logical)
        {
            var scales = monitors.Where(monitor => LogicalBounds(monitor).Contains(logical.TopLeft))
                .Concat(monitors)
                .Select(monitor => monitor.Scale)
                .Distinct();
            foreach (var scale in scales)
            {
                var bounds = ToPixels(logical, scale);
                if (Owner(monitors, bounds)?.Scale == scale) return (bounds, scale);
            }
            return null;
        }

        public static (PixelRect Bounds, double Scale)? KeepOnScreen(IReadOnlyList<ConnectedMonitor> monitors, PixelRect window, double windowScale)
        {
            if (monitors.Count == 0 || window.Width <= 0 || window.Height <= 0) return null;
            if (monitors.Sum(monitor => Area(Intersect(window, monitor.Bounds))) >= Area(window)) return null;

            var target = Owner(monitors, window) ?? Nearest(monitors, window);
            var work = target.WorkArea;
            var ratio = target.Scale / windowScale;
            int width = Math.Min(Round(window.Width * ratio), work.Width);
            int height = Math.Min(Round(window.Height * ratio), work.Height);
            int left = Math.Clamp(window.Left, work.Left, work.Right - width);
            int top = Math.Clamp(window.Top, work.Top, work.Bottom - height);
            return (new PixelRect(left, top, left + width, top + height), target.Scale);
        }

        public static IReadOnlyList<ConnectedMonitor> CurrentMonitors()
        {
            var monitors = new List<ConnectedMonitor>();
            PInvoke.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr handle, IntPtr _, ref RECT _, IntPtr _) =>
            {
                var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
                if (!PInvoke.GetMonitorInfo(handle, ref info)) return true;
                var scale = PInvoke.GetDpiForMonitor(handle, PInvoke.MDT_EFFECTIVE_DPI, out var dpi, out _) == 0 && dpi > 0
                    ? dpi / 96.0 : 1.0;
                monitors.Add(new(ToPixelRect(info.rcMonitor), ToPixelRect(info.rcWork), scale));
                return true;
            }, IntPtr.Zero);
            return monitors;
        }

        public static void RestoreWindow(IntPtr hwnd, Rect logical)
        {
            if (!TryGetPlacement(hwnd, out var current)) return;
            var monitors = CurrentMonitors();
            var restored = RestoreBounds(monitors, logical) ?? current;
            var target = KeepOnScreen(monitors, restored.Bounds, restored.Scale) ?? restored;
            if (target != current) MoveWindow(hwnd, current, target, "Restored saved bounds");
        }

        public static void KeepWindowOnScreen(IntPtr hwnd)
        {
            if (!TryGetPlacement(hwnd, out var current)) return;
            if (KeepOnScreen(CurrentMonitors(), current.Bounds, current.Scale) is { } target)
                MoveWindow(hwnd, current, target, "Brought off-screen window back");
        }

        private static void MoveWindow(IntPtr hwnd, (PixelRect Bounds, double Scale) from, (PixelRect Bounds, double Scale) to, string reason)
        {
            const uint flags = PInvoke.SWP_NOZORDER | PInvoke.SWP_NOACTIVATE;
            var target = to.Bounds;
            if (from.Scale != to.Scale)
            {
                var source = HwndSource.FromHwnd(hwnd);
                source?.AddHook(KeepSizeOnDpiChange);
                try
                {
                    PInvoke.SetWindowPos(hwnd, 0, target.Left - 1, target.Top - 1, target.Width + 1, target.Height + 1, flags);
                }
                finally
                {
                    source?.RemoveHook(KeepSizeOnDpiChange);
                }
            }
            PInvoke.SetWindowPos(hwnd, 0, target.Left, target.Top, target.Width, target.Height, flags);

            var landed = TryGetPlacement(hwnd, out var placed) && placed == to;
            Logger.SimpleLog($"[MonitorLayout] {reason}: {Describe(from)} -> {Describe(to)}{(landed ? "" : $", but landed at {Describe(placed)}")}");
        }

        private static IntPtr KeepSizeOnDpiChange(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message != PInvoke.WM_GETDPISCALEDSIZE) return IntPtr.Zero;
            handled = true;
            return (IntPtr)1;
        }

        private static bool TryGetPlacement(IntPtr hwnd, out (PixelRect Bounds, double Scale) placement)
        {
            var found = PInvoke.GetWindowRect(hwnd, out var rect);
            var dpi = PInvoke.GetDpiForWindow(hwnd);
            placement = (ToPixelRect(rect), dpi > 0 ? dpi / 96.0 : 1.0);
            return found;
        }

        private static Rect LogicalBounds(ConnectedMonitor monitor) => new(monitor.Bounds.Left / monitor.Scale,
            monitor.Bounds.Top / monitor.Scale, monitor.Bounds.Width / monitor.Scale, monitor.Bounds.Height / monitor.Scale);

        private static PixelRect ToPixels(Rect logical, double scale)
        {
            int left = Round(logical.Left * scale), top = Round(logical.Top * scale);
            return new PixelRect(left, top, left + Round(logical.Width * scale), top + Round(logical.Height * scale));
        }

        private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

        private static ConnectedMonitor? Owner(IReadOnlyList<ConnectedMonitor> monitors, PixelRect rect)
        {
            ConnectedMonitor? owner = null;
            long largest = 0;
            foreach (var monitor in monitors)
            {
                var area = Area(Intersect(rect, monitor.Bounds));
                if (area > largest) (owner, largest) = (monitor, area);
            }
            return owner;
        }

        private static ConnectedMonitor Nearest(IReadOnlyList<ConnectedMonitor> monitors, PixelRect rect) =>
            monitors.MinBy(monitor => SquaredDistance(rect, monitor.Bounds));

        private static long SquaredDistance(PixelRect a, PixelRect b)
        {
            long dx = Math.Max(0, Math.Max(b.Left - a.Right, a.Left - b.Right));
            long dy = Math.Max(0, Math.Max(b.Top - a.Bottom, a.Top - b.Bottom));
            return dx * dx + dy * dy;
        }

        private static PixelRect Intersect(PixelRect a, PixelRect b) =>
            new(Math.Max(a.Left, b.Left), Math.Max(a.Top, b.Top), Math.Min(a.Right, b.Right), Math.Min(a.Bottom, b.Bottom));

        private static long Area(PixelRect rect) => rect.Width > 0 && rect.Height > 0 ? (long)rect.Width * rect.Height : 0;

        private static PixelRect ToPixelRect(RECT rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

        private static string Describe((PixelRect Bounds, double Scale) placement) =>
            $"({placement.Bounds.Left},{placement.Bounds.Top} {placement.Bounds.Width}x{placement.Bounds.Height} @{Round(placement.Scale * 96)}dpi)";
    }
}
