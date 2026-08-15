using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using TanukiTarkovMap.Models.Utils;

/**
SettingsLocationMigration - 0.1.0 설정 파일을 Local에서 Roaming으로 이전

Purpose: 예전 위치의 settings.json을 현재 설정과 병합하고, 이전 실패 뒤에도 읽을 수 있는
후보 순서를 제공해 사용자가 저장한 설정이 기본값으로 바뀌지 않게 한다.

Architecture: AppPaths.PrepareOnStartup()이 CEF 초기화 전에 Migrate()를 한 번 호출한다.
현재 경로는 AppPaths가 소유하고, 이 클래스는 예전 경로와 두 파일 사이의 이전 규칙을 소유한다.

Core Functionality:
- Migrate(): 예전 파일이 더 최근이면 현재 JSON에 재귀 병합해 Roaming 설정으로 저장
- SettingsReadPaths: 더 최근인 파일부터 반환하고 한 파일을 읽지 못하면 다른 파일을 시도하게 함
- LegacySettingsFiles: AppPaths가 설정 초기화 때 예전 파일까지 함께 지울 수 있도록 경로 목록 제공

State Management:
- LegacySettingsFilePath: 0.1.0까지 사용한 Local settings.json 경로
- 파일 수정 시각: 현재 파일과 예전 파일이 함께 있을 때 병합 및 읽기 순서를 정하는 기준

Method Flow:
  AppPaths.PrepareOnStartup() -> Migrate() -> 수정 시각 비교 -> JSON 재귀 병합 -> AppPaths.WriteSettingsFile()
  Settings.Load() -> SettingsReadPaths -> 최신 후보부터 역직렬화

Key Methods:
- Migrate(): 예전 설정이 없거나 현재 설정보다 오래됐으면 건너뛰고, 더 최근일 때만 병합
- MergeJsonObjects(current, legacy): 예전 파일에 있는 값만 덮어쓰고 새 속성은 보존
- ReadSettingsObject(path): JSON 최상위 값이 객체인지 확인해 병합 입력으로 변환

Dependencies:
- AppPaths: 현재 설정 경로와 원자적 파일 저장 제공
- Settings: SettingsReadPaths를 순서대로 읽어 AppSettings 복원
- Logger: 이전 실패를 기록하되 앱 시작은 계속 진행

Design Rationale: SettingsReadPaths는 읽기 후보를 고르지만 예전 경로를 알아야 하고, 이전 실패 때
원본을 복구 후보로 남기는 규칙의 일부다. 예전 경로 결합을 이 클래스에 모으려고 이전 코드와 함께 둔다.

Historical Context: 0.1.0까지 설정을 Velopack 설치 폴더 안에 저장해 앱 제거 시 함께 사라졌다.
현재 버전은 Roaming에 저장하며, 아직 0.1.0을 쓰는 사용자를 위해 이 단방향 이전을 유지한다.

Known Limitations: 파일 수정 시각만으로 어느 설정이 최신인지 판정한다. 두 파일의 시각이 같으면
현재 Roaming 파일을 먼저 읽는 기존 규칙을 유지한다.

Critical Warnings: 예전 파일은 이전 직후 지우지 않는다. 현재 파일 저장이 실패하면
SettingsReadPaths가 예전 원본을 읽어야 한다. 설정 초기화 때 AppPaths.DeleteSettingsFiles()가 함께 지운다.

Last Updated: 2026-08-16 | .NET 8 | 설정 위치 이전 로직 분리
*/
namespace TanukiTarkovMap.Models.Migrations
{
    internal static class SettingsLocationMigration
    {
        /// <summary> 0.1.0까지 설정을 두던 자리 (Velopack 설치 폴더 안) </summary>
        private static string LegacySettingsFilePath => Path.Combine(AppPaths.LocalRoot, "settings.json");

        /// <summary> 설정 초기화가 현재 파일과 함께 지워야 하는 예전 파일 목록 </summary>
        internal static IReadOnlyList<string> LegacySettingsFiles => [LegacySettingsFilePath];

        /// <summary>
        /// 설정을 읽을 후보. 더 최근 파일을 먼저 읽고 실패하면 다른 위치를 시도한다.
        /// 이전에 실패해 두 파일이 함께 남아도 최신 설정을 읽으며, 손상된 한 파일 때문에
        /// 정상인 다른 파일까지 버리고 기본값을 만들지 않는다
        /// </summary>
        internal static IReadOnlyList<string> SettingsReadPaths
        {
            get
            {
                var current = new FileInfo(AppPaths.SettingsFilePath);
                var legacy = new FileInfo(LegacySettingsFilePath);

                if (current.Exists && legacy.Exists)
                {
                    return current.LastWriteTimeUtc >= legacy.LastWriteTimeUtc
                        ? [current.FullName, legacy.FullName]
                        : [legacy.FullName, current.FullName];
                }

                if (current.Exists) return [current.FullName];
                if (legacy.Exists) return [legacy.FullName];
                return [];
            }
        }

        internal static void Migrate()
        {
            try
            {
                var legacy = new FileInfo(LegacySettingsFilePath);
                if (!legacy.Exists) return;

                // 예전 위치를 지우지 않으므로 이전에 실패하면 두 파일이 함께 남을 수 있다.
                // 새 파일보다 예전 파일이 더 최근일 때만 병합해 오래된 값으로 덮지 않는다
                var current = new FileInfo(AppPaths.SettingsFilePath);
                if (current.Exists && current.LastWriteTimeUtc >= legacy.LastWriteTimeUtc) return;

                var merged = current.Exists
                    ? ReadSettingsObject(AppPaths.SettingsFilePath)
                    : new JsonObject();
                var legacySettings = ReadSettingsObject(LegacySettingsFilePath);

                // 예전 파일의 값만 최신 값으로 덮고, 그 파일에 없던 새 속성은 현재 파일에 남긴다.
                // 중첩된 설정도 같은 규칙으로 합쳐 이후에 필드가 늘어도 이전할 수 있게 한다
                MergeJsonObjects(merged, legacySettings);
                AppPaths.WriteSettingsFile(merged.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                Logger.SimpleLog($"[AppPaths] Settings merged from {LegacySettingsFilePath} (newer)");
            }
            catch (Exception ex)
            {
                // SettingsReadPaths가 더 최신인 예전 파일을 먼저 돌려주고, 그 파일까지 읽지 못하면
                // 현재 파일을 다시 시도한다. 실패한 이전 때문에 곧바로 기본값을 만들지 않는다
                Logger.SimpleLog($"[AppPaths] Settings migration failed: {ex.Message}");
            }
        }

        private static JsonObject ReadSettingsObject(string path)
        {
            var root = JsonNode.Parse(File.ReadAllText(path));
            return root as JsonObject
                   ?? throw new InvalidDataException($"설정 파일의 최상위 값이 객체가 아닙니다: {path}");
        }

        private static void MergeJsonObjects(JsonObject current, JsonObject legacy)
        {
            foreach (var (name, legacyValue) in legacy)
            {
                if (legacyValue is JsonObject legacyObject && current[name] is JsonObject currentObject)
                {
                    MergeJsonObjects(currentObject, legacyObject);
                    continue;
                }

                current[name] = legacyValue?.DeepClone();
            }
        }
    }
}
