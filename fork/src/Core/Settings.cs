using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TarkovClient
{
    class Settings
    {
        const string SETTINGS_FILE_PATH = "settings.json";

        public static void Save()
        {
            AppSettings settings = Env.GetSettings();

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

                Env.SetSettings(settings);
            }
            catch (Exception)
            {
                // 파일 읽기 실패 시 기본값으로 생성
                CreateDefaultSettings();
            }
        }

        private static void CreateDefaultSettings()
        {
            var defaultSettings = new AppSettings()
            {
                gameFolder = null,
                screenshotsFolder = null,
                pipEnabled = false,
                pipRememberPosition = true,
                pipHotkeyEnabled = false,
                pipHotkeyKey = "F11",
                normalWidth = 1400,
                normalHeight = 900,
                normalLeft = -1,
                normalTop = -1,
                mapSettings = CreateDefaultMapSettings(),
                enableAutoRestore = true,
                restoreThresholdWidth = 800,
                restoreThresholdHeight = 600,
                autoDeleteLogs = false,
                autoDeleteScreenshots = false,
            };

            Env.SetSettings(defaultSettings, true);
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
                enabled = true,
                transform = "matrix(0.166113, 0, 0, 0.166113, -165.258, -154.371)",
                width = 327,
                height = 315,
                left = 1596,
                top = 643,
            };

            // Woods (woods_preset)
            mapSettings["woods_preset"] = new MapSetting()
            {
                enabled = true,
                transform = "matrix(0.111237, 0, 0, 0.111237, -101.331, -113.302)",
                width = 365,
                height = 343,
                left = 1559,
                top = 613,
            };

            // Customs (customs_preset)
            mapSettings["customs_preset"] = new MapSetting()
            {
                enabled = true,
                transform = "matrix(0.177979, 0, 0, 0.177979, -215.026, -185.151)",
                width = 428,
                height = 211,
                left = 1499,
                top = 746,
            };

            // Reserve (rezerv_base_preset)
            mapSettings["rezerv_base_preset"] = new MapSetting()
            {
                enabled = true,
                transform = "matrix(0.222473, 0, 0, 0.222473, -227.365, -224.862)",
                width = 317,
                height = 250,
                left = 1604,
                top = 706,
            };

            // Ground Zero (sandbox_high_preset)
            mapSettings["sandbox_high_preset"] = new MapSetting()
            {
                enabled = true,
                transform = "matrix(0.347614, 0, 0, 0.347614, -346.781, -365.505)",
                width = 328,
                height = 362,
                left = 1599,
                top = 613,
            };

            // Streets of Tarkov (city_preset)
            mapSettings["city_preset"] = new MapSetting()
            {
                enabled = true,
                transform = "matrix(0.21875, 0, 0, 0.21875, -193.814, -223.336)",
                width = 367,
                height = 344,
                left = 1553,
                top = 685,
            };

            // Lighthouse (lighthouse_preset)
            mapSettings["lighthouse_preset"] = new MapSetting()
            {
                enabled = true,
                transform = "matrix(0.241013, 0, 0, 0.241013, -258.081, -256.536)",
                width = 299,
                height = 414,
                left = 1622,
                top = 548,
            };

            // Interchange (shopping_mall)
            mapSettings["shopping_mall"] = new MapSetting()
            {
                enabled = true,
                transform = "matrix(0.125141, 0, 0, 0.125141, -124.377, -127.995)",
                width = 282,
                height = 249,
                left = 1644,
                top = 709,
            };

            // Shoreline (shoreline_preset)
            mapSettings["shoreline_preset"] = new MapSetting()
            {
                enabled = true,
                transform = "matrix(0.222473, 0, 0, 0.222473, -231.212, -228.746)",
                width = 409,
                height = 261,
                left = 1517,
                top = 697,
            };

            // The Lab (laboratory_preset)
            mapSettings["laboratory_preset"] = new MapSetting()
            {
                enabled = true,
                transform = "matrix(0.124512, 0, 0, 0.124512, -191.645, -129.873)",
                width = 357,
                height = 311,
                left = 1560,
                top = 660,
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
