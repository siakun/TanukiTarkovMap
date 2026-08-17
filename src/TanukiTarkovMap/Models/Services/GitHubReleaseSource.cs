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
  (full nupkg + releases.{os}.json)이 둘 다 있는 릴리스만 남기며, 자산 크기도 함께 담는다.
  목록이 비어 돌아오면 태그와 피드로 다시 만든다
- ResolvePlanAsync(): 대상에서 뒤로 따라가며 로컬 패키지에 닿는 delta 사슬을 찾아 받을 것을 확정한다
- GetReleaseFeed(): 확정된 자산 목록을 Velopack에 돌려준다
- DownloadReleaseEntry(): 요청된 자산을 스트리밍으로 받으며 진행률을 알린다

State Management:
- _target: 이 소스가 가리키는 릴리스. 생성 후 바뀌지 않는다
- _allVersions: 설치 가능한 전체 릴리스. 사슬을 뒤로 따라갈 때 기준 버전을 찾는 데 쓴다
- _downloadUrls: 사슬이 정해진 뒤 파일명으로 주소를 찾는 표
- _deltaPlanned: delta로 받기로 정해졌는지. full 되돌리기를 한 번만 알리는 데 쓴다
- _cachedFeed: 피드 조회 결과. Velopack이 한 번의 설치 중 여러 번 조회해도 왕복을 반복하지 않는다

Method Flow:
  FetchVersionsAsync -> ReleaseVersion 목록 (설정 화면의 ListBox)
  사용자 선택 -> new GitHubReleaseSource(선택, 전체 목록) -> UpdateManager에 주입
    -> ResolvePlanAsync(로컬 패키지) -> 대상 피드부터 기준을 따라 뒤로 이동
       -> 로컬 패키지에 닿으면 그 경로가 사슬, 못 닿으면 full 1개
    -> DownloadReleaseEntry -> nupkg 다운로드 -> Velopack이 SHA 검증 후 적용

Design Rationale: VelopackAsset을 직접 조립하지 않고 릴리스가 이미 담고 있는 피드 JSON을
파싱한다. Velopack은 받은 패키지의 SHA를 피드 값과 대조하는데, 우리가 조립한 값에는 그 해시가
없어 검증을 통과시킬 방법이 없기 때문이다. delta도 각자 자기 릴리스의 피드에만 해시가 있어
사슬 길이만큼 피드를 받아야 한다.

이 사슬 구성은 배포 구조에서 나온 우회책이다. 릴리스마다 자기 피드를 두는 구조라 피드 하나가
자기 delta와 그 기준까지, 곧 한 단계만 기술한다. 중앙 피드로 옮기면 Velopack이 알아서 사슬을 잇고 이 코드는 필요
없어진다. 판단 근거는 docs/20260816-update-delivery-design.md에 있다.

Critical Warnings: Velopack은 delta로 조립한 결과를 대상 해시로 다시 검증하지 않는다.
기준을 잘못 짚으면 틀린 내용이 조용히 남으므로, 사슬을 이을 때 버전만 보지 말고 해시까지 본다.

Known Limitations: 다운그레이드는 언제나 full이다. delta는 올라가는 방향으로만 만들어진다.
사슬이 끊겼거나 너무 길거나 합이 full에 견줘 크면 full로 떨어진다.

