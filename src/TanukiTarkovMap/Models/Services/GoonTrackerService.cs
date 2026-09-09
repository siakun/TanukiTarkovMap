using System.Net.Http;
using System.Text.RegularExpressions;
using System.Timers;
using TanukiTarkovMap.Models.Utils;

/**
GoonTrackerService - Tarkov Goon Tracker 웹사이트의 최근 군즈 목격 제보 제공

Purpose: 활성화된 동안 PvE 목격 제보를 조회한다. 제보된 맵이 현재 레이드의 출현을 보장하지는 않는다.

Core Functionality:
- FetchCurrentGoonsMapAsync: 웹사이트에서 최근 목격 제보의 맵 파싱
- 주기적 업데이트: 활성화된 동안 제보 갱신
- GoonsMapChanged 이벤트: 제보 맵 변경 시 구독자에게 알림

State Management:
- CurrentGoonsMap: 최근 군즈 목격 제보의 맵 이름
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
        private bool _enabled = false;

        /// <summary>
        /// Goons 위치 맵 이름 목록 (tarkov-goon-tracker에서 사용하는 이름과 앱 내 Name 매핑)
        /// </summary>
        private static readonly HashSet<string> GoonsMapNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "woods", "shoreline", "customs", "lighthouse"
        };

        /// <summary>
        /// 최근 군즈 목격 제보의 맵 이름
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
        /// 최근 군즈 목격 제보의 맵이 변경되었을 때 발생하는 이벤트
        /// </summary>
        public event EventHandler<string?>? GoonsMapChanged;

        /// <summary>
        /// Goon Tracker 활성화 여부
        /// </summary>
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    if (_enabled)
                    {
                        _updateTimer.Start();
                        _ = FetchCurrentGoonsMapAsync();
                        Logger.SimpleLog("[GoonTrackerService] Enabled");
                    }
                    else
                    {
                        _updateTimer.Stop();
                        CurrentGoonsMap = null;
                        Logger.SimpleLog("[GoonTrackerService] Disabled");
                    }
                }
            }
        }

        // 생성 시에는 조회하지 않고 저장된 Enabled 설정을 적용할 때 시작한다.
        internal GoonTrackerService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "TanukiTarkovMap/1.0");

            // 3분마다 업데이트
            _updateTimer = new System.Timers.Timer(3 * 60 * 1000);
            _updateTimer.Elapsed += OnTimerElapsed;
            _updateTimer.AutoReset = true;
        }

        private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            await FetchCurrentGoonsMapAsync();
        }

        /// <summary>
        /// 웹사이트에서 최근 군즈 목격 제보를 가져온다.
        /// </summary>
        public async Task FetchCurrentGoonsMapAsync()
        {
            if (!_enabled || _disposed)
                return;

            try
            {
                var response = await _httpClient.GetStringAsync("https://www.tarkov-goon-tracker.com/pve");

                // 조회 중 기능을 껐으면 늦게 도착한 제보로 표시를 복원하지 않는다.
                if (!_enabled || _disposed)
                    return;

                // HTML에서 최근 군즈 목격 제보의 맵 파싱
                // 패턴: "map":{"name":"Woods" 형태의 JSON에서 첫 번째 맵 이름 추출
                var mapMatch = Regex.Match(response, @"""map"":\s*\{\s*""name""\s*:\s*""([^""]+)""");

                if (mapMatch.Success)
                {
                    var mapName = mapMatch.Groups[1].Value.ToLowerInvariant();
                    if (GoonsMapNames.Contains(mapName))
                    {
                        CurrentGoonsMap = mapName;
                        Logger.SimpleLog($"[GoonTrackerService] Latest reported Goons map: {mapName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[GoonTrackerService] Failed to fetch Goons report: {ex.Message}");
            }
        }

        /// <summary>
        /// 지정된 맵이 최근 군즈 목격 제보의 맵인지 확인한다.
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
