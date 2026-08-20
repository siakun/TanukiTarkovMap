using System.IO;
using System.Text.Json;
using TanukiTarkovMap.Models.Utils;

/**
MapArchive - 오프라인 맵 사본에서 주소에 해당하는 파일을 찾아 준다

Purpose: 사이트가 죽어도 맵을 볼 수 있게 한다. 2026-08-17 GitHub과 tarkov-market 양쪽에서
겪었듯이, 이 앱은 남의 서버가 살아 있어야만 쓸모가 있는 상태였다.

Architecture: tools/archive-maps.mjs가 맵 페이지를 실제 브라우저로 열어 받은 응답을 그대로
저장해 둔다. 이 클래스는 그 저장분을 읽어 "이 주소를 요청하면 이 파일과 MIME"을 돌려주고,
ArchiveResourceRequestHandlerFactory가 그 답으로 브라우저 요청에 응답한다. 사이트 코드가
무엇을 필요로 하는지 우리가 알 필요가 없다는 것이 이 구조의 요점이다.

저장 구조 (도구가 만든다):
  archive/blobs/<sha1>       응답 본문. 같은 내용은 맵이 달라도 한 번만 저장된다
  archive/maps/<맵ID>.json   { 주소: { blob, mime, status } }
  archive/manifest.json      만든 시각과 맵 목록

State Management:
- _entries: 주소에서 사본 항목으로 가는 표. 모든 맵의 색인을 한 번에 담는다.
  맵 12개를 합쳐도 항목이 2천 개 아래라 나눠 담을 이유가 없다
- IsAvailable: 사본이 실제로 있는지. 없으면 로컬 모드를 켤 수 없다

Method Flow:
  Load() -> manifest 확인 -> maps/*.json 병합 -> _entries
  Find(url) -> 정확히 일치하는 항목 -> 없으면 질의 문자열을 뗀 주소로 한 번 더

Design Rationale: 주소를 그대로 열쇠로 쓴다. 사이트의 절대 주소를 그대로 두고 요청만
가로채므로, 사본을 위해 페이지를 고칠 필요가 없고 온라인과 같은 코드가 돈다.

Known Limitations: 사본은 만든 시점의 사이트다. 퀘스트와 시세는 그 시점 값으로 남고,
사이트가 구조를 바꾸면 도구를 다시 돌려 사본을 갱신해야 한다.

Last Updated: 2026-08-18 | .NET 8 / CefSharp 141 | 오프라인 맵 도입
*/
namespace TanukiTarkovMap.Models.Offline
{
    /// <summary>
    /// 사본에 담긴 응답 하나
    /// </summary>
    /// <param name="FilePath">본문이 담긴 blob 파일 경로</param>
    /// <param name="MimeType">저장할 때 기록한 MIME</param>
    public sealed record ArchiveEntry(string FilePath, string MimeType);

    public sealed class MapArchive
    {
        /// <summary> 실행 파일 옆의 사본 폴더. 배포에 함께 들어간다 </summary>
        private static string ArchiveFolder =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "archive");

        private readonly Dictionary<string, ArchiveEntry> _entries =
            new(StringComparer.OrdinalIgnoreCase);

        private bool _loaded;

        /// <summary>
        /// 도구가 만든 색인은 소문자 키(blob, mime)를 쓰므로 이름 대소문자를 가리지 않는다
        /// </summary>
        private static readonly JsonSerializerOptions IndexJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// 사본을 쓸 수 있는지 여부. 폴더가 없거나 비어 있으면 false
        /// </summary>
        public bool IsAvailable
        {
            get
            {
                Load();
                return _entries.Count > 0;
            }
        }

        /// <summary> 사본에 담긴 응답 수 (설정 화면 표시용) </summary>
        public int EntryCount
        {
            get
            {
                Load();
                return _entries.Count;
            }
        }

        /// <summary> 사본을 만든 시각. 알 수 없으면 null (설정 화면 표시용) </summary>
        public DateTime? CreatedAt { get; private set; }

        /// <summary>
        /// 주소에 해당하는 사본을 찾는다. 없으면 null
        /// </summary>
        public ArchiveEntry? Find(string url)
        {
            Load();

            if (string.IsNullOrEmpty(url)) return null;

            if (_entries.TryGetValue(url, out var entry)) return entry;

            // 질의 문자열이 매번 달라지는 요청이 있다 (예: 퀘스트 목록의 hash).
            // 같은 자원을 가리키므로 질의를 뗀 주소로 한 번 더 찾는다
            var queryIndex = url.IndexOf('?');
            if (queryIndex > 0 && _entries.TryGetValue(url[..queryIndex], out var withoutQuery))
            {
                return withoutQuery;
            }

            return null;
        }

        /// <summary>
        /// 색인을 한 번만 읽어 표를 만든다. 실패하면 빈 표로 남아 로컬 모드가 꺼진 것과 같아진다
        /// </summary>
        private void Load()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                var manifestPath = Path.Combine(ArchiveFolder, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    Logger.SimpleLog($"[MapArchive] No archive at {ArchiveFolder}");
                    return;
                }

                using (var manifestStream = File.OpenRead(manifestPath))
                {
                    var manifest = JsonSerializer.Deserialize<ArchiveManifest>(manifestStream, IndexJsonOptions);
                    CreatedAt = manifest?.CreatedAt;
                }

                var mapsFolder = Path.Combine(ArchiveFolder, "maps");
                if (!Directory.Exists(mapsFolder)) return;

                foreach (var indexPath in Directory.EnumerateFiles(mapsFolder, "*.json"))
                {
                    using var indexStream = File.OpenRead(indexPath);
                    var index = JsonSerializer.Deserialize<Dictionary<string, ArchiveIndexEntry>>(indexStream, IndexJsonOptions);
                    if (index == null) continue;

                    foreach (var (url, entry) in index)
                    {
                        if (string.IsNullOrEmpty(entry.Blob)) continue;

                        var blobPath = Path.Combine(ArchiveFolder, "blobs", entry.Blob);
                        if (!File.Exists(blobPath)) continue;

                        // 맵마다 같은 주소가 나오지만 내용은 같으므로 처음 것만 담는다
                        _entries.TryAdd(url, new ArchiveEntry(blobPath, entry.Mime ?? "application/octet-stream"));
                    }
                }

                Logger.SimpleLog($"[MapArchive] Loaded {_entries.Count} archived response(s), created {CreatedAt:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[MapArchive] Load failed: {ex.Message}");
            }
        }

        private sealed class ArchiveManifest
        {
            public DateTime? CreatedAt { get; set; }
        }

        private sealed class ArchiveIndexEntry
        {
            public string? Blob { get; set; }
            public string? Mime { get; set; }
        }
    }
}
