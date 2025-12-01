using System.Runtime.InteropServices;
using Velopack;

namespace TanukiTarkovMap;

/**
Program - Application entry point with Velopack auto-update and single instance management

Purpose: Ensures single instance execution, initializes Velopack for automatic updates,
         and starts the WPF application
Architecture: Entry point that handles mutex check, update checking, and application startup

Core Functionality:
- Single instance check via Mutex (before CEF initialization)
- Brings existing window to front if already running
- Initializes Velopack framework on startup
- Checks for updates from GitHub Releases

Method Flow:
  Main() → [Mutex Check] → VelopackApp.Build().Run() → App.Run() → CheckForUpdates()

Dependencies:
- Velopack: Auto-update framework
- GithubSource: Update source from GitHub Releases
- Win32 API: For finding and focusing existing window

Design Rationale: Mutex check must happen BEFORE CEF initialization to prevent
zombie Chrome processes when duplicate instance is detected.
*/
public static class Program
{
    private const string GitHubRepoUrl = "https://github.com/Sia819/TanukiTarkovMap";
    private const string MutexName = "TanukiTarkovMapMutex";
    private const string MainWindowTitle = "TanukiTarkovMap";

    // Win32 API imports for window management
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    [STAThread]
    public static void Main(string[] args)
    {
        // 1. 중복 실행 체크 (CEF 초기화 전에 반드시 먼저!)
        using var mutex = new Mutex(true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            // 기존 창 포커스 시도
            BringExistingInstanceToFront();
            return;
        }

        // 2. Velopack 초기화 (설치/제거/업데이트 훅 처리)
        VelopackApp.Build().Run();

        // 3. WPF 앱 시작
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    private static void BringExistingInstanceToFront()
    {
        var hwnd = FindWindow(null, MainWindowTitle);

        if (hwnd != IntPtr.Zero)
        {
            // 최소화되어 있으면 복원
            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, SW_RESTORE);
            }
            else
            {
                ShowWindow(hwnd, SW_SHOW);
            }

            // 창을 전면으로 가져오기
            SetForegroundWindow(hwnd);
        }
        // 창을 찾지 못한 경우 (트레이에만 있을 수 있음) - 조용히 종료
    }
}
