using System.Runtime.InteropServices;
using TanukiTarkovMap.Models.Utils;
using Velopack;

namespace TanukiTarkovMap;

/**
Program - Application entry point with Velopack auto-update and single instance management

Purpose: Ensures single instance execution, registers uninstall cleanup for preserved browser data,
         initializes Velopack for automatic updates, and starts the WPF application
Architecture: Entry point that handles mutex checks, Velopack lifecycle hooks, update checking,
              and application startup

Core Functionality:
- Initializes Velopack and removes preserved browser data before uninstall
- Single instance check via Mutex (before CEF initialization)
- Brings existing window to front if already running
- Checks for updates from GitHub Releases

Method Flow:
  Main() -> Build() -> Register Uninstall Hook -> Run()
    -> [Uninstall] DeleteRoamingBrowserDataOnUninstall() -> Exit
    -> [Normal Start] Mutex Check
      -> [Duplicate] BringExistingInstanceToFront() -> Exit
      -> [First Instance] App.Run() -> CheckForUpdates()

Dependencies:
- Velopack: Auto-update framework
- AppPaths: Removes browser data preserved outside the install folder before uninstall
- GithubSource: Update source from GitHub Releases
- Win32 API: For finding and focusing existing window

Design Rationale: Velopack lifecycle dispatch must precede the mutex because hook processes can run
while the app owns that mutex. Hook paths exit inside Run(), while normal startup continues to the
mutex before App initializes CEF, preventing zombie Chrome processes from duplicate instances.
*/
public static class Program
{
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
        // 1. 제거 훅 프로세스가 앱 뮤텍스에 막히지 않도록 Velopack 수명주기 분기를 가장 먼저 처리한다.
        // 훅 경로는 Run() 안에서 종료되고 정상 시작만 아래 단일 인스턴스 검사로 이어진다
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => AppPaths.DeleteRoamingBrowserDataOnUninstall())
            .Run();

        // 2. 정상 시작의 중복 실행 체크 (App에서 CEF를 초기화하기 전에 반드시 처리)
        using var mutex = new Mutex(true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            // 기존 창 포커스 시도
            BringExistingInstanceToFront();
            return;
        }

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
