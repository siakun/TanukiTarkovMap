namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// 맵 마커 및 방향 표시기 관련 JavaScript 스크립트
    /// - 플레이어 위치에 방향 표시 삼각형 추가
    /// - 스크린샷의 회전값과 지도의 좌표 변환을 적용한 방향 표시
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
        /// 3. 부모의 CSS 변환을 보존하고 자식 삼각형의 각도만 보정
        /// 4. 지도 영역 밖의 마커도 MutationObserver로 감지 및 자동 처리
        /// 5. 상태 확인 시 스타일과 DOM 감시를 복구하고 파일명에서 읽은 방향 적용
        /// </summary>
        public static string ADD_DIRECTION_INDICATORS_SCRIPT =>
            JavaScriptLoader.Load("map-markers.js");

        public const string ENSURE_READY_SCRIPT =
            "window.tanukiDirection?.version === 5 && typeof window.tanukiDirection.ensure === 'function' " +
            "&& typeof window.tanukiDirection.setHeading === 'function' && window.tanukiDirection.ensure() === true;";
    }
}
