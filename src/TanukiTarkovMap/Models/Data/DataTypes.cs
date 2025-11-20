using System.Text.Json.Serialization;

namespace TanukiTarkovMap.Models.Data
{
    public class MapSetting
    {
        public bool Enabled { get; set; } = true;                // 해당 맵에서 PiP 기능 활성화 여부
        public string Transform { get; set; } = "";
        public double Width { get; set; } = 300;
        public double Height { get; set; } = 250;
        public double Left { get; set; } = -1;
        public double Top { get; set; } = -1;
    }

    public class AppSettings
    {
        public string GameFolder { get; set; } = "";
        public string ScreenshotsFolder { get; set; } = "";

        // PiP 설정 추가
        public bool PipEnabled { get; set; } = false;            // PiP 기능 활성화/비활성화
        public bool PipRememberPosition { get; set; } = true;    // 위치 기억 여부
        public bool PipHotkeyEnabled { get; set; } = false;      // PiP 활성화 버튼 사용 여부
        public string PipHotkeyKey { get; set; } = "F11";        // 사용자 설정 핫키
        public bool PipHideWebElements { get; set; } = true;     // PiP 모드에서 웹 UI 요소 숨기기

        // 일반 모드 설정 추가
        public double NormalWidth { get; set; } = 0f;            // 일반 모드 창 너비
        public double NormalHeight { get; set; } = 0f;           // 일반 모드 창 높이
        public double NormalLeft { get; set; } = 0f;             // 일반 모드 창 X 위치 (-1: 자동 계산)
        public double NormalTop { get; set; } = 0f;              // 일반 모드 창 Y 위치 (-1: 자동 계산)

        // 맵별 개별 설정
        public Dictionary<string, MapSetting> MapSettings { get; set; } = new();

        // PiP 자동 복원 설정
        public bool enableAutoRestore { get; set; } = true;         // 자동 요소 복원 기능 활성화
        public double restoreThresholdWidth { get; set; } = 800;    // 복원 임계 너비
        public double restoreThresholdHeight { get; set; } = 600;   // 복원 임계 높이

        // 파일 자동 정리 설정
        public bool autoDeleteLogs { get; set; } = false;           // 로그 폴더 자동 정리
        public bool autoDeleteScreenshots { get; set; } = false;    // 스크린샷 자동 정리

        public override string ToString()
        {
            return $"gameFolder: '{GameFolder}' \nscreenshotsFolder: '{ScreenshotsFolder}' \npipEnabled: {PipEnabled}";
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
