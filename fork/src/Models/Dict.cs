using System.Collections.Generic;

namespace TarkovClient
{
    public class Dict
    {
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
