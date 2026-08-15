namespace TanukiTarkovMap.Models.Data
{
    /// <summary>
    /// 타르코프 맵 설정 및 목록을 관리하는 정적 클래스
    /// </summary>
    public static class MapConfiguration
    {
        /// <summary>
        /// 사용 가능한 타르코프 맵 목록
        ///
        /// scene preset 이름은 추측하지 말고 설치된 게임에서 확인한다.
        /// EscapeFromTarkov_Data 아래 maps 폴더의 파일 이름이 그대로 로그의
        /// "path:maps/{이름}.bundle"에 찍히며, LogsWatcher가 그 값으로 맵을 찾는다.
        /// 이름 하나가 어긋나면 그 맵만 조용히 자동 전환되지 않는다.
        ///
        /// 목록에 넣으려면 열어 볼 맵 페이지가 함께 있어야 한다. 게임 번들에는 있지만
        /// 볼 페이지가 없는 맵(개발용, Arena 등)은 여기 넣지 않는다.
        /// </summary>
        public static List<MapInfo> AvailableMaps { get; } = new()
        {
            new MapInfo("ground-zero", "Ground Zero", "https://tarkov-market.com/maps/ground-zero", "sandbox_high_preset", "sandbox_preset", "sandbox_start_preset"),
            new MapInfo("factory", "Factory", "https://tarkov-market.com/maps/factory", "factory_day_preset", "factory_night_preset"),
            new MapInfo("customs", "Customs", "https://tarkov-market.com/maps/customs", "customs_preset"),
            new MapInfo("interchange", "Interchange", "https://tarkov-market.com/maps/interchange", "shopping_mall"),
            new MapInfo("woods", "Woods", "https://tarkov-market.com/maps/woods", "woods_preset"),
            new MapInfo("shoreline", "Shoreline", "https://tarkov-market.com/maps/shoreline", "shoreline_preset"),
            new MapInfo("reserve", "Reserve", "https://tarkov-market.com/maps/reserve", "rezerv_base_preset"),
            new MapInfo("lighthouse", "Lighthouse", "https://tarkov-market.com/maps/lighthouse", "lighthouse_preset"),
            new MapInfo("streets", "Streets of Tarkov", "https://tarkov-market.com/maps/streets", "city_preset"),
            new MapInfo("lab", "The Lab", "https://tarkov-market.com/maps/lab", "laboratory_preset", "laboratory_dark_preset"),
            // MapId는 맵별 창 크기와 위치를 저장하는 열쇠라 바꾸면 사용자의 저장값이 끊긴다.
            // 실제 번들 이름은 labyrinth_preset이므로 MapId는 두고 프리셋만 덧붙인다
            new MapInfo("labyrinth", "Labyrinth", "https://tarkov-market.com/maps/labyrinth", "labyrinth", "labyrinth_preset"),
            new MapInfo("icebreaker", "Icebreaker", "https://tarkov-market.com/maps/icebreaker", "icebreaker")
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

        /// <summary>
        /// 게임 로그의 scene preset 값으로 MapInfo 조회
        /// 한 맵에 프리셋이 여럿인 경우(Ground Zero 레벨 구간, Factory 시간대)를 모두 같은 맵으로 매핑한다
        /// </summary>
        public static MapInfo? GetByScenePreset(string scenePreset)
            => AvailableMaps.FirstOrDefault(m => m.ScenePresets.Contains(scenePreset, StringComparer.OrdinalIgnoreCase));
    }
}
