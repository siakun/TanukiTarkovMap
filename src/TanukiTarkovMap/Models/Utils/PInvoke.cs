using System.Runtime.InteropServices;

namespace TanukiTarkovMap.Models.Utils
{
    /// <summary>
    /// Windows API (P/Invoke) 선언을 모아둔 중앙 집중식 네이티브 메서드 클래스
    /// 모든 DllImport는 이 클래스를 통해 접근하여 중복을 방지하고 유지보수성을 향상시킴
    /// </summary>
    internal static class PInvoke
    {
        #region User32.dll - 윈도우 관리

        /// <summary>
        /// 마우스 캡처를 해제합니다. (윈도우 드래그용)
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern bool ReleaseCapture();

        /// <summary>
        /// 지정된 윈도우에 메시지를 전송합니다. (윈도우 드래그용)
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        /// <summary>
        /// 윈도우의 위치와 크기를 변경합니다.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool SetWindowPos(
            IntPtr hWnd,
            int hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags
        );

        /// <summary>
        /// 윈도우의 표시 상태를 설정합니다.
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        /// <summary>
        /// 현재 포그라운드 윈도우의 핸들을 가져옵니다.
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        /// <summary>
        /// 지정된 윈도우를 최상단으로 가져옵니다.
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern bool BringWindowToTop(IntPtr hWnd);

        /// <summary>
        /// 지정된 핸들이 유효한 윈도우인지 확인합니다.
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern bool IsWindow(IntPtr hWnd);

        /// <summary>
        /// 지정된 윈도우가 화면에 표시되는지 확인합니다.
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        /// <summary>
        /// 윈도우의 확장 스타일을 가져오거나 설정합니다.
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern int GetWindowLong(IntPtr hwnd, int index);

        /// <summary>
        /// 윈도우의 확장 스타일을 설정합니다.
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        /// <summary>
        /// Layered Window의 투명도 및 색상 키를 설정합니다.
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern bool SetLayeredWindowAttributes(
            IntPtr hwnd,
            uint colorKey,
            byte alpha,
            uint flags
        );

        /// <summary>
        /// 특정 가상 키의 상태를 가져옵니다.
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern short GetKeyState(int nVirtKey);

        /// <summary>
        /// 윈도우의 경계 사각형을 화면 좌표로 가져옵니다.
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        #endregion

        #region User32.dll - 키보드 Hook

        /// <summary>
        /// Low-Level 키보드 Hook을 설치합니다.
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(
            int idHook,
            LowLevelKeyboardProc lpfn,
            IntPtr hMod,
            uint dwThreadId
        );

        /// <summary>
        /// Hook을 제거합니다.
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

        /// <summary>
        /// 다음 Hook 프로시저를 호출합니다.
        /// </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr CallNextHookEx(
            IntPtr hhk,
            int nCode,
            IntPtr wParam,
            IntPtr lParam
        );

        /// <summary>
        /// Low-Level 키보드 Hook 델리게이트
        /// </summary>
        internal delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        #endregion

        #region Kernel32.dll

        /// <summary>
        /// 지정된 모듈의 핸들을 가져옵니다.
        /// </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr GetModuleHandle(string lpModuleName);

        #endregion

        #region 상수 정의

        // 윈도우 메시지
        internal const int WM_NCLBUTTONDOWN = 0xA1;
        internal const int HT_CAPTION = 0x2;

        // Hook 상수
        internal const int WH_KEYBOARD_LL = 13;
        internal const int WM_KEYDOWN = 0x0100;
        internal const int WM_SYSKEYDOWN = 0x0104;

        // 가상 키 코드 (Modifier Keys)
        internal const int VK_CONTROL = 0x11;
        internal const int VK_MENU = 0x12; // Alt key
        internal const int VK_SHIFT = 0x10;
        internal const int VK_LWIN = 0x5B;
        internal const int VK_RWIN = 0x5C;

        // WindowPos 플래그
        internal const int HWND_TOPMOST = -1;
        internal const int HWND_NOTOPMOST = -2;
        internal const uint SWP_NOMOVE = 0x0002;
        internal const uint SWP_NOSIZE = 0x0001;
        internal const uint SWP_SHOWWINDOW = 0x0040;
        internal const uint SWP_NOACTIVATE = 0x0010;

        // ShowWindow 명령
        internal const int SW_SHOW = 5;
        internal const int SW_RESTORE = 9;
        internal const int SW_SHOWNOACTIVATE = 4;

        // 윈도우 스타일
        internal const int GWL_EXSTYLE = -20;
        internal const int WS_EX_LAYERED = 0x80000;
        internal const int LWA_ALPHA = 0x2;

        #endregion
    }

    /// <summary>
    /// 윈도우 사각형 구조체 (화면 좌표)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }
}
