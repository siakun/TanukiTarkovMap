namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// 맵 마커 및 방향 표시기 관련 JavaScript 스크립트
    /// - 플레이어 위치에 방향 표시 삼각형 추가
    /// - 마커 회전에 따른 방향 표시기 회전
    /// </summary>
    public static class MapMarkers
    {
        /// <summary>
        /// 방향 표시기를 추가하는 스크립트
        ///
        /// JavaScript 파일 위치: Models/JavaScript/Scripts/map-markers.js
        ///
        /// 실행 절차:
        /// 1. SVG 기반 삼각형 아이콘을 CSS 스타일로 정의
        /// 2. 모든 마커(.marker) 요소를 찾아서 삼각형 추가
        /// 3. 마커의 transform rotate 값을 읽어 삼각형도 같은 각도로 회전
        /// 4. MutationObserver로 새로 추가되는 마커 감지 및 자동 처리
        /// 5. 2초 후 초기 마커 초기화 실행 (DOM 로딩 대기)
        /// </summary>
        public static string ADD_DIRECTION_INDICATORS_SCRIPT =>
            JavaScriptLoader.Load("map-markers.js");
    }
}
