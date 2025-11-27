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

        public MapInfo(string name, string displayName, string url, string mapId)
        {
            Name = name;
            DisplayName = displayName;
            Url = url;
            MapId = mapId;
        }
    }
}
