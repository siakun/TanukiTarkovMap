using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Versioning;
using TanukiTarkovMap.Models.Utils;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

/**
GitHubReleaseSource - GitHub 릴리스 하나를 Velopack 설치 대상으로 노출

Purpose: 사용자가 목록에서 고른 임의 버전을 설치할 수 있게 한다. 다운그레이드가 이 클래스의
존재 이유다.

Architecture: Velopack이 기본 제공하는 GithubSource는 "최신 릴리스 하나에 모든 패키지가 모여
있다"를 전제로 한다. 최신 릴리스의 releases.{os}.json만 읽고, 다운로드 URL도 그 릴리스 안에서만
찾는다. 이 저장소는 태그마다 자기 버전의 nupkg만 올리므로(전체 패키지가 254MB라 누적이 불가능),
GithubSource로는 두 버전(최신과 그 직전)까지만 보이고 그보다 오래된 버전은 설치할 수 없다.
그래서 릴리스 목록은 GitHub API로 직접 조회하고, 고른 릴리스 하나에 고정된 IUpdateSource를
만들어 Velopack의 다운로드/검증/적용 절차에 태운다.

Core Functionality:
- FetchVersionsAsync(): GitHub Releases API로 설치 가능한 버전 목록을 만든다. Velopack 산출물
  (full nupkg + releases.{os}.json)이 둘 다 있는 릴리스만 남긴다
- GetReleaseFeed(): 고정된 릴리스의 releases.{os}.json을 받아 그 릴리스에 실제로 올라간
  full 패키지 하나만 남긴 피드를 돌려준다
- DownloadReleaseEntry(): 그 패키지를 스트리밍으로 받으며 진행률을 알린다

State Management:
- _target: 이 소스가 가리키는 릴리스. 생성 후 바뀌지 않는다
- _cachedFeed: 피드 조회 결과. Velopack이 한 번의 설치 중 여러 번 조회해도 왕복을 반복하지 않는다

Method Flow:
  FetchVersionsAsync -> ReleaseVersion 목록 (설정 화면의 ListBox)
  사용자 선택 -> new GitHubReleaseSource(선택) -> UpdateManager에 주입
    -> GetReleaseFeed -> releases.{os}.json 파싱 -> 대상 full 패키지 1개
    -> DownloadReleaseEntry -> nupkg 다운로드 -> Velopack이 SHA 검증 후 적용

Design Rationale: VelopackAsset을 직접 조립하지 않고 릴리스가 이미 담고 있는 피드 JSON을
파싱한다. Velopack은 받은 패키지의 SHA를 피드 값과 대조하는데, 우리가 조립한 값에는 그 해시가
없어 검증을 통과시킬 방법이 없기 때문이다.

Known Limitations: 버전 전환은 항상 full 패키지를 받는다(254MB). delta는 직전 버전에서 최신으로
가는 경로에만 존재하므로 임의 버전 이동에는 쓸 수 없고, Velopack도 다운그레이드에는 full만
허용한다. 자동 업데이트는 GithubSource를 그대로 쓰므로 delta 경로가 유지된다.

Last Updated: 2026-08-14 | .NET 8 / Velopack 0.0.1298 | 버전 전환 기능 도입
*/
namespace TanukiTarkovMap.Models.Services
{
    /// <summary>
    /// 설치할 수 있는 릴리스 하나의 정보 (설정 화면 버전 목록의 항목)
    /// </summary>
    public sealed class ReleaseVersion
    {
        /// <summary> GitHub 릴리스 태그 (예: v0.1.0) </summary>
        public required string Tag { get; init; }

        /// <summary> 태그에서 v를 뗀 시맨틱 버전. 정렬과 현재 버전 비교에 쓴다 </summary>
        public required SemanticVersion Version { get; init; }

        /// <summary> GitHub에서 프리릴리스로 표시한 릴리스인지 여부 </summary>
        public required bool IsPrerelease { get; init; }

        /// <summary> 이 릴리스의 full nupkg 파일명 (피드에서 대상 패키지를 가려낼 때 쓴다) </summary>
        public required string PackageFileName { get; init; }

        /// <summary> full nupkg 다운로드 주소 </summary>
        public required string PackageUrl { get; init; }

        /// <summary> releases.{os}.json 다운로드 주소 (패키지 체크섬의 출처) </summary>
        public required string FeedUrl { get; init; }

