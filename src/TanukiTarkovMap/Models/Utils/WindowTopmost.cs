using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TanukiTarkovMap.Models.Utils
{
    /// <summary>
    /// Windows API를 사용한 강력한 최상단 유지 유틸리티
    /// 시스템 레벨에서 창을 최상단에 고정하는 기능 제공
    /// </summary>
    public static class WindowTopmost
    {

        /// <summary>
        /// 창을 시스템 레벨에서 최상단으로 강제 고정
        /// </summary>
        /// <param name="window">대상 WPF 창</param>
        /// <param name="activate">창을 활성화할지 여부</param>
        /// <returns>성공 여부</returns>
        public static bool SetTopmost(Window window, bool activate = true)
        {
            try
            {
                window.Topmost = true;

                var hwnd = GetWindowHandle(window);
                if (hwnd == IntPtr.Zero)
                {
                    return false;
                }

                PInvoke.ShowWindow(hwnd, PInvoke.SW_SHOWNOACTIVATE);

                uint flags = PInvoke.SWP_NOMOVE | PInvoke.SWP_NOSIZE | PInvoke.SWP_NOACTIVATE;
                bool result = PInvoke.SetWindowPos(hwnd, PInvoke.HWND_TOPMOST, 0, 0, 0, 0, flags);

                return result;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 창의 최상단 고정을 해제
        /// </summary>
        /// <param name="window">대상 WPF 창</param>
        /// <returns>성공 여부</returns>
        public static bool RemoveTopmost(Window window)
        {
            try
            {
                var hwnd = GetWindowHandle(window);
                if (hwnd == IntPtr.Zero)
                    return false;

                bool result = PInvoke.SetWindowPos(
                    hwnd,
                    PInvoke.HWND_NOTOPMOST,
                    0,
                    0,
                    0,
                    0,
                    PInvoke.SWP_NOMOVE | PInvoke.SWP_NOSIZE | PInvoke.SWP_NOACTIVATE
                );

                return result;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 창 핸들 획득 (안전한 방식)
        /// </summary>
        /// <param name="window">대상 WPF 창</param>
        /// <returns>창 핸들 (실패 시 IntPtr.Zero)</returns>
        private static IntPtr GetWindowHandle(Window window)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;

                if (hwnd == IntPtr.Zero)
                {
                    hwnd = new WindowInteropHelper(window).EnsureHandle();
                }

                return hwnd;
            }
            catch (Exception)
            {
                return IntPtr.Zero;
            }
        }
    }
}
