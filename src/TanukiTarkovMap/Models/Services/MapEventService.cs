using System;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Models.Services
{
    /// <summary>
    /// 맵 변경 및 스크린샷 이벤트를 처리하는 서비스 구현
    /// 싱글톤 패턴으로 FileSystem 모니터링과 ViewModel을 연결
    /// </summary>
    public class MapEventService : IMapEventService
    {
        private static MapEventService _instance;
        public static MapEventService Instance => _instance ??= new MapEventService();

        /// <inheritdoc/>
        public event EventHandler<MapChangedEventArgs> MapChanged;

        /// <inheritdoc/>
        public event EventHandler ScreenshotTaken;

        private MapEventService()
        {
            Logger.SimpleLog("[MapEventService] Instance created");
        }

        /// <inheritdoc/>
        public void OnMapChanged(string mapName)
        {
            Logger.SimpleLog($"[MapEventService] OnMapChanged called: {mapName}");

            var subscriberCount = MapChanged?.GetInvocationList().Length ?? 0;
            Logger.SimpleLog($"[MapEventService] MapChanged event has {subscriberCount} subscriber(s)");

            MapChanged?.Invoke(this, new MapChangedEventArgs(mapName));

            Logger.SimpleLog($"[MapEventService] MapChanged event invoked for map: {mapName}");
        }

        /// <inheritdoc/>
        public void OnScreenshotTaken()
        {
            Logger.SimpleLog("[MapEventService] OnScreenshotTaken called");

            var subscriberCount = ScreenshotTaken?.GetInvocationList().Length ?? 0;
            Logger.SimpleLog($"[MapEventService] ScreenshotTaken event has {subscriberCount} subscriber(s)");

            ScreenshotTaken?.Invoke(this, EventArgs.Empty);

            Logger.SimpleLog("[MapEventService] ScreenshotTaken event invoked");
        }
    }
}
