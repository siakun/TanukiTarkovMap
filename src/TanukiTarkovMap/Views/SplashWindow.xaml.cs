using System.Windows;
using System.Windows.Media.Animation;

namespace TanukiTarkovMap.Views;

/**
SplashWindow - 앱 시작 시 표시되는 스플래시/업데이터 창

Purpose: 앱 로딩 및 업데이트 진행상황을 사용자에게 시각적으로 표시
Architecture: WindowChrome 없는 투명 창, 로고 펄스 애니메이션

Core Functionality:
- 로고 펄스 애니메이션 자동 시작
- 상태 텍스트 업데이트 (시작하는 중, 업데이트 확인 중, 다운로드 중 등)
- 진행바 업데이트 (0~100%)

Design Rationale: 단순한 스플래시 창이므로 별도 ViewModel 없이 직접 메서드 호출
*/
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 로고 펄스 애니메이션 시작
        var storyboard = (Storyboard)FindResource("LogoPulse");
        storyboard.Begin();
    }

    /// <summary>
    /// 상태 텍스트 업데이트
    /// </summary>
    public void SetStatus(string status)
    {
        Dispatcher.Invoke(() => StatusText.Text = status);
    }

    /// <summary>
    /// 진행률 업데이트 (0~100)
    /// </summary>
    public void SetProgress(int percent)
    {
        Dispatcher.Invoke(() =>
        {
            var parent = ProgressFill.Parent as FrameworkElement;
            var targetWidth = parent?.ActualWidth > 0 ? parent.ActualWidth : 340;
            ProgressFill.Width = targetWidth * (percent / 100.0);
        });
    }

    /// <summary>
    /// 진행바 숨기기 (업데이트 없을 때)
    /// </summary>
    public void HideProgress()
    {
        Dispatcher.Invoke(() => ProgressFill.Visibility = Visibility.Collapsed);
    }
}