        /// <summary> full nupkg 크기(byte). 진행률을 MB로 환산할 때 쓴다 </summary>
        public required long PackageSize { get; init; }
    }

    public sealed class GitHubReleaseSource : IUpdateSource
    {
        /// <summary>
        /// GitHub API와 릴리스 자산 다운로드에 함께 쓰는 클라이언트.
        /// 요청마다 새로 만들면 소켓이 고갈되므로 하나를 공유한다
        /// </summary>
        private static readonly HttpClient Http = CreateHttpClient();

        /// <summary> Velopack이 릴리스에 함께 올리는 피드 파일명 (예: releases.win.json) </summary>
        private static string FeedFileName =>
            $"releases.{VelopackRuntimeInfo.GetOsShortName(VelopackRuntimeInfo.SystemOs)}.json";

        /// <summary>
        /// 내려받는 도중 이만큼 한 바이트도 오지 않으면 멈춘 것으로 보고 중단한다.
        /// HttpClient.Timeout이 본문에는 걸리지 않아, 이것이 없으면 연결이 조용히 끊겼을 때
        /// 설치가 끝나지 않는 상태로 남는다
        /// </summary>
        private static readonly TimeSpan StallTimeout = TimeSpan.FromMinutes(2);

        private readonly ReleaseVersion _target;
        private VelopackAssetFeed? _cachedFeed;

        public GitHubReleaseSource(ReleaseVersion target)
        {
            _target = target;
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();

            // GitHub API는 User-Agent가 없는 요청을 403으로 거절한다
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TanukiTarkovMap", "1.0"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            // ResponseHeadersRead로 받으면 이 제한은 응답 헤더까지만 걸린다.
            // 본문이 아무리 길어도 여기서 끊기지 않으므로 헤더를 기다리는 시간만 넉넉히 둔다.
            // 내려받는 도중 멈추는 것은 StallTimeout이 따로 잡는다
            client.Timeout = TimeSpan.FromMinutes(2);
            return client;
        }

        /// <summary>
        /// 저장소의 릴리스를 조회해 설치할 수 있는 버전 목록을 최신 순으로 돌려준다.
        /// 네트워크 실패는 호출자가 처리한다
        /// </summary>
        public static async Task<IReadOnlyList<ReleaseVersion>> FetchVersionsAsync(string repoUrl, CancellationToken cancelToken = default)
        {
            var (owner, repo) = ParseRepoUrl(repoUrl);
            var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases?per_page=100";

            var json = await Http.GetStringAsync(apiUrl, cancelToken);
            var releases = JsonSerializer.Deserialize<GitHubRelease[]>(json) ?? [];

            var versions = new List<ReleaseVersion>();
            foreach (var release in releases)
            {
                if (release.Draft) continue;

                var package = release.Assets.FirstOrDefault(
                    asset => asset.Name.EndsWith("-full.nupkg", StringComparison.OrdinalIgnoreCase));
                var feed = release.Assets.FirstOrDefault(
                    asset => string.Equals(asset.Name, FeedFileName, StringComparison.OrdinalIgnoreCase));

                // Velopack 산출물이 갖춰지지 않은 릴리스는 설치 대상이 될 수 없다
                if (package == null || feed == null) continue;

                if (!SemanticVersion.TryParse(release.TagName.TrimStart('v', 'V'), out var version)) continue;

                versions.Add(new ReleaseVersion
                {
                    Tag = release.TagName,
                    Version = version,
                    IsPrerelease = release.Prerelease,
                    PackageFileName = package.Name,
                    PackageUrl = package.DownloadUrl,
                    FeedUrl = feed.DownloadUrl,
                    PackageSize = package.Size,
                });
            }

            versions.Sort((left, right) => right.Version.CompareTo(left.Version));
            Logger.SimpleLog($"[GitHubReleaseSource] Fetched {versions.Count} installable version(s)");
            return versions;
        }

        /// <summary>
        /// 이 소스가 가리키는 릴리스의 설치 대상 패키지.
        /// Velopack에 넘길 UpdateInfo를 만들 때 쓴다
        /// </summary>
        public async Task<VelopackAsset> GetTargetAssetAsync(CancellationToken cancelToken = default)
        {
            var feed = await LoadFeedAsync(cancelToken);
            return feed.Assets[0];
        }

