using System;

namespace TanukiTarkovMap.Models.Services
{
    /// <summary>
    /// 맵 변경 및 스크린샷 이벤트를 처리하는 서비스 인터페이스
    /// FileSystem 모니터링과 ViewModel을 연결
    /// </summary>
    public interface IMapEventService
    {
        /// <summary>
        /// 맵이 변경되었을 때 발생하는 이벤트
        /// </summary>
        event EventHandler<MapChangedEventArgs> MapChanged;

        /// <summary>
        /// 스크린샷이 생성되었을 때 발생하는 이벤트
        /// </summary>
        event EventHandler ScreenshotTaken;

        /// <summary>
        /// 맵 변경 이벤트 발생
        /// </summary>
        void OnMapChanged(string mapName);

        /// <summary>
        /// 스크린샷 생성 이벤트 발생
        /// </summary>
        void OnScreenshotTaken();
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
