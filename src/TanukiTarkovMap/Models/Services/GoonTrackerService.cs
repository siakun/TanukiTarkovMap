using System.Net.Http;
using System.Text.RegularExpressions;
using System.Timers;
using TanukiTarkovMap.Models.Utils;

/**
GoonTrackerService - Tarkov Goon Tracker 웹사이트에서 Goons 위치 정보 제공

Purpose: tarkov-goon-tracker.com/pve에서 현재 Goons가 있는 맵을 주기적으로 조회하여 제공

Core Functionality:
- FetchCurrentGoonsMap: 웹사이트에서 현재 Goons 위치 파싱
- 주기적 업데이트: 5분마다 자동으로 위치 갱신
- GoonsMapChanged 이벤트: 맵 변경 시 구독자에게 알림

State Management:
- CurrentGoonsMap: 현재 Goons가 있는 맵 이름 (예: "Woods", "Customs")
- _updateTimer: 주기적 업데이트용 타이머

Dependencies:
- HttpClient: 웹 페이지 요청
*/
namespace TanukiTarkovMap.Models.Services
{
    public class GoonTrackerService : IDisposable
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly System.Timers.Timer _updateTimer;
        private string? _currentGoonsMap;
        private bool _disposed = false;

        /// <summary>
        /// Goons 위치 맵 이름 목록 (tarkov-goon-tracker에서 사용하는 이름과 앱 내 Name 매핑)
        /// </summary>
        private static readonly HashSet<string> GoonsMapNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "woods", "shoreline", "customs", "lighthouse"
        };

        /// <summary>
        /// 현재 Goons가 있는 맵 이름 (예: "woods", "customs")
        /// </summary>
        public string? CurrentGoonsMap
        {
            get => _currentGoonsMap;
            private set
            {
                if (_currentGoonsMap != value)
                {
                    _currentGoonsMap = value;
                    GoonsMapChanged?.Invoke(this, value);
                }
            }
        }

        /// <summary>
        /// Goons 맵 위치가 변경되었을 때 발생하는 이벤트
        /// </summary>
        public event EventHandler<string?>? GoonsMapChanged;

        internal GoonTrackerService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "TanukiTarkovMap/1.0");

            // 3분마다 업데이트
            _updateTimer = new System.Timers.Timer(3 * 60 * 1000);
            _updateTimer.Elapsed += OnTimerElapsed;
            _updateTimer.AutoReset = true;

            // 초기 로드
            _ = FetchCurrentGoonsMapAsync();
            _updateTimer.Start();
        }

        private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            await FetchCurrentGoonsMapAsync();
        }

        /// <summary>
        /// 웹사이트에서 현재 Goons 위치를 가져옵니다.
        /// </summary>
        public async Task FetchCurrentGoonsMapAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync("https://www.tarkov-goon-tracker.com/pve");

                // HTML에서 최근 Goons 위치 파싱
                // 패턴: "map":{"name":"Woods" 형태의 JSON에서 첫 번째 맵 이름 추출
                var mapMatch = Regex.Match(response, @"""map"":\s*\{\s*""name""\s*:\s*""([^""]+)""");

                if (mapMatch.Success)
                {
                    var mapName = mapMatch.Groups[1].Value.ToLowerInvariant();
                    if (GoonsMapNames.Contains(mapName))
                    {
                        CurrentGoonsMap = mapName;
                        Logger.SimpleLog($"[GoonTrackerService] Current Goons map: {mapName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[GoonTrackerService] Failed to fetch Goons location: {ex.Message}");
            }
        }

        /// <summary>
        /// 지정된 맵에 Goons가 있는지 확인합니다.
        /// </summary>
        /// <param name="mapName">맵 이름 (MapInfo.Name)</param>
        public bool IsGoonsOnMap(string? mapName)
        {
            if (string.IsNullOrEmpty(mapName) || string.IsNullOrEmpty(CurrentGoonsMap))
                return false;

            return mapName.Equals(CurrentGoonsMap, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 지정된 맵이 Goons가 나타날 수 있는 맵인지 확인합니다.
        /// </summary>
        /// <param name="mapName">맵 이름</param>
        public static bool CanGoonsSpawnOnMap(string? mapName)
        {
            return !string.IsNullOrEmpty(mapName) && GoonsMapNames.Contains(mapName);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _updateTimer.Stop();
                _updateTimer.Dispose();
                _disposed = true;
            }
        }
    }
}
