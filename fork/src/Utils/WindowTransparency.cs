using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TarkovClient.Utils
{
    /// <summary>
    /// Windows API를 사용한 창 투명도 제어 유틸리티
    /// WebView2와 호환되는 LayeredWindow 방식 구현
    /// </summary>
    public static class WindowTransparency
    {
        // Windows API 상수
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int LWA_ALPHA = 0x2;

        // Windows API 함수들
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(
            IntPtr hwnd,
            uint colorKey,
            byte alpha,
            uint flags
        );

        /// <summary>
        /// 창을 투명 모드로 활성화
        /// </summary>
        /// <param name="window">대상 WPF 창</param>
        /// <param name="opacity">투명도 (0.0 = 완전투명, 1.0 = 불투명)</param>
        /// <returns>성공 여부</returns>
        public static bool EnableTransparency(Window window, double opacity = 1.0)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;

                if (hwnd == IntPtr.Zero)
                {
                    hwnd = new WindowInteropHelper(window).EnsureHandle();
                }

                if (hwnd == IntPtr.Zero)
                {
                    return false;
                }

                // 현재 창 스타일 가져오기
                var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

                // Layered Window 스타일 추가
                var newStyle = extendedStyle | WS_EX_LAYERED;
                var setResult = SetWindowLong(hwnd, GWL_EXSTYLE, newStyle);

                // 투명도 설정 (0-255 범위로 변환)
                byte alpha = (byte)(opacity * 255);
                var transparencyResult = SetLayeredWindowAttributes(hwnd, 0, alpha, LWA_ALPHA);

                // 설정 후 스타일 재확인
                var finalStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                var isLayered = (finalStyle & WS_EX_LAYERED) != 0;

                return transparencyResult;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 창의 투명도만 업데이트 (이미 LayeredWindow로 설정된 창용)
        /// </summary>
        /// <param name="window">대상 WPF 창</param>
        /// <param name="opacity">투명도 (0.0 = 완전투명, 1.0 = 불투명)</param>
        /// <returns>성공 여부</returns>
        public static bool UpdateTransparency(Window window, double opacity)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero)
                    return false;

                // 투명도만 업데이트 (0-255 범위로 변환)
                byte alpha = (byte)(Math.Max(0.0, Math.Min(1.0, opacity)) * 255);
                return SetLayeredWindowAttributes(hwnd, 0, alpha, LWA_ALPHA);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 창의 투명 모드를 비활성화
        /// </summary>
        /// <param name="window">대상 WPF 창</param>
        /// <returns>성공 여부</returns>
        public static bool DisableTransparency(Window window)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero)
                    return false;

                // 현재 창 스타일 가져오기
                var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

                // Layered Window 스타일 제거
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_LAYERED);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 창이 LayeredWindow 모드인지 확인
        /// </summary>
        /// <param name="window">대상 WPF 창</param>
        /// <returns>LayeredWindow 여부</returns>
        public static bool IsLayeredWindow(Window window)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero)
                    return false;

                var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                return (extendedStyle & WS_EX_LAYERED) != 0;
            }
            catch
            {
                return false;
            }
        }

    }
}