Last Updated: 2026-08-16 | .NET 8 / Velopack 0.0.1298 | 버전 전환에 delta 사슬 적용
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

        /// <summary>
        /// 이 릴리스의 delta nupkg 파일명. delta가 없는 릴리스(첫 릴리스, 생성 실패)는 null.
        ///
        /// 이 delta가 어느 버전에서 출발하는지는 파일명으로 알 수 없고 그 릴리스의 피드에만
        /// 적혀 있다. 대개는 직전 릴리스지만 언제나 그런 것은 아니다. 프리릴리스를 만들 때
        /// 최신 정식 버전을 기준으로 잡는 도구가 흔하다
        /// </summary>
        public string? DeltaFileName { get; init; }

        /// <summary> delta nupkg 다운로드 주소. DeltaFileName이 null이면 함께 null </summary>
        public string? DeltaUrl { get; init; }

        /// <summary> delta nupkg 크기(byte). full과 견줘 사슬을 쓸지 정할 때 쓴다 </summary>
        public long DeltaSize { get; init; }
    }

    /// <summary>
    /// 이번 설치에서 실제로 받을 것. Deltas가 비어 있으면 full 하나만 받는다
    /// </summary>
    /// <param name="TargetFull">대상 버전의 full 패키지</param>
    /// <param name="Deltas">적용 순서대로 정렬된 delta. 사슬을 못 쓰면 빈 배열</param>
    public sealed record InstallPlan(VelopackAsset TargetFull, VelopackAsset[] Deltas);

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

        /// <summary>
        /// 이보다 긴 사슬은 만들지 않는다. Velopack의 UpdateOptions.MaximumDeltasBeforeFallback
        /// 기본값과 같은 값이다. 그쪽은 다운로드 시점에 걸리지만 우리는 그 전에 사슬마다 피드를
        /// 받으므로, 같은 지점에서 미리 끊어야 헛된 왕복이 생기지 않는다
        /// </summary>
        private const int MaximumDeltaChainLength = 10;

        /// <summary>
        /// 태그로 목록을 다시 만들 때 한꺼번에 받을 피드 수.
        /// 태그가 많은 저장소에서 왕복이 한꺼번에 몰리지 않게 묶는다
        /// </summary>
        private const int TagFeedConcurrency = 6;

        /// <summary>
        /// delta 합이 full의 이 분의 1을 넘으면 full로 받는다.
        /// delta를 적용하려면 full 패키지를 통째로 풀고 다시 조립해야 하므로, 전송량이 조금
        /// 줄어드는 정도로는 그 고정 비용을 갚지 못한다
        /// </summary>
        private const int DeltaToFullSizeRatio = 10;

        private readonly ReleaseVersion _target;

        /// <summary> 설치 가능한 전체 릴리스 목록. 사슬을 뒤로 따라갈 때 기준 버전을 찾는 데 쓴다 </summary>
        private readonly IReadOnlyList<ReleaseVersion> _allVersions;

        /// <summary> 사슬이 정해진 뒤 파일명으로 주소를 찾기 위한 표 </summary>
        private readonly Dictionary<string, string> _downloadUrls = new(StringComparer.OrdinalIgnoreCase);

        private VelopackAssetFeed? _cachedFeed;

        /// <summary> delta로 받기로 정해졌는지. full 되돌리기를 한 번만 알리기 위한 표시 </summary>
        private bool _deltaPlanned;

        /// <summary>
        /// delta로 받기로 했다가 Velopack이 full로 되돌렸을 때 알린다.
        /// 화면에 delta 크기가 남아 있으면 진행률 눈금이 뒤로 가는 것처럼 보인다
        /// </summary>
        public event Action<long>? FullFallbackStarted;

        public GitHubReleaseSource(ReleaseVersion target, IReadOnlyList<ReleaseVersion>? allVersions = null)
        {
            _target = target;
            _allVersions = allVersions ?? [];
            _downloadUrls[target.PackageFileName] = target.PackageUrl;
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
        ///
        /// 목록이 비어 돌아오면 태그로 다시 만든다. 2026-08-17 GitHub 장애 때 릴리스 목록
        /// 엔드포인트가 오류 없이 빈 배열을 돌려주어, 되돌리기가 통째로 막히고 화면에는
        /// "설치할 수 있는 버전이 없습니다"만 남았다. 되돌리기는 이 앱의 안전장치이므로
        /// 엔드포인트 하나에 묶어 두지 않는다.
        ///
        /// 조회가 예외로 끝나는 경우(한도 초과, 연결 실패)는 폴백하지 않는다. 태그 조회도 같은
        /// API라 같은 이유로 실패하고, 그 사정은 호출 측이 이미 구분해 알린다
        /// </summary>
        public static async Task<IReadOnlyList<ReleaseVersion>> FetchVersionsAsync(string repoUrl, CancellationToken cancelToken = default)
        {
            var (owner, repo) = ParseRepoUrl(repoUrl);

            var versions = await FetchFromReleaseListAsync(owner, repo, cancelToken);
            if (versions.Count > 0)
            {
                Logger.SimpleLog($"[GitHubReleaseSource] Fetched {versions.Count} installable version(s)");
                return versions;
            }

            Logger.SimpleLog("[GitHubReleaseSource] Release list came back empty, rebuilding from tags");

            versions = await FetchFromTagsAsync(owner, repo, cancelToken);
            Logger.SimpleLog($"[GitHubReleaseSource] Fetched {versions.Count} installable version(s) from tags");
            return versions;
        }

        /// <summary>
        /// 릴리스 목록 엔드포인트로 버전 목록을 만든다 (기본 경로, API 요청 한 번)
        /// </summary>
        private static async Task<List<ReleaseVersion>> FetchFromReleaseListAsync(
            string owner, string repo, CancellationToken cancelToken)
        {
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

                // delta는 없을 수 있다. 첫 릴리스이거나 delta 생성 단계가 실패한 경우다
                var delta = release.Assets.FirstOrDefault(
                    asset => asset.Name.EndsWith("-delta.nupkg", StringComparison.OrdinalIgnoreCase));

                versions.Add(new ReleaseVersion
                {
                    Tag = release.TagName,
                    Version = version,
                    IsPrerelease = release.Prerelease,
                    PackageFileName = package.Name,
                    PackageUrl = package.DownloadUrl,
                    FeedUrl = feed.DownloadUrl,
                    PackageSize = package.Size,
                    DeltaFileName = delta?.Name,
                    DeltaUrl = delta?.DownloadUrl,
                    DeltaSize = delta?.Size ?? 0,
                });
            }

            versions.Sort((left, right) => right.Version.CompareTo(left.Version));
            return versions;
        }

        /// <summary>
        /// 태그와 릴리스 자산으로 버전 목록을 다시 만든다 (목록 엔드포인트 우회).
        ///
        /// 자산 주소는 태그와 파일명으로 정해지므로(releases/download 아래) 자산 목록을 API에
        /// 묻지 않아도 된다. 파일명과 크기는 그 릴리스의 피드에 적혀 있어 피드 하나만 받으면
        /// 채울 수 있고, 그 다운로드는 API가 아니라 시간당 한도와도 무관하다.
        /// 그래서 이 경로가 쓰는 API 요청은 태그 조회 한 번뿐이다
        /// </summary>
        private static async Task<List<ReleaseVersion>> FetchFromTagsAsync(
            string owner, string repo, CancellationToken cancelToken)
        {
            var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/tags?per_page=100";

            var json = await Http.GetStringAsync(apiUrl, cancelToken);
            var tags = JsonSerializer.Deserialize<GitHubTag[]>(json) ?? [];

            using var limiter = new SemaphoreSlim(TagFeedConcurrency);
            var lookups = new List<Task<ReleaseVersion?>>();

            foreach (var tag in tags)
            {
                if (!SemanticVersion.TryParse(tag.Name.TrimStart('v', 'V'), out var version)) continue;

                lookups.Add(LoadVersionFromTagAsync(owner, repo, tag.Name, version, limiter, cancelToken));
            }

            var versions = (await Task.WhenAll(lookups)).OfType<ReleaseVersion>().ToList();
            versions.Sort((left, right) => right.Version.CompareTo(left.Version));
            return versions;
        }

        /// <summary>
        /// 태그 하나의 피드를 받아 릴리스를 복원한다.
        ///
        /// 릴리스가 없는 태그나 Velopack 산출물이 없는 태그는 null이다. 그런 태그가 섞여 있다고
        /// 목록 전체를 버릴 이유가 없으므로 건너뛴 사정만 로그에 남긴다.
        ///
        /// 프리릴리스 여부는 GitHub 릴리스의 표시가 아니라 태그의 시맨틱 버전으로 판정한다.
        /// 이 경로에서는 그 표시를 알 수 없고, 이 저장소는 v0.1.1-beta처럼 버전에 남기고 있다
        /// </summary>
        private static async Task<ReleaseVersion?> LoadVersionFromTagAsync(
            string owner,
            string repo,
            string tag,
            SemanticVersion version,
            SemaphoreSlim limiter,
            CancellationToken cancelToken)
        {
            await limiter.WaitAsync(cancelToken);

            try
            {
                var feedUrl = AssetUrl(owner, repo, tag, FeedFileName);
                var json = await Http.GetStringAsync(feedUrl, cancelToken);
                var feed = VelopackAssetFeed.FromJson(json);

                // 피드에는 delta의 기준이 된 다른 버전의 full도 적혀 있어 버전까지 대조한다
                var package = feed.Assets.FirstOrDefault(
                    asset => asset.Type == VelopackAssetType.Full
                             && asset.Version.CompareTo(version) == 0);

                if (package == null) return null;

                var delta = feed.Assets.FirstOrDefault(
                    asset => asset.Type == VelopackAssetType.Delta
                             && asset.Version.CompareTo(version) == 0);

                return new ReleaseVersion
                {
                    Tag = tag,
                    Version = version,
                    IsPrerelease = version.IsPrerelease,
                    PackageFileName = package.FileName,
                    PackageUrl = AssetUrl(owner, repo, tag, package.FileName),
                    FeedUrl = feedUrl,
                    PackageSize = package.Size,
                    DeltaFileName = delta?.FileName,
                    DeltaUrl = delta == null ? null : AssetUrl(owner, repo, tag, delta.FileName),
                    DeltaSize = delta?.Size ?? 0,
                };
            }
            catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[GitHubReleaseSource] {tag} has no installable release ({ex.Message})");
                return null;
            }
            finally
            {
                limiter.Release();
            }
        }

        /// <summary>
        /// 릴리스 자산의 다운로드 주소. GitHub이 정한 고정 형식이라 API를 거치지 않고 만든다
        /// </summary>
        private static string AssetUrl(string owner, string repo, string tag, string fileName) =>
            $"https://github.com/{owner}/{repo}/releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(fileName)}";

        /// <summary>
        /// 이번 설치에서 실제로 받을 것을 정한다.
        ///
        /// 대상의 full은 반드시 있어야 하므로 그 조회가 실패하면 예외가 나간다. 이전에도 그랬다.
        /// 반면 delta는 없어도 설치할 수 있으므로, 사슬을 찾다 무엇이 잘못되면 예외를 삼키고
        /// 빈 목록을 돌려준다. delta 때문에 되던 설치가 안 되는 일이 없어야 한다
        /// </summary>
        public async Task<InstallPlan> ResolvePlanAsync(
            VelopackAsset? localBase,
            CancellationToken cancelToken = default)
        {
            var targetFeed = await LoadReleaseFeedAsync(_target, cancelToken);
            var targetFull = RequireAsset(targetFeed, _target, _target.PackageFileName, VelopackAssetType.Full);

            if (localBase == null)
            {
                return Finish(targetFull, []);
            }

            try
            {
                var deltas = await WalkChainAsync(targetFeed, targetFull, localBase, cancelToken);
                return Finish(targetFull, deltas);
            }
            catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
            {
                // 사용자가 멈춘 것을 조회 실패로 바꿔 읽으면 설치가 그대로 이어진다.
                // 토큰을 함께 봐야 한다. HttpClient는 자기 Timeout이 지나도 같은 예외를 던지는데,
                // 그것은 조회 실패이므로 full로 넘어가야 한다
                throw;
            }
            catch (Exception ex)
            {
                Logger.SimpleLog($"[GitHubReleaseSource] Delta plan failed ({ex.Message}), using full package");
                return Finish(targetFull, []);
            }
        }

        /// <summary>
        /// 대상에서 뒤로 따라가며 로컬 패키지에 닿는 delta 사슬을 만든다.
        ///
        /// 버전 번호로 사이 릴리스를 훑지 않는다. 릴리스마다 자기 delta의 기준이 피드에 적혀
        /// 있으므로 그것을 따라가야 실제로 붙는 경로가 나온다. 번호로 훑으면 두 가지로 어긋난다.
        /// 사이 릴리스가 지워졌을 때, 그리고 프리릴리스처럼 직전이 아닌 버전을 기준으로 delta가
        /// 만들어졌을 때다. 뒤에서 따라가면 1.0.0에서 1.1.0-beta를 건너뛰고 1.1.0으로 바로
        /// 가는 경로처럼, 번호만 보면 놓치는 지름길도 그대로 잡힌다.
        ///
        /// 기준 대조는 버전과 해시를 함께 본다. 버전만 보면 같은 번호로 만들어진 다른 패키지에
        /// 붙을 수 있는데, Velopack은 delta로 조립한 결과를 대상 해시로 다시 검증하지 않아
        /// 그 오류가 조용히 남는다
        /// </summary>
        private async Task<VelopackAsset[]> WalkChainAsync(
            VelopackAssetFeed targetFeed,
            VelopackAsset targetFull,
            VelopackAsset localBase,
            CancellationToken cancelToken)
        {
            var deltas = new List<VelopackAsset>();
            var current = _target;
            var currentFeed = targetFeed;
            long chainBytes = 0;

            for (var step = 0; step < MaximumDeltaChainLength; step++)
            {
                var delta = currentFeed.Assets.FirstOrDefault(
                    asset => asset.Type == VelopackAssetType.Delta
                             && asset.Version.CompareTo(current.Version) == 0);

                if (delta == null)
                {
                    throw new InvalidOperationException($"릴리스 {current.Tag}에 delta가 없습니다.");
                }

                chainBytes += delta.Size;
                if (chainBytes > targetFull.Size / DeltaToFullSizeRatio)
                {
                    throw new InvalidOperationException(
                        $"delta 합계 {chainBytes / 1024}KB가 full의 {DeltaToFullSizeRatio}분의 1을 넘습니다.");
                }

                var baseAsset = currentFeed.Assets.FirstOrDefault(
                    asset => asset.Type == VelopackAssetType.Full
                             && asset.Version.CompareTo(current.Version) != 0);

                if (baseAsset == null)
                {
                    throw new InvalidOperationException($"릴리스 {current.Tag}의 피드에 delta 기준이 없습니다.");
                }

                if (string.IsNullOrEmpty(current.DeltaUrl))
                {
                    throw new InvalidOperationException($"릴리스 {current.Tag}의 delta 주소를 찾지 못했습니다.");
                }

                deltas.Insert(0, delta);
                _downloadUrls[delta.FileName] = current.DeltaUrl;

                if (SameAsset(baseAsset, localBase))
                {
                    Logger.SimpleLog(
                        $"[GitHubReleaseSource] Delta chain of {deltas.Count} ({chainBytes / 1024}KB vs full {targetFull.Size / 1024}KB)");
                    return [.. deltas];
                }

                // 상한에 닿았으면 다음 피드를 받지 않는다. 어차피 쓰지 못할 조회다
                if (step + 1 >= MaximumDeltaChainLength)
                {
                    throw new InvalidOperationException($"사슬이 {MaximumDeltaChainLength}단계를 넘었습니다.");
                }

                (current, currentFeed) = await FindBaseReleaseAsync(baseAsset, cancelToken);
            }

            throw new InvalidOperationException($"사슬이 {MaximumDeltaChainLength}단계를 넘었습니다.");
        }

        /// <summary>
        /// delta의 기준이 되는 릴리스를 찾는다. 버전이 같은 후보가 여럿일 수 있으므로
        /// 각 후보의 full이 그 기준과 같은 내용인지 확인하고 맞는 것만 고른다.
        ///
        /// 버전만 보고 고르면 사슬 중간에서 다른 패키지로 갈아타게 된다. Velopack이 조립 결과를
        /// 다시 검증하지 않으므로 그 어긋남은 드러나지 않고 설치까지 간다.
        ///
        /// 내용까지 같은 후보가 여럿이면 먼저 찾은 것을 쓰고 되돌아가지 않는다. 그 갈래가 뒤에서
        /// 끊겨도 다른 갈래를 다시 뒤지지 않는다는 뜻이다. 되돌아가려면 갈래마다 delta 목록과
        /// 누적 크기, 주소 표를 따로 들고 다녀야 해서 값이 큰데, 얻는 것은 full 대신 delta를
        /// 쓸 기회 하나뿐이다. 잘못 이어 붙일 위험은 없다. 그리고 이 상황은 서로 다른 릴리스가
        /// 바이트까지 같은 full을 담아야 생긴다
        /// </summary>
        private async Task<(ReleaseVersion Release, VelopackAssetFeed Feed)> FindBaseReleaseAsync(
            VelopackAsset baseAsset,
            CancellationToken cancelToken)
        {
            foreach (var candidate in _allVersions.Where(v => v.Version.CompareTo(baseAsset.Version) == 0))
            {
                VelopackAssetFeed feed;
                try
                {
                    feed = await LoadReleaseFeedAsync(candidate, cancelToken);
                }
                catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // 후보 하나를 읽지 못한 것으로 사슬을 포기하지 않는다
                    Logger.SimpleLog($"[GitHubReleaseSource] {candidate.Tag} feed unreadable ({ex.Message}), trying next");
                    continue;
                }

                var full = feed.Assets.FirstOrDefault(
                    asset => asset.Type == VelopackAssetType.Full
                             && string.Equals(asset.FileName, candidate.PackageFileName, StringComparison.OrdinalIgnoreCase));

                if (full != null && SameAsset(full, baseAsset))
                {
                    return (candidate, feed);
                }

                Logger.SimpleLog($"[GitHubReleaseSource] {candidate.Tag} is v{baseAsset.Version} but its content differs");
            }

            throw new InvalidOperationException($"delta 기준인 {baseAsset.Version}과 같은 내용의 릴리스를 찾지 못했습니다.");
        }

        /// <summary>
        /// 두 자산이 같은 패키지인지 본다. 버전이 같아도 내용이 다를 수 있으므로 해시까지 본다.
        /// SHA256이 없는 옛 피드를 위해 SHA1도 받아들인다
        /// </summary>
        private static bool SameAsset(VelopackAsset left, VelopackAsset right)
        {
            if (left.Version.CompareTo(right.Version) != 0) return false;

            if (!string.IsNullOrEmpty(left.SHA256) && !string.IsNullOrEmpty(right.SHA256))
            {
                return string.Equals(left.SHA256, right.SHA256, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrEmpty(left.SHA1) && !string.IsNullOrEmpty(right.SHA1))
            {
                return string.Equals(left.SHA1, right.SHA1, StringComparison.OrdinalIgnoreCase);
            }

            // 해시를 견줄 수 없으면 같다고 보지 않는다. 틀린 기준에 붙이는 것보다 full이 낫다
            return false;
        }

        private InstallPlan Finish(VelopackAsset targetFull, VelopackAsset[] deltas)
        {
            _cachedFeed = new VelopackAssetFeed { Assets = [targetFull, .. deltas] };
            _deltaPlanned = deltas.Length > 0;
            return new InstallPlan(targetFull, deltas);
        }

        private static async Task<VelopackAssetFeed> LoadReleaseFeedAsync(
            ReleaseVersion release, CancellationToken cancelToken)
        {
            var json = await Http.GetStringAsync(release.FeedUrl, cancelToken);
            return VelopackAssetFeed.FromJson(json);
        }

        /// <summary>
        /// 피드에서 자산 하나를 찾는다.
        ///
        /// 피드에는 delta를 만들 때 기준으로 삼은 버전의 full도 적혀 있는데, 그 파일은 이
        /// 릴리스에 올라가 있지 않아 주소를 보장할 수 없다. 그래서 파일명까지 대조해 이 릴리스가
        /// 실제로 갖고 있는 자산만 남긴다
        /// </summary>
        private static VelopackAsset RequireAsset(
            VelopackAssetFeed feed, ReleaseVersion release, string fileName, VelopackAssetType type)
        {
            var asset = feed.Assets.FirstOrDefault(
                candidate => candidate.Type == type
                             && string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase));

            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"릴리스 {release.Tag}의 피드에서 {fileName} 정보를 찾지 못했습니다.");
            }

            return asset;
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
            var url = ResolveDownloadUrl(releaseEntry.FileName);
            if (url == null)
            {
                throw new InvalidOperationException(
                    $"이 소스가 제공하지 않는 패키지를 요청했습니다: {releaseEntry.FileName}");
            }

            // delta로 받기로 해 놓고 full을 요청받았다면 Velopack이 되돌린 것이다.
            // 화면에 남은 delta 크기를 바로잡지 않으면 진행률이 뒤로 가는 것처럼 보인다
            if (_deltaPlanned
                && string.Equals(releaseEntry.FileName, _target.PackageFileName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.SimpleLog("[GitHubReleaseSource] Velopack fell back to the full package");
                _deltaPlanned = false;
                FullFallbackStarted?.Invoke(releaseEntry.Size);
            }

            Logger.SimpleLog($"[GitHubReleaseSource] Downloading {releaseEntry.FileName}");
            await DownloadFileAsync(url, localFile, progress, cancelToken);
        }

        /// <summary>
        /// 파일명으로 다운로드 주소를 찾는다. 이 소스가 제공하는 것은 대상의 full 하나와
        /// 사슬의 delta들뿐이며, 그 밖의 요청은 주소를 보장할 수 없어 null을 돌려준다
        /// </summary>
        private string? ResolveDownloadUrl(string fileName)
            => _downloadUrls.TryGetValue(fileName, out var url) ? url : null;

        private async Task<VelopackAssetFeed> LoadFeedAsync(CancellationToken cancelToken)
        {
            if (_cachedFeed != null) return _cachedFeed;

            // ResolvePlanAsync를 거치지 않고 Velopack이 먼저 피드를 물어보는 경로를 위한 대비다.
            // 그때는 사슬을 확인할 기준 버전이 없으므로 대상의 full만 내놓는다
            var targetFull = await FindAssetAsync(
                _target, _target.PackageFileName, VelopackAssetType.Full, cancelToken);

            _cachedFeed = new VelopackAssetFeed { Assets = [targetFull] };
            return _cachedFeed;
        }

        /// <summary>
        /// 릴리스의 피드에서 자산 하나를 찾는다.
        ///
        /// 피드에는 delta를 만들 때 기준으로 삼은 버전의 full도 적혀 있는데, 그 파일은 이
        /// 릴리스에 올라가 있지 않아 주소를 보장할 수 없다. 그래서 파일명까지 대조해 이 릴리스가
        /// 실제로 갖고 있는 자산만 남긴다
        /// </summary>
        private static async Task<VelopackAsset> FindAssetAsync(
            ReleaseVersion release,
            string fileName,
            VelopackAssetType type,
            CancellationToken cancelToken)
        {
            var json = await Http.GetStringAsync(release.FeedUrl, cancelToken);
            var feed = VelopackAssetFeed.FromJson(json);

            var asset = feed.Assets.FirstOrDefault(
                candidate => candidate.Type == type
                             && string.Equals(candidate.FileName, fileName, StringComparison.OrdinalIgnoreCase));

            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"릴리스 {release.Tag}의 피드에서 {fileName} 정보를 찾지 못했습니다.");
            }

            return asset;
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

        private sealed class GitHubTag
        {
            [JsonPropertyName("name")] public string Name { get; set; } = "";
        }

        private sealed class GitHubAsset
        {
            [JsonPropertyName("name")] public string Name { get; set; } = "";
            [JsonPropertyName("browser_download_url")] public string DownloadUrl { get; set; } = "";
            [JsonPropertyName("size")] public long Size { get; set; }
        }
    }
}
