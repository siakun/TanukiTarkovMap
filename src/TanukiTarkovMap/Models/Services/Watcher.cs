using TanukiTarkovMap.Models.FileSystem;

/**
Watcher - 파일 시스템 감시자들의 통합 관리 Facade

Purpose: ScreenshotsWatcher와 LogsWatcher를 한 번에 제어하는 단순화된 인터페이스 제공

Core Functionality:
- Start(): 모든 감시자 시작 (앱 초기화 시 App.xaml.cs에서 호출)
- Stop(): 모든 감시자 종료 (앱 종료 시)
- Restart(): 모든 감시자 재시작

개별 감시자 직접 사용:
- 스크린샷 경로만 변경 → ScreenshotsWatcher.Restart()
- 게임 폴더만 변경 → LogsWatcher.Restart()
- 둘 다 재시작 필요 → Watcher.Restart()
*/
namespace TanukiTarkovMap.Models.Services
{
    public static class Watcher
    {
        public static void Start()
        {
            ScreenshotsWatcher.Start();
            LogsWatcher.Start();
        }

        public static void Stop()
        {
            ScreenshotsWatcher.Stop();
            LogsWatcher.Stop();
        }

        public static void Restart()
        {
            ScreenshotsWatcher.Restart();
            LogsWatcher.Restart();
        }
    }
}
