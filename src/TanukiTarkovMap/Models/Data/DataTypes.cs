using System.Text.Json.Serialization;

namespace TanukiTarkovMap.Models.Data
{
    public class MapSetting
    {
        public double Width { get; set; } = 300;
        public double Height { get; set; } = 250;
        public double Left { get; set; } = -1;
        public double Top { get; set; } = -1;
    }

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
        public int BrowserZoomLevel { get; set; } = 67;             // Browser 배율 (%)

        // 창 투명도 설정
        public double WindowOpacity { get; set; } = 1.0;            // 창 투명도 (0.1 ~ 1.0)

        // Goon Tracker 설정
        public bool GoonTrackerEnabled { get; set; } = true;        // Goon Tracker 사용 여부

        public override string ToString()
        {
            return $"gameFolder: '{GameFolder}' \nscreenshotsFolder: '{ScreenshotsFolder}'";
        }
    }

    public class MapChangeData : WsMessage
    {
        [JsonPropertyName("map")] public string Map { get; set; } = "";

        public override string ToString()
        {
            return $"{Map}";
        }
    }

    public class UpdatePositionData : WsMessage
    {
        [JsonPropertyName("x")] public float X { get; set; }
        [JsonPropertyName("y")] public float Y { get; set; }
        [JsonPropertyName("z")] public float Z { get; set; }

        public override string ToString()
        {
            return $"x:{X} y:{Y} z:{Z}";
        }
    }

    public class SendFilenameData : WsMessage
    {
        [JsonPropertyName("filename")]
        public string Filename { get; set; } = "";

        public override string ToString()
        {
            return $"{Filename}";
        }
    }

    public class QuestUpdateData : WsMessage
    {
        [JsonPropertyName("questId")] public string QuestId { get; set; } = "";
        [JsonPropertyName("status")] public string Status { get; set; } = "";

        public override string ToString()
        {
            return $"{QuestId} {Status}";
        }
    }

    public class WsMessage
    {
        [JsonPropertyName("messageType")] public string MessageType { get; set; } = "";
        
        public override string ToString()
        {
            return $"messageType: {MessageType}";
        }
    }

    public class ConfigurationData : WsMessage
    {
        [JsonPropertyName("gameFolder")] public string GameFolder { get; set; } = "";
        [JsonPropertyName("screenshotsFolder")] public string ScreenshotsFolder { get; set; } = "";
        [JsonPropertyName("version")] public string Version { get; set; } = "";

        public override string ToString()
        {
            return $"gameFolder: '{GameFolder}' \n" +
                   $"screenshotsFolder: '{ScreenshotsFolder}' \n" +
                   $"version: '{Version}'";
        }
    }

    public class UpdateSettingsData : AppSettings
    {
        [JsonPropertyName("messageType")] public string MessageType { get; set; } = "";
    }
}
