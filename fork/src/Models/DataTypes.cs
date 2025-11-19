using System.Collections.Generic;

namespace TarkovClient
{
    public class MapSetting
    {
        public bool enabled { get; set; } = true; // 해당 맵에서 PiP 기능 활성화 여부
        public string transform { get; set; }
        public double width { get; set; } = 300;
        public double height { get; set; } = 250;
        public double left { get; set; } = -1;
        public double top { get; set; } = -1;
    }

    public class AppSettings
    {
        public string gameFolder { get; set; }
        public string screenshotsFolder { get; set; }

        // PiP 설정 추가
        public bool pipEnabled { get; set; } = false; // PiP 기능 활성화/비활성화
        public bool pipRememberPosition { get; set; } = true; // 위치 기억 여부
        public bool pipHotkeyEnabled { get; set; } = false; // PiP 활성화 버튼 사용 여부
        public string pipHotkeyKey { get; set; } = "F11"; // 사용자 설정 핫키

        // 일반 모드 설정 추가
        public double normalWidth { get; set; } // 일반 모드 창 너비
        public double normalHeight { get; set; } // 일반 모드 창 높이
        public double normalLeft { get; set; } // 일반 모드 창 X 위치 (-1: 자동 계산)
        public double normalTop { get; set; } // 일반 모드 창 Y 위치 (-1: 자동 계산)

        // 맵별 개별 설정
        public Dictionary<string, MapSetting> mapSettings { get; set; } = new Dictionary<string, MapSetting>();

        // PiP 자동 복원 설정
        public bool enableAutoRestore { get; set; } = true; // 자동 요소 복원 기능 활성화
        public double restoreThresholdWidth { get; set; } = 800; // 복원 임계 너비
        public double restoreThresholdHeight { get; set; } = 600; // 복원 임계 높이

        // 파일 자동 정리 설정
        public bool autoDeleteLogs { get; set; } = false; // 로그 폴더 자동 정리
        public bool autoDeleteScreenshots { get; set; } = false; // 스크린샷 자동 정리

        public override string ToString()
        {
            return $"gameFolder: '{gameFolder}' \nscreenshotsFolder: '{screenshotsFolder}' \npipEnabled: {pipEnabled}";
        }
    }

    public class MapChangeData : WsMessage
    {
        public string map { get; set; }

        public override string ToString()
        {
            return $"{map}";
        }
    }

    public class UpdatePositionData : WsMessage
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }

        public override string ToString()
        {
            return $"x:{x} y:{y} z:{z}";
        }
    }

    public class SendFilenameData : WsMessage
    {
        public string filename { get; set; }

        public override string ToString()
        {
            return $"{filename}";
        }
    }

    public class QuestUpdateData : WsMessage
    {
        public string questId { get; set; }
        public string status { get; set; }

        public override string ToString()
        {
            return $"{questId} {status}";
        }
    }

    public class WsMessage
    {
        public string messageType { get; set; }

        public override string ToString()
        {
            return $"messageType: {messageType}";
        }
    }

    public class ConfigurationData : WsMessage
    {
        public string gameFolder { get; set; }
        public string screenshotsFolder { get; set; }
        public string version { get; set; }

        public override string ToString()
        {
            return $"gameFolder: '{gameFolder}' \nscreenshotsFolder: '{screenshotsFolder}' \nversion: '{version}'";
        }
    }

    public class UpdateSettingsData : AppSettings
    {
        public string messageType { get; set; }
    }
}
