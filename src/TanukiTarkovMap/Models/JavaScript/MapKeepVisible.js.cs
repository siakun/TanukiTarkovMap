namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// 맵을 열 때 실제 그림을 창에 맞추고, 끌거나 확대해도 그림이 화면 가운데를 벗어나지 않게 하는 스크립트
    ///
    /// 사이트의 경계는 빈 여백을 포함한 좌표 공간을 기준으로 삼으므로 실제 지형이 화면 밖으로
    /// 나갈 수 있습니다. 화면에 표시된 지형 캔버스의 픽셀로 그림 범위를 잽니다.
    ///
    /// 배율은 사이트의 휠 입력으로, 위치는 사이트 panzoom의 moveBy로 맞춥니다. 드래그 중에는 전달할
    /// 커서 이동량을 줄이고, 휠 확대와 창 크기 변경 뒤에는 사이트가 그린 다음 프레임에 범위로 되돌립니다.
    /// 내 위치가 표시되면 화면 가운데는 위치가 정합니다. 직접 조작이 시작되면 초기 맞춤을 중단합니다.
    ///
    /// JavaScript 파일 위치: Models/JavaScript/Scripts/map-keep-visible.js
    /// </summary>
    public static class MapKeepVisible
    {
        /// <summary>
        /// 초기 그림 맞춤과 이동 제한 스크립트
        /// </summary>
        public static string KEEP_MAP_VISIBLE_SCRIPT => JavaScriptLoader.Load("map-keep-visible.js");
    }
}
