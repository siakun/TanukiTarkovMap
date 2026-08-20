using System.IO;

/**
LogFileWatcher - 폴더 안에서 가장 최신인 로그 파일 하나를 따라가며 증가분을 알린다

Purpose: 타르코프가 쓰는 로그 파일은 세션 폴더 안에서 이름이 바뀔 수 있고(롤오버),
게임이 켜지기 전에는 아예 없다. 감시자가 파일 하나에 붙박이면 그 뒤로 들어오는
내용을 영영 못 읽는데, 로그 추적이 끊긴 사실은 밖에서 알 방법이 없어 앱은 마지막으로
읽은 맵을 계속 옳다고 믿는다. 그래서 파일을 고정하지 않고 매번 다시 고른다.

Architecture: 5초 주기 폴링 한 줄기로만 돈다. 폴링이 파일 선택과 증가 감지를 함께
맡으므로 FileSystemWatcher를 따로 두지 않는다.

Core Functionality:
- Start(): 폴링 시작. 폴더가 아직 비어 있어도 그대로 돈다
- Stop(): 다음 주기에 폴링 종료
- Created: 따라가는 파일이 새로 정해졌을 때(첫 발견, 롤오버) 발생
- Changed: 따라가던 파일이 커졌을 때 발생

State Management:
- currentFilePath: 지금 따라가는 파일. null이면 아직 후보가 없다
- lastFileSize: 마지막으로 알린 크기. 파일이 바뀌면 0으로 되돌린다
- isStopping: 폴링 종료 요청. volatile로 두어 폴링 스레드가 바로 본다
- isStarted: 폴링을 이미 띄웠는지. 재시작은 새 인스턴스로 하므로 두 번째 Start()는 무시한다

Method Flow:
  Start -> Task.Run(PollLoop)
  PollLoop -> TryGetFilePath -> 경로가 바뀜? -> lastFileSize=0, Created 발생
                             -> 크기 증가? -> lastFileSize 갱신, Changed 발생
                             -> 예외 -> 이번 주기만 건너뜀 (루프 유지)

Key Methods:
- TryGetFilePath(): 이름 역순 첫 파일을 고른다. 롤오버 접미사(application_001.log)가
  커지므로 이름이 큰 쪽이 최신이다
- PollLoop(): 종료 요청 전까지 checkInterval 간격으로 위 판정을 반복

Dependencies:
- LogsWatcher: 이 감시자의 Created/Changed를 받아 로그 본문을 해석한다

Design Rationale: 파일이 생기기를 FileSystemWatcher로 기다리고 그 뒤로는 폴링하는
2단 구조를 폴링 한 줄기로 합쳤다. 파일이 바뀌는 사건(생성, 롤오버, 교체)을 모두
같은 한 판정으로 처리해, 어느 한쪽 경로만 동작하고 다른 쪽은 조용히 죽는 상태를 없앤다.

Known Limitations: 파일 생성과 롤오버를 최대 checkInterval(기본 5초)만큼 늦게 알아챈다.
로그 감지는 레이드 진입 직후 몇 초의 지연을 허용하는 용도라 그대로 둔다.

Critical Warnings: 폴링 루프 안에서 예외가 났다고 루프를 끝내지 말 것. 파일이 잠기거나
잠깐 사라지는 일은 정상이며, 한 번의 예외로 루프를 끝내면 그 뒤로 로그 추적이 끊긴
채 앱이 계속 돌아간다.

Last Updated: 2026-08-21 | .NET 8 | 로그 추적이 조용히 끊기던 문제 수정
*/
namespace TanukiTarkovMap.Models.Utils
{
    public class LogFileWatcher
    {
        readonly string folder;
        readonly string searchPattern;
        readonly int checkInterval;

        volatile bool isStopping = false;
        bool isStarted = false;
        string? currentFilePath;
        long lastFileSize = 0;

        public event EventHandler<FileChangedEventArgs> Created;
        public event EventHandler<FileChangedEventArgs> Changed;

        public LogFileWatcher(string folder, string searchPattern, int checkInterval = 5000)
        {
            this.folder = folder;
            this.searchPattern = searchPattern;
            this.checkInterval = checkInterval;
        }

        string? TryGetFilePath()
        {
            if (!Directory.Exists(folder))
            {
                return null;
            }

            // 로그가 롤오버되면 application_001.log 처럼 접미사가 커지므로 이름 역순 첫 파일이 최신이다
            return Directory.GetFiles(folder, searchPattern)
                .OrderByDescending(filePath => filePath, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        /// <summary>
        /// 폴링을 시작한다. Stop() 뒤에 다시 부르는 용도는 없으므로 두 번째 호출은 무시한다.
        /// 되살리면 먼저 돌던 루프와 새 루프가 겹쳐 같은 증가분을 두 번 알린다
        /// </summary>
        public void Start()
        {
            if (isStarted)
            {
                return;
            }

            isStarted = true;
            Task.Run(PollLoop);
        }

        void PollLoop()
        {
            while (!isStopping)
            {
                try
                {
                    CheckOnce();
                }
                catch (Exception)
                {
                    // 파일이 잠기거나 폴더가 잠시 사라지는 일은 정상이다.
                    // 이번 주기만 건너뛰고 다음 주기에 다시 본다
                }

                Thread.Sleep(checkInterval);
            }
        }

        void CheckOnce()
        {
            var filePath = TryGetFilePath();
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            // 따라갈 파일이 바뀌었다: 첫 발견이거나 로그가 롤오버됐다.
            // 새 파일은 처음부터 다시 세야 하므로 기준 크기를 되돌린다
            if (!string.Equals(filePath, currentFilePath, StringComparison.OrdinalIgnoreCase))
            {
                currentFilePath = filePath;
                lastFileSize = 0;
                Created?.Invoke(this, new FileChangedEventArgs(filePath));
                return;
            }

            long currentFileSize = new FileInfo(filePath).Length;
            if (currentFileSize > lastFileSize)
            {
                lastFileSize = currentFileSize;
                Changed?.Invoke(this, new FileChangedEventArgs(filePath));
            }
        }

        public void Stop()
        {
            isStopping = true;
        }
    }
}
