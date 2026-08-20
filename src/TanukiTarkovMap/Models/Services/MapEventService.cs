using System;
using TanukiTarkovMap.Models.Data;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Models.Services
{
    /// <summary>
    /// 맵 변경, 스크린샷, 퀘스트 완료 이벤트를 처리하는 서비스
    /// FileSystem 모니터링과 ViewModel을 연결
    ///
    /// 스크린샷과 퀘스트 이벤트는 WebBrowserViewModel이 받아
    /// tarkov-market.com의 window.pilot으로 넘긴다 (Models/JavaScript/PilotBridge.js.cs)
    ///
    /// 사용법: ServiceLocator.MapEventService (DI 싱글톤)
    /// </summary>
    public class MapEventService
    {
        /// <summary>
        /// DI 컨테이너 전용 생성자 - 외부에서 new 사용 금지
        /// ServiceLocator.CreateInstance()를 통해서만 생성
        /// </summary>
        internal MapEventService()
        {
            Logger.SimpleLog("[MapEventService] Instance created");
        }

        /// <summary>
        /// 맵이 변경되었을 때 발생하는 이벤트
        /// </summary>
        public event EventHandler<MapChangedEventArgs>? MapChanged;

        /// <summary>
        /// 스크린샷이 생성되었을 때 발생하는 이벤트
        /// </summary>
        public event EventHandler<ScreenshotTakenEventArgs>? ScreenshotTaken;

        /// <summary>
        /// 퀘스트를 완료했을 때 발생하는 이벤트
        /// </summary>
        public event EventHandler<QuestCompletedEventArgs>? QuestCompleted;

        /// <summary>
        /// 맵 변경 이벤트 발생
        /// </summary>
        /// <param name="map">전환 대상 맵</param>
        /// <param name="source">이 요청이 어디서 나왔는지. 수신 측이 설정으로 각각 끌 수 있게 함께 넘긴다</param>
        public void OnMapChanged(MapInfo map, MapChangeSource source)
        {
            Logger.SimpleLog($"[MapEventService] OnMapChanged called: {map.DisplayName} (source: {source})");

            MapChanged?.Invoke(this, new MapChangedEventArgs(map, source));
        }

        /// <summary>
        /// 스크린샷 생성 이벤트 발생
        /// </summary>
        /// <param name="filename">스크린샷 파일명. 좌표와 시선 방향이 이름에 들어 있다</param>
        public void OnScreenshotTaken(string filename)
        {
            Logger.SimpleLog($"[MapEventService] OnScreenshotTaken called: {filename}");

            ScreenshotTaken?.Invoke(this, new ScreenshotTakenEventArgs(filename));
        }

        /// <summary>
        /// 퀘스트 완료 이벤트 발생
        /// </summary>
        /// <param name="questId">타르코프 퀘스트 ID (로그의 templateId 앞부분)</param>
        public void OnQuestCompleted(string questId)
        {
            Logger.SimpleLog($"[MapEventService] OnQuestCompleted called: {questId}");

            QuestCompleted?.Invoke(this, new QuestCompletedEventArgs(questId));
        }
    }

    /// <summary>
    /// 맵 변경 요청이 나온 곳.
    /// 두 경로는 신뢰도가 달라 사용자가 따로 끌 수 있어야 한다.
    /// RaidEntry는 게임 로그에서 방금 읽은 진입이라 확실하지만,
    /// Screenshot은 마지막으로 읽어 둔 맵을 그대로 다시 쓰는 보정이라 로그 추적이
    /// 끊기면 지난 맵을 계속 밀어 넣는다
    /// </summary>
    public enum MapChangeSource
    {
        /// <summary>게임 로그의 scene preset 줄에서 레이드 진입을 감지</summary>
        RaidEntry,

        /// <summary>스크린샷 생성 시 마지막으로 감지한 맵으로 맞추는 보정</summary>
        Screenshot
    }

    /// <summary>
    /// 맵 변경 이벤트 인자
    /// </summary>
    public class MapChangedEventArgs : EventArgs
    {
        public MapInfo Map { get; }

        public MapChangeSource Source { get; }

        public MapChangedEventArgs(MapInfo map, MapChangeSource source)
        {
            Map = map;
            Source = source;
        }
    }

    /// <summary>
    /// 스크린샷 생성 이벤트 인자
    /// </summary>
    public class ScreenshotTakenEventArgs : EventArgs
    {
        public string Filename { get; }

        public ScreenshotTakenEventArgs(string filename)
        {
            Filename = filename;
        }
    }

    /// <summary>
    /// 퀘스트 완료 이벤트 인자
    /// </summary>
    public class QuestCompletedEventArgs : EventArgs
    {
        public string QuestId { get; }

        public QuestCompletedEventArgs(string questId)
        {
            QuestId = questId;
        }
    }
}
