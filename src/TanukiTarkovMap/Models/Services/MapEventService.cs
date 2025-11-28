using System;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Models.Services
{
    /// <summary>
    /// 맵 변경 및 스크린샷 이벤트를 처리하는 서비스
    /// FileSystem 모니터링과 ViewModel을 연결
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
        public event EventHandler? ScreenshotTaken;

        /// <summary>
        /// 맵 변경 이벤트 발생
        /// </summary>
        public void OnMapChanged(string mapName)
        {
            Logger.SimpleLog($"[MapEventService] OnMapChanged called: {mapName}");

            var subscriberCount = MapChanged?.GetInvocationList().Length ?? 0;
            Logger.SimpleLog($"[MapEventService] MapChanged event has {subscriberCount} subscriber(s)");

            MapChanged?.Invoke(this, new MapChangedEventArgs(mapName));

            Logger.SimpleLog($"[MapEventService] MapChanged event invoked for map: {mapName}");
        }

        /// <summary>
        /// 스크린샷 생성 이벤트 발생
        /// </summary>
        public void OnScreenshotTaken()
        {
            Logger.SimpleLog("[MapEventService] OnScreenshotTaken called");

            var subscriberCount = ScreenshotTaken?.GetInvocationList().Length ?? 0;
            Logger.SimpleLog($"[MapEventService] ScreenshotTaken event has {subscriberCount} subscriber(s)");

            ScreenshotTaken?.Invoke(this, EventArgs.Empty);

            Logger.SimpleLog("[MapEventService] ScreenshotTaken event invoked");
        }
    }

    /// <summary>
    /// 맵 변경 이벤트 인자
    /// </summary>
    public class MapChangedEventArgs : EventArgs
    {
        public string MapName { get; }

        public MapChangedEventArgs(string mapName)
        {
            MapName = mapName;
        }
    }
}
