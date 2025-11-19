using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace TarkovClient;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private NotifyIcon trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        SetCulture();

        bool createdNew;
        using (Mutex mutex = new Mutex(true, "TarkovPilotMutex", out createdNew))
        {
            if (createdNew)
            {
                // First instance
                StartApp(e.Args);
                base.OnStartup(e);
            }
            else
            {
                // Exit if already running
                Shutdown();
            }
        }
    }

    private static void SetCulture()
    {
        // make floats with dots
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
    }

    private void StartApp(string[] args)
    {
        CreateTrayIcon();
        Settings.Load();  // 설정을 먼저 로드

        // 구 로그 폴더 정리 (최신 폴더 제외)
        GameSessionCleaner.CleanOldLogFolders();

        // 이전 세션의 스크린샷 파일 정리
        GameSessionCleaner.CleanScreenshotFiles();

        Server.Start();
        Watcher.Start();

        // WPF 메인 윈도우는 자동으로 생성됨 (App.xaml의 StartupUri)
    }

    private void CreateTrayIcon()
    {
        trayIcon = new NotifyIcon
        {
            Icon = TarkovClient.Properties.Resources.korea,
            Visible = true,
            Text = "Tarkov Client",
        };

        ContextMenuStrip contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(
            "Open",
            null,
            (s, e) =>
            {
                Process.Start(
                    new ProcessStartInfo { FileName = Env.WebsiteUrl, UseShellExecute = true }
                );
            }
        );
        contextMenu.Items.Add(
            "Exit",
            null,
            (s, e) =>
            {
                trayIcon?.Dispose();
                Shutdown();
            }
        );

        trayIcon.ContextMenuStrip = contextMenu;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        trayIcon?.Dispose();
        base.OnExit(e);
    }
}
