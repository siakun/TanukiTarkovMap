using System.Windows;
using NuGet.Versioning;
using Velopack;
using Velopack.Sources;

namespace TanukiTarkovMap;

/**
Program - Application entry point with Velopack auto-update integration

Purpose: Initializes Velopack for automatic updates before starting the WPF application
Architecture: Entry point that handles update checking and application startup

Core Functionality:
- Initializes Velopack framework on startup
- Checks for updates from GitHub Releases
- Starts main WPF application after update check

Method Flow:
  Main() → VelopackApp.Build().Run() → CheckForUpdates() → App.Run()

Dependencies:
- Velopack: Auto-update framework
- GithubSource: Update source from GitHub Releases

Design Rationale: Velopack requires initialization before any WPF code runs.
Custom Main method allows update checking before the main application starts.
*/
public static class Program
{
    private const string GitHubRepoUrl = "https://github.com/Sia819/TanukiTarkovMap";

    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack 초기화 (반드시 가장 먼저 호출)
        // 설치/제거/업데이트 시 필요한 훅 처리
        VelopackApp.Build()
            .OnFirstRun((v) => OnFirstRun(v))
            .Run();

        // WPF 앱 시작
        var app = new App();
        app.InitializeComponent();

        // 업데이트 체크 (백그라운드)
        _ = CheckForUpdatesAsync();

        app.Run();
    }

    private static void OnFirstRun(SemanticVersion version)
    {
        // 첫 설치 시 실행될 코드
        MessageBox.Show(
            $"TanukiTarkovMap v{version} 설치를 완료했습니다!",
            "설치 완료",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static async Task CheckForUpdatesAsync()
    {
        try
        {
            var updateManager = new UpdateManager(new GithubSource(GitHubRepoUrl, null, false));

            // Velopack으로 설치되지 않은 경우 (개발 모드) 스킵
            if (!updateManager.IsInstalled)
                return;

            // 업데이트 확인
            var updateInfo = await updateManager.CheckForUpdatesAsync();
            if (updateInfo == null)
                return;

            // 업데이트 발견 - 사용자에게 알림
            var result = MessageBox.Show(
                $"새 버전 {updateInfo.TargetFullRelease.Version}이 있습니다.\n\n지금 업데이트하시겠습니까?",
                "업데이트 가능",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            // 업데이트 다운로드
            await updateManager.DownloadUpdatesAsync(updateInfo);

            // 업데이트 적용 및 재시작
            var restartResult = MessageBox.Show(
                "업데이트 다운로드가 완료되었습니다.\n\n지금 재시작하시겠습니까?",
                "업데이트 완료",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (restartResult == MessageBoxResult.Yes)
            {
                updateManager.ApplyUpdatesAndRestart(updateInfo);
            }
        }
        catch (Exception ex)
        {
            // 업데이트 실패해도 앱은 정상 실행
            System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
        }
    }
}
