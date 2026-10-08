namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// Online 사이트 지도 위에 앱의 내 위치 표시(원과 방향 삼각형)를 그리는 스크립트
    /// - 모양은 Local 미니맵의 내 위치와 같다
    /// - 자리와 각도는 Pilot 브리지가 사이트 지도 상태의 좌표 변환으로 구해 넘긴다
    /// </summary>
    public static class MapMarkers
    {
        /// <summary>
        /// 내 위치 표시를 등록하는 스크립트
        ///
        /// JavaScript 파일 위치: Models/JavaScript/Scripts/map-markers.js
        ///
        /// 실행 절차:
        /// 1. Pilot 브리지가 위치를 받으면 show로 자리 계산 함수(locate)와 따라갈지 판정(alive)을 맡긴다
        /// 2. locate가 알려 준 지도 컨테이너에 원 요소 하나(안에 방향 삼각형)를 붙인다
        /// 3. alive가 참인 동안 매 프레임 자리와 화면 각도를 다시 맞춘다
        /// 4. 브리지가 새 스크린샷이라고 알리면 보인 순간 한 번 핑을 켠다
        /// 5. 상태 확인 시 지워진 표시와 멈춘 갱신을 복구한다
        /// </summary>
        public static string INIT_SCRIPT => JavaScriptLoader.Load("map-markers.js");

        public const string ENSURE_READY_SCRIPT =
            "window.tanukiMarker?.version === 7 && typeof window.tanukiMarker.ensure === 'function' " +
            "&& typeof window.tanukiMarker.show === 'function' && window.tanukiMarker.ensure() === true;";
    }
}
