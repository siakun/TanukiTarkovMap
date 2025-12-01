using System.Windows;

namespace TanukiTarkovMap.Views;

/**
SplashWindow - 앱 시작 시 표시되는 스플래시/업데이터 창

Purpose: 앱 로딩 및 업데이트 진행상황을 사용자에게 시각적으로 표시
Architecture: WindowChrome 없는 투명 창

Core Functionality:
- 상태 텍스트 업데이트 (시작하는 중, 업데이트 확인 중, 다운로드 중 등)
- 진행바 업데이트 (0~100%)

Design Rationale: 단순한 스플래시 창이므로 별도 ViewModel 없이 직접 메서드 호출
*/
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 상태 텍스트 업데이트
    /// </summary>
    public void SetStatus(string status)
    {
        Dispatcher.Invoke(() => StatusText.Text = status);
    }

    /// <summary>
    /// 진행률 업데이트 (0~100) - 호출 시 자동으로 프로그레스 바 표시
    /// </summary>
    public void SetProgress(int percent)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressBarContainer.Visibility = Visibility.Visible;
            var targetWidth = ProgressBarContainer.ActualWidth > 0 ? ProgressBarContainer.ActualWidth : 200;
            ProgressFill.Width = targetWidth * (percent / 100.0);
        });
    }

    /// <summary>
    /// 진행바 숨기기 (업데이트 없을 때)
    /// </summary>
    public void HideProgress()
    {
        Dispatcher.Invoke(() => ProgressBarContainer.Visibility = Visibility.Collapsed);
    }
}
