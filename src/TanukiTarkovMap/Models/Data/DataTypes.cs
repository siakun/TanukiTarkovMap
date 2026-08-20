namespace TanukiTarkovMap.Models.Data
{
    /// <summary>
    /// 맵별 창 위치/크기 설정 (settings.json에 저장)
    /// </summary>
    public class MapSetting
    {
        public double Width { get; set; } = 300;
        public double Height { get; set; } = 250;
        public double Left { get; set; } = -1;
        public double Top { get; set; } = -1;
    }

    /// <summary>
    /// 앱 전체 설정 (settings.json으로 직렬화/역직렬화)
    /// Settings.Save()/Load()를 통해 파일로 저장됨
    /// </summary>
    public class AppSettings
    {
        public const string DefaultHotkeyKey = "F11";

        public string GameFolder { get; set; } = "";
        public string ScreenshotsFolder { get; set; } = "";

        // 마지막 선택한 맵 (앱 시작 시 복원용)
        public string SelectedMapId { get; set; } = "";          // 마지막으로 선택한 맵 ID

        // 일반 모드 설정 추가
        public double NormalWidth { get; set; } = 0f;            // 일반 모드 창 너비
        public double NormalHeight { get; set; } = 0f;           // 일반 모드 창 높이
        public double NormalLeft { get; set; } = 0f;             // 일반 모드 창 X 위치 (-1: 자동 계산)
        public double NormalTop { get; set; } = 0f;              // 일반 모드 창 Y 위치 (-1: 자동 계산)

        // 맵별 개별 설정
        public Dictionary<string, MapSetting> MapSettings { get; set; } = new();

        // 전역 단축키 설정
        public bool HotkeyEnabled { get; set; } = true;          // 전역 단축키 사용 여부
        public string HotkeyKey { get; set; } = DefaultHotkeyKey; // 단축키 (트레이 숨기기/열기)

        // 파일 자동 정리 설정
        public bool autoDeleteLogs { get; set; } = false;           // 로그 폴더 자동 정리
        public bool autoDeleteScreenshots { get; set; } = false;    // 스크린샷 자동 정리

        // 창 고정 설정
        public bool IsAlwaysOnTop { get; set; } = true;             // 항상 위 (Topmost) 설정 - 기본값 활성화

        // Browser 배율 설정
        // 저장된 값이 없거나 0인 설정 파일을 대비해 기본값과 보정을 여기 한 곳에 둔다.
        // 값을 읽는 쪽이 저마다 기본값을 적으면 한쪽만 바뀌어도 조용히 어긋난다
        public const int DefaultBrowserZoomLevel = 67;              // Browser 배율 기본값 (%)

        public int BrowserZoomLevel { get; set; } = DefaultBrowserZoomLevel;  // Browser 배율 (%)

        /// <summary>저장된 배율. 값이 없거나 0이면 기본값으로 보정한다</summary>
        public int EffectiveBrowserZoomLevel
            => BrowserZoomLevel > 0 ? BrowserZoomLevel : DefaultBrowserZoomLevel;

        // 창 투명도 설정
        public double WindowOpacity { get; set; } = 1.0;            // 창 투명도 (0.1 ~ 1.0)

        // Goon Tracker 설정
        public bool GoonTrackerEnabled { get; set; } = true;        // Goon Tracker 사용 여부

        // 자동 맵 전환 설정.
        // 두 경로를 따로 끄고 켠다. 진입 감지는 게임 로그에서 방금 읽은 사실이라 확실하지만,
        // 스크린샷 보정은 마지막으로 읽어 둔 맵을 다시 쓰는 추측이라 신뢰도가 다르다
        public bool AutoMapSwitchEnabled { get; set; } = true;         // 맵 입장 시 자동 맵 변경
        public bool ScreenshotMapSyncEnabled { get; set; } = true;     // 스크린샷 촬영 시 로그의 현재 맵으로 이동

        // 오프라인 맵 (실험적 기능)
        public bool LocalMapEnabled { get; set; } = false;          // 상단바에 Online/Local 전환 표시
        public bool LocalMapModeActive { get; set; } = false;       // 마지막으로 고른 전환 상태

        // 업데이트 설정
        public bool AutoUpdateEnabled { get; set; } = true;         // 새 버전 자동 확인/다운로드
        public bool PrereleaseEnabled { get; set; } = false;        // 베타(프리릴리스) 버전까지 받기

        public override string ToString()
        {
            return $"gameFolder: '{GameFolder}' \nscreenshotsFolder: '{ScreenshotsFolder}'";
        }
    }
}
