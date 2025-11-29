namespace TanukiTarkovMap.Models.Data
{
    /// <summary>
    /// 타르코프 맵 설정 및 목록을 관리하는 정적 클래스
    /// </summary>
    public static class MapConfiguration
    {
        /// <summary>
        /// 사용 가능한 타르코프 맵 목록
        /// </summary>
        public static List<MapInfo> AvailableMaps { get; } = new()
        {
            new MapInfo("ground-zero", "Ground Zero", "https://tarkov-market.com/maps/ground-zero", "sandbox_high_preset"),
            new MapInfo("factory", "Factory", "https://tarkov-market.com/maps/factory", "factory_day_preset"),
            new MapInfo("customs", "Customs", "https://tarkov-market.com/maps/customs", "customs_preset"),
            new MapInfo("interchange", "Interchange", "https://tarkov-market.com/maps/interchange", "shopping_mall"),
            new MapInfo("woods", "Woods", "https://tarkov-market.com/maps/woods", "woods_preset"),
            new MapInfo("shoreline", "Shoreline", "https://tarkov-market.com/maps/shoreline", "shoreline_preset"),
            new MapInfo("reserve", "Reserve", "https://tarkov-market.com/maps/reserve", "rezerv_base_preset"),
            new MapInfo("lighthouse", "Lighthouse", "https://tarkov-market.com/maps/lighthouse", "lighthouse_preset"),
            new MapInfo("streets", "Streets of Tarkov", "https://tarkov-market.com/maps/streets", "city_preset"),
            new MapInfo("lab", "The Lab", "https://tarkov-market.com/maps/lab", "laboratory_preset"),
            new MapInfo("labyrinth", "Labyrinth", "https://tarkov-market.com/maps/labyrinth", "labyrinth")
        };

        /// <summary>
        /// 맵 이름으로 MapInfo 조회
        /// </summary>
        public static MapInfo? GetByName(string name)
            => AvailableMaps.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// 맵 ID로 MapInfo 조회
        /// </summary>
        public static MapInfo? GetByMapId(string mapId)
            => AvailableMaps.FirstOrDefault(m => m.MapId.Equals(mapId, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// 표시 이름으로 MapInfo 조회
        /// </summary>
        public static MapInfo? GetByDisplayName(string displayName)
            => AvailableMaps.FirstOrDefault(m => m.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase));
    }
}
