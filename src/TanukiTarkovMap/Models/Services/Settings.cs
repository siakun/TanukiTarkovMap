using System.IO;
using System.Text.Json;
using TanukiTarkovMap.Models.Data;
using TanukiTarkovMap.Models.Utils;

/**
Settings - 앱 설정 파일 관리 (settings.json)

Purpose: AppSettings를 JSON 파일로 저장/로드하며, 첫 실행 시 기본값 자동 생성

Core Functionality:
- Save(): App.GetSettings() 데이터를 settings.json에 저장
- Load(): settings.json 읽기, 없으면 CreateDefaultSettings() 호출
- Delete(): 설정 파일 삭제 (리셋용)

첫 실행 시:
- TarkovPathFinder로 게임/스크린샷 폴더 자동 탐지
- 각 맵별 기본 창 위치/크기 설정값 생성
*/
namespace TanukiTarkovMap.Models.Services
{
    public class Settings
    {
        const string SETTINGS_FILE_PATH = "settings.json";

        public static void Save()
        {
            AppSettings settings = App.GetSettings();

            var json = JsonSerializer.Serialize(
                settings,
                new JsonSerializerOptions { WriteIndented = true }
            );
            File.WriteAllText(SETTINGS_FILE_PATH, json);
        }

        public static void Load()
        {
            if (!File.Exists(SETTINGS_FILE_PATH))
            {
                // 설정 파일이 없으면 기본값으로 생성
                CreateDefaultSettings();
                return;
            }

            try
            {
                var json = File.ReadAllText(SETTINGS_FILE_PATH);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);

                // 경로에 {0} 플레이스홀더가 있으면 현재 사용자 이름으로 치환
                if (settings != null)
                {
                    if (!string.IsNullOrEmpty(settings.GameFolder) && settings.GameFolder.Contains("{0}"))
                    {
                        settings.GameFolder = string.Format(settings.GameFolder, Environment.UserName);
                    }

                    if (!string.IsNullOrEmpty(settings.ScreenshotsFolder) && settings.ScreenshotsFolder.Contains("{0}"))
                    {
                        settings.ScreenshotsFolder = string.Format(settings.ScreenshotsFolder, Environment.UserName);
                    }
                }

                App.SetSettings(settings);
            }
            catch (Exception)
            {
                // 파일 읽기 실패 시 기본값으로 생성
                CreateDefaultSettings();
            }
        }

        private static void CreateDefaultSettings()
        {
            // 첫 실행 시 자동 탐지 실행
            string? detectedGameFolder = TarkovPathFinder.FindGameFolder();
            string? detectedScreenshotsFolder = TarkovPathFinder.FindScreenshotsFolder();

            // 스크린샷 폴더를 찾지 못한 경우 기본 경로 사용
            if (detectedScreenshotsFolder == null)
            {
                detectedScreenshotsFolder = TarkovPathFinder.GetDefaultScreenshotsFolder();
            }

            var defaultSettings = new AppSettings()
            {
                GameFolder = detectedGameFolder,
                ScreenshotsFolder = detectedScreenshotsFolder,
                NormalWidth = 800,
                NormalHeight = 600,
                NormalLeft = -1,
                NormalTop = -1,
                MapSettings = CreateDefaultMapSettings(),
                HotkeyEnabled = true,
                HotkeyKey = "F11",
                autoDeleteLogs = false,
                autoDeleteScreenshots = false,
                IsAlwaysOnTop = true,   // 기본적으로 항상 위 활성화
            };

            App.SetSettings(defaultSettings, true);
            Save(); // 기본 설정을 파일로 저장
        }

        private static System.Collections.Generic.Dictionary<
            string,
            MapSetting
        > CreateDefaultMapSettings()
        {
            var mapSettings = new System.Collections.Generic.Dictionary<string, MapSetting>();

            // 테스트 결과 기반 실제 게임 내부 이름들로 기본 설정값 생성

            // Factory (factory_day_preset)
            mapSettings["factory_day_preset"] = new MapSetting()
            {
                Width = 327,
                Height = 315,
                Left = 1596,
                Top = 643,
            };

            // Woods (woods_preset)
            mapSettings["woods_preset"] = new MapSetting()
            {
                Width = 365,
                Height = 343,
                Left = 1559,
                Top = 613,
            };

            // Customs (customs_preset)
            mapSettings["customs_preset"] = new MapSetting()
            {
                Width = 428,
                Height = 211,
                Left = 1499,
                Top = 746,
            };

            // Reserve (rezerv_base_preset)
            mapSettings["rezerv_base_preset"] = new MapSetting()
            {
                Width = 317,
                Height = 250,
                Left = 1604,
                Top = 706,
            };

            // Ground Zero (sandbox_high_preset)
            mapSettings["sandbox_high_preset"] = new MapSetting()
            {
                Width = 328,
                Height = 362,
                Left = 1599,
                Top = 613,
            };

            // Streets of Tarkov (city_preset)
            mapSettings["city_preset"] = new MapSetting()
            {
                Width = 367,
                Height = 344,
                Left = 1553,
                Top = 685,
            };

            // Lighthouse (lighthouse_preset)
            mapSettings["lighthouse_preset"] = new MapSetting()
            {
                Width = 299,
                Height = 414,
                Left = 1622,
                Top = 548,
            };

            // Interchange (shopping_mall)
            mapSettings["shopping_mall"] = new MapSetting()
            {
                Width = 282,
                Height = 249,
                Left = 1644,
                Top = 709,
            };

            // Shoreline (shoreline_preset)
            mapSettings["shoreline_preset"] = new MapSetting()
            {
                Width = 409,
                Height = 261,
                Left = 1517,
                Top = 697,
            };

            // The Lab (laboratory_preset)
            mapSettings["laboratory_preset"] = new MapSetting()
            {
                Width = 357,
                Height = 311,
                Left = 1560,
                Top = 660,
            };

            return mapSettings;
        }

        public static void Delete()
        {
            try
            {
                if (!File.Exists(SETTINGS_FILE_PATH))
                    return;

                File.Delete(SETTINGS_FILE_PATH);
            }
            catch (Exception) { }
        }
    }
}
