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
        public void OnMapChanged(MapInfo map)
        {
            Logger.SimpleLog($"[MapEventService] OnMapChanged called: {map.DisplayName}");

            MapChanged?.Invoke(this, new MapChangedEventArgs(map));
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
    /// 맵 변경 이벤트 인자
    /// </summary>
    public class MapChangedEventArgs : EventArgs
    {
        public MapInfo Map { get; }

        public MapChangedEventArgs(MapInfo map)
        {
            Map = map;
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
