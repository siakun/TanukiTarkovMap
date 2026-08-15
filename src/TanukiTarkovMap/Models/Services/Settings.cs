using System.IO;
using System.Text.Json;
using TanukiTarkovMap.Models.Data;
using TanukiTarkovMap.Models.Migrations;
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

현재 파일 위치는 AppPaths가 정하고, SettingsLocationMigration이 이전 실패 때 읽을 후보를 정한다.
앱을 제거해도 설정이 남도록 Velopack 설치 폴더 밖에 둔다.
*/
namespace TanukiTarkovMap.Models.Services
{
    public class Settings
    {
        public static void Save()
        {
            AppSettings settings = App.GetSettings();

            var json = JsonSerializer.Serialize(
                settings,
                new JsonSerializerOptions { WriteIndented = true }
            );
            AppPaths.WriteSettingsFile(json);
        }

        public static void Load()
        {
            // 더 최신인 위치부터 읽되 손상됐거나 접근할 수 없으면 다른 위치를 시도한다.
            // 한쪽 파일의 실패만으로 정상인 설정까지 버리고 기본값을 만들지 않는다
            foreach (var readPath in SettingsLocationMigration.SettingsReadPaths)
            {
                try
                {
                    var json = File.ReadAllText(readPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json)
                                   ?? throw new InvalidDataException($"설정 파일이 비어 있습니다: {readPath}");

                    // 경로에 {0} 플레이스홀더가 있으면 현재 사용자 이름으로 치환
                    if (!string.IsNullOrEmpty(settings.GameFolder) && settings.GameFolder.Contains("{0}"))
                    {
                        settings.GameFolder = string.Format(settings.GameFolder, Environment.UserName);
                    }

                    if (!string.IsNullOrEmpty(settings.ScreenshotsFolder) && settings.ScreenshotsFolder.Contains("{0}"))
                    {
                        settings.ScreenshotsFolder = string.Format(settings.ScreenshotsFolder, Environment.UserName);
                    }

                    App.SetSettings(settings);
                    return;
                }
                catch (Exception ex)
                {
                    Logger.SimpleLog($"[Settings] Failed to load {readPath}: {ex.Message}");
                }
            }

            // 두 위치가 모두 없거나 읽지 못했을 때만 기본값을 만든다
            CreateDefaultSettings();
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
            // 한쪽만 지우면 다음 실행에서 남은 파일이 다시 병합돼 초기화가 되돌아간다
            AppPaths.DeleteSettingsFiles();
        }
    }
}
