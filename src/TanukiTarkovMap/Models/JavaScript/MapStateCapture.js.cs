namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// 사이트가 지도 상태 객체와 층 데이터 표를 만드는 순간 그 객체를 기록해 두는 스크립트
    ///
    /// 왜 이 방식인가:
    /// 2026-10 판부터 사이트는 좌표 변환과 이동 함수를 가진 지도 상태를 하위 컴포넌트에 넘기지 않는다.
    /// 만들어진 뒤에는 페이지 밖에서 닿을 경로가 없으므로, Vue가 reactive 객체를 WeakMap에 등록하는
    /// 순간에 지도 상태와 층 데이터 표만 골라 기록한다. Pilot 브리지는 지도 상태의 좌표 변환으로 내 위치를
    /// 그릴 자리를 구하고, 층 데이터 표가 채워졌는지 보고 사이트에 층 선택용 좌표를 넘길 때를 정한다.
    ///
    /// 넣는 시점이 다른 스크립트와 다르다:
    /// 페이지 스크립트가 지도 상태를 만들기 전에 들어가 있어야 하므로 page-health.js처럼 FrameLoadStart에 넣는다.
    /// 늦으면 그 문서에서는 지도 상태를 찾지 못하고 브리지가 map-unavailable로 알린다.
    ///
    /// JavaScript 파일 위치: Models/JavaScript/Scripts/map-state-capture.js
    /// </summary>
    public static class MapStateCapture
    {
        /// <summary>
        /// 지도 상태 기록을 등록하는 스크립트
        /// </summary>
        public static string INIT_SCRIPT => JavaScriptLoader.Load("map-state-capture.js");
    }
}
