namespace TanukiTarkovMap.Models.Data
{
    /// <summary>
    /// 타르코프 맵 정보
    /// </summary>
    public class MapInfo
    {
        /// <summary>
        /// 맵 식별자 (예: "ground-zero", "factory")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 맵 표시 이름 (예: "Ground Zero", "Factory")
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 맵 페이지 URL
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 맵 식별자 (예: "sandbox_high_preset", "factory_day_preset")
        /// tarkov-market.com 내부에서 사용하는 맵 ID
        /// </summary>
        public string MapId { get; set; }

        /// <summary>
        /// 이 맵에 해당하는 게임 로그의 scene preset 값 목록
        /// 첫 항목은 MapId이며, 같은 맵의 다른 프리셋(레벨 구간, 시간대)이 뒤에 붙는다
        /// </summary>
        public IReadOnlyList<string> ScenePresets { get; }

        public MapInfo(string name, string displayName, string url, string mapId, params string[] extraScenePresets)
        {
            Name = name;
            DisplayName = displayName;
            Url = url;
            MapId = mapId;
            ScenePresets = new[] { mapId }.Concat(extraScenePresets).ToArray();
        }
    }
}