        public Task<VelopackAssetFeed> GetReleaseFeed(
            IVelopackLogger logger,
            string? appId,
            string channel,
            Guid? stagingId = null,
            VelopackAsset? latestLocalRelease = null)
        {
            // 이 소스는 릴리스 하나에 고정돼 있어 채널과 스테이징 인자가 대상을 바꾸지 않는다.
            // 이 시그니처에는 취소 토큰이 없으므로 피드 조회는 HttpClient.Timeout에만 기댄다
            return LoadFeedAsync(CancellationToken.None);
        }

        public async Task DownloadReleaseEntry(
            IVelopackLogger logger,
            VelopackAsset releaseEntry,
            string localFile,
            Action<int> progress,
            CancellationToken cancelToken = default)
        {
            if (!string.Equals(releaseEntry.FileName, _target.PackageFileName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"릴리스 {_target.Tag}에 없는 패키지를 요청했습니다: {releaseEntry.FileName}");
            }

            Logger.SimpleLog($"[GitHubReleaseSource] Downloading {releaseEntry.FileName} from {_target.Tag}");
            await DownloadFileAsync(_target.PackageUrl, localFile, progress, cancelToken);
        }

        private async Task<VelopackAssetFeed> LoadFeedAsync(CancellationToken cancelToken)
        {
            if (_cachedFeed != null) return _cachedFeed;

            var json = await Http.GetStringAsync(_target.FeedUrl, cancelToken);
            var feed = VelopackAssetFeed.FromJson(json);

            // 릴리스의 피드에는 delta를 만들려고 함께 넣은 직전 버전도 들어 있다.
            // 이 릴리스에 실제로 올라간 파일만 남겨야 다운로드 주소를 보장할 수 있다
            var assets = feed.Assets
                .Where(asset => asset.Type == VelopackAssetType.Full)
                .Where(asset => string.Equals(asset.FileName, _target.PackageFileName, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (assets.Length == 0)
            {
                throw new InvalidOperationException(
                    $"릴리스 {_target.Tag}의 피드에서 {_target.PackageFileName} 정보를 찾지 못했습니다.");
            }

            _cachedFeed = new VelopackAssetFeed { Assets = assets };
            return _cachedFeed;
        }

        private static async Task DownloadFileAsync(string url, string localFile, Action<int>? progress, CancellationToken cancelToken)
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancelToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0L;

            using var source = await response.Content.ReadAsStreamAsync(cancelToken);
            using var destination = File.Create(localFile);

            // 한 번 읽을 때마다 시한을 다시 건다. 데이터가 계속 오면 시한도 계속 밀리고,
            // 조용히 멈추면 StallTimeout 뒤에 취소되어 설치가 끝나지 않는 상태로 남지 않는다
            using var stallWatch = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
            stallWatch.CancelAfter(StallTimeout);

            var buffer = new byte[81920];
            long receivedBytes = 0;
            var lastPercent = -1;
            int read;

            while ((read = await source.ReadAsync(buffer, stallWatch.Token)) > 0)
            {
                stallWatch.CancelAfter(StallTimeout);

                await destination.WriteAsync(buffer.AsMemory(0, read), cancelToken);
                receivedBytes += read;

                if (totalBytes <= 0) continue;

                // 254MB를 80KB씩 받으면 3천 번 넘게 도달하므로 값이 바뀔 때만 알린다
                var percent = (int)(receivedBytes * 100 / totalBytes);
                if (percent == lastPercent) continue;

                lastPercent = percent;
                progress?.Invoke(percent);
            }
        }

        private static (string Owner, string Repo) ParseRepoUrl(string repoUrl)
        {
            var segments = new Uri(repoUrl).AbsolutePath.Trim('/').Split('/');
            if (segments.Length < 2)
            {
                throw new ArgumentException($"GitHub 저장소 주소가 아닙니다: {repoUrl}", nameof(repoUrl));
            }

            return (segments[0], segments[1]);
        }

        /// <summary> GitHub Releases API 응답에서 필요한 항목만 받는 모델 </summary>
        private sealed class GitHubRelease
        {
            [JsonPropertyName("tag_name")] public string TagName { get; set; } = "";
            [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
            [JsonPropertyName("draft")] public bool Draft { get; set; }
            [JsonPropertyName("assets")] public GitHubAsset[] Assets { get; set; } = [];
        }

        private sealed class GitHubAsset
        {
            [JsonPropertyName("name")] public string Name { get; set; } = "";
            [JsonPropertyName("browser_download_url")] public string DownloadUrl { get; set; } = "";
            [JsonPropertyName("size")] public long Size { get; set; }
        }
    }
}
