using System.Runtime.InteropServices;

namespace TanukiTarkovMap.Models.Utils
{
    internal static class MonitorRefreshRate
    {
        internal const int FallbackHz = 60;

        internal static IntPtr GetMonitorHandle(IntPtr windowHandle)
            => PInvoke.MonitorFromWindow(windowHandle, PInvoke.MONITOR_DEFAULTTONEAREST);

        internal static int GetForWindow(IntPtr windowHandle)
        {
            var monitorHandle = GetMonitorHandle(windowHandle);
            if (monitorHandle == IntPtr.Zero)
                return FallbackHz;

            var monitorInfo = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (!PInvoke.GetMonitorInfo(monitorHandle, ref monitorInfo))
                return FallbackHz;

            var devMode = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
            if (!PInvoke.EnumDisplaySettings(monitorInfo.szDevice, PInvoke.ENUM_CURRENT_SETTINGS, ref devMode))
                return FallbackHz;

            return devMode.dmDisplayFrequency > 1 ? (int)devMode.dmDisplayFrequency : FallbackHz;
        }
    }
}
