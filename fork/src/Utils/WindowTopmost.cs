using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TarkovClient.Utils
{
    /// <summary>
    /// Windows API를 사용한 강력한 최상단 유지 유틸리티
    /// 시스템 레벨에서 창을 최상단에 고정하는 기능 제공
    /// </summary>
    public static class WindowTopmost
    {
        // Windows API 상수
        private const int HWND_TOPMOST = -1;
        private const int HWND_NOTOPMOST = -2;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOACTIVATE = 0x0010;

        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;
        private const int SW_SHOWNOACTIVATE = 4;

        // Windows API 함수들
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            int hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags
        );

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

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

                ShowWindow(hwnd, SW_SHOWNOACTIVATE);

                uint flags = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE;
                bool result = SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, flags);

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

                bool result = SetWindowPos(
                    hwnd,
                    HWND_NOTOPMOST,
                    0,
                    0,
                    0,
                    0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE
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
