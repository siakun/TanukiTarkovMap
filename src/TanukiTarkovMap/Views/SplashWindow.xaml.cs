using System.Windows;

namespace TanukiTarkovMap.Views;

/**
SplashWindow - 시작이 오래 걸릴 때만 나타나는 대기 표시

Purpose: 시작이 눈에 띄게 오래 걸리는 실행에서 앱이 살아 있음을 알린다.
설치 직후 첫 실행, 느린 디스크, 백신 검사가 그런 경우다.

Architecture: WindowChrome 없는 투명 창. 상태 문구 하나만 보여준다.

Core Functionality:
- SetStatus(status): 지금 무엇을 하는 중인지 한 줄로 알린다

Design Rationale: 단순한 창이라 별도 ViewModel 없이 직접 메서드로 다룬다.

Historical Context: 처음에는 이 창이 업데이트를 관리하기로 했다. Discord처럼 확인과
다운로드를 끝낸 뒤 메인 창을 여는 구조였고 진행바도 그래서 있었다. 그 설계를 버린 이유는
docs/startup-speed-and-updates.md에 적어 두었다. 요지는 이 앱이 목적지가 아니라 레이드
중에 위치를 확인하는 보조 도구라, 시작 앞에 30초짜리 관문을 두면 앱이 존재하는 이유를
스스로 막는다는 것이다. 업데이트는 메인 창이 열린 뒤 백그라운드로 받는다.
진행바(SetProgress, HideProgress)는 그 설계와 함께 지웠다.

Critical Warnings: 아이콘을 보여주려고 최소 표시 시간을 늘리지 않는다.
그것은 미관을 위해 사용자를 기다리게 하는 일이고, 위 문서가 정한 우선순위와 어긋난다.
표시 시점은 App이 정하며 이 창은 언제 뜰지 스스로 정하지 않는다.

Last Updated: 2026-08-16 | .NET 8 | 업데이트 관리 역할 제거
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
}
