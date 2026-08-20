namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// 맵이 화면에서 사라지지 않게 되돌리는 스크립트
    ///
    /// 사이트의 이동에는 제한이 없어 조금만 끌어도 맵이 화면 밖으로 나가고 빈 격자만 남습니다.
    /// 레이드 중에 그 상태를 되돌리는 데 시간이 듭니다.
    ///
    /// 끄는 동안에는 막지 않습니다. 손을 뗐을 때 화면 한가운데에 맵이 없으면 그 자리를 덮을
    /// 때까지 미끄러져 옵니다. 확대와 축소, 창 크기 변경 뒤에도 같은 판정을 합니다.
    ///
    /// JavaScript 파일 위치: Models/JavaScript/Scripts/map-keep-visible.js
    /// </summary>
    public static class MapKeepVisible
    {
        /// <summary>
        /// 화면 한가운데에 맵이 없으면 가운데로 끌어오는 스크립트
        /// </summary>
        public static string KEEP_MAP_VISIBLE_SCRIPT => JavaScriptLoader.Load("map-keep-visible.js");
    }
}
