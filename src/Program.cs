using System.Runtime.InteropServices;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.Core.Utils;
using Velopack;

namespace TanukiTarkovMap;

public static class Program
{
    private const string MutexName = "TanukiTarkovMapMutex";
    private const string MainWindowTitle = "TanukiTarkovMap";

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
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .OnBeforeUninstallFastCallback(_ => AppPaths.DeleteRoamingBrowserDataOnUninstall())
            .Run();

        using var mutex = new Mutex(true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            BringExistingInstanceToFront();
            return;
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    private static void BringExistingInstanceToFront()
    {
        var hwnd = FindWindow(null, MainWindowTitle);

        if (hwnd != IntPtr.Zero)
        {
            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, SW_RESTORE);
            }
            else
            {
                ShowWindow(hwnd, SW_SHOW);
            }

            SetForegroundWindow(hwnd);
        }
    }
}
