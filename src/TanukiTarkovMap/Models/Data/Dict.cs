namespace TanukiTarkovMap.Models.Data
{
    /// <summary>
    /// 게임 내부 Location ID → 맵 표시 이름 변환 딕셔너리
    /// 게임 로그에서 읽은 Location 값을 UI 표시용 맵 이름으로 변환할 때 사용
    /// </summary>
    public class Dict
    {
        /// <summary>
        /// 게임 내부 Location ID를 MapName 상수로 매핑
        /// Key: 게임 로그의 location 값 (예: "sandbox", "bigmap")
        /// Value: MapName 상수 (예: "Ground Zero", "Customs")
        /// </summary>
        public static Dictionary<string, string> LocationToMap = new Dictionary<string, string>
        {
            { "sandbox", MapName.Ground_Zero },
            { "sandbox_high", MapName.Ground_Zero },
            { "factory_day", MapName.Factory },
            { "factory_night", MapName.Factory },
            { "factory4_day", MapName.Factory },
            { "factory4_night", MapName.Factory },
            { "bigmap", MapName.Customs },
            { "woods", MapName.Woods },
            { "shoreline", MapName.Shoreline },
            { "interchange", MapName.Interchange },
            { "shopping_mall", MapName.Interchange },
            { "rezervbase", MapName.Reserve },
            { "rezerv_base", MapName.Reserve },
            { "laboratory", MapName.The_Lab },
            { "lighthouse", MapName.Lighthouse },
            { "tarkovstreets", MapName.Streets_of_Tarkov },
            { "city", MapName.Streets_of_Tarkov },
        };
    }
}
