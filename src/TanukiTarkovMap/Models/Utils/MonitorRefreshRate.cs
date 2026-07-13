using System.Runtime.InteropServices;

namespace TanukiTarkovMap.Models.Utils
{
    /// <summary>
    /// 윈도우가 위치한 모니터의 현재 주사율(Hz)을 조회하는 유틸리티
    ///
    /// 조회 경로: MonitorFromWindow → GetMonitorInfo(장치명) → EnumDisplaySettings(현재 모드)
    /// 멀티모니터 환경에서 창이 있는 모니터 기준으로 동작한다
    /// </summary>
    internal static class MonitorRefreshRate
    {
        /// <summary> 조회 실패 시 사용하는 안전한 기본 주사율 </summary>
        internal const int FallbackHz = 60;

        /// <summary>
        /// 윈도우가 표시된 모니터의 핸들을 가져온다 (모니터 이동 감지용)
        /// </summary>
        internal static IntPtr GetMonitorHandle(IntPtr windowHandle)
            => PInvoke.MonitorFromWindow(windowHandle, PInvoke.MONITOR_DEFAULTTONEAREST);

        /// <summary>
        /// 윈도우가 표시된 모니터의 현재 주사율(Hz)을 가져온다. 조회 실패 시 60 반환
        /// </summary>
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

            // dmDisplayFrequency의 0과 1은 "하드웨어 기본값"을 뜻하는 예약값이라 실제 주사율이 아니다
            return devMode.dmDisplayFrequency > 1 ? (int)devMode.dmDisplayFrequency : FallbackHz;
        }
    }
}
