using System.IO;
using System.Text.Json;
using CefSharp;
using CefSharp.SchemeHandler;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Models.Offline
{
    /**
    LocalViewer - Local 모드의 자체 미니맵 주소와 그 주소를 응답하는 브라우저 저장 공간

    Purpose: 앱과 함께 배포한 미니맵(viewer/)과 지도 리소스(resources/)를 사이트 런타임 없이 CEF에서 연다.
    Architecture: 빌드가 viewer/와 resources/를 실행 파일 옆 LocalMap 폴더로 복사한다. Local 브라우저 전용
    메모리 RequestContext에 CefSharp의 FolderSchemeHandlerFactory를 등록해 https://tanuki-map.local/을
    그 폴더로 응답하게 한다. Online 브라우저의 프로필에는 등록하지 않는다.

    Core Functionality:
    - PageUrl(mapName, language): 맵의 미니맵 주소. mapName은 MapInfo.Name(리소스 폴더 이름)이고,
      language는 미니맵이 로딩과 오류 안내를 처음부터 그 언어로 그리도록 넘기는 화면 언어 코드다
    - MapName(address): 미니맵 주소에서 맵 이름을 되읽는다. 미니맵 주소가 아니면 null
    - CreateRequestContext(): Local 브라우저가 쓸 저장 공간을 만들고 파일 응답을 등록한다
    - ShowScreenshot/IsRendered/SetFaction/SetControlsVisible/SetLanguage: 미니맵의 window.tanukiViewer 호출문

    Dependencies:
    - FolderSchemeHandlerFactory: 경로가 폴더 밖으로 나가는 요청을 막고 확장자로 MIME을 정한다
    - viewer/index.html: 미니맵 페이지. CSP로 같은 주소 밖의 요청을 막는다

    Design Rationale: 파일 응답은 CefSharp가 제공하는 표준 처리기로 충분하다. 실행 중에 리소스를 다시
    검증하지 않는다. 리소스와 미니맵은 CI와 릴리스 빌드에서 tools/resource-bundle.mjs check와
    tools/verify-viewer.mjs로 검사하고, 실행 중에 읽지 못하면 미니맵이 오류를 화면에 표시한다.
    사이트 주소처럼 https를 써서 미니맵이 ES 모듈과 fetch를 그대로 사용한다.

    Historical Context: 0.3.x의 Local은 사이트 응답 사본(archive/)을 요청 가로채기로 돌려주며 사이트
    런타임을 그대로 실행했다. 사이트가 위치 입력 경로를 바꾸면 사본을 새로 받을 때 함께 깨지는 구조라
    리소스만 받아 앱이 그리는 방식으로 바꿨다. 이유와 대안은 docs/20260821-local-viewer-design.md에 있다.

    Last Updated: 2026-10-08 | .NET 8.0 / CefSharp 141.0.110 | By 자체 미니맵 통합, 안내 문구 언어 전달
    */
    public static class LocalViewer
    {
        public const string Host = "tanuki-map.local";

        /// <summary>실행 파일 옆에서 미니맵과 리소스를 담는 폴더</summary>
        public static string Root => Path.Combine(AppContext.BaseDirectory, "LocalMap");

        public static string PageUrl(string mapName, string language) =>
            $"https://{Host}/viewer/index.html?map={Uri.EscapeDataString(mapName)}&lang={Uri.EscapeDataString(language)}";

        public static string? MapName(string? address)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps || uri.Host != Host
                || uri.AbsolutePath != "/viewer/index.html") return null;

            foreach (var part in uri.Query.TrimStart('?').Split('&'))
            {
                if (part.StartsWith("map=", StringComparison.Ordinal))
                    return Uri.UnescapeDataString(part[4..]);
            }
            return null;
        }

        public static IRequestContext CreateRequestContext()
        {
            // RequestContext가 전달받은 settings의 해제도 맡는다.
            var context = new RequestContext(new RequestContextSettings { CachePath = string.Empty });

            // FolderSchemeHandlerFactory는 폴더가 없으면 생성자에서 예외를 던진다. 설치가 깨져 폴더가
            // 없을 때 앱을 멈추지 않고, 미니맵 주소가 열리지 않는 오류 화면으로 남게 한다.
            if (Directory.Exists(Root))
                context.RegisterSchemeHandlerFactory("https", Host, new FolderSchemeHandlerFactory(Root, hostName: Host));
            else
                Logger.SimpleLog($"[LocalViewer] Viewer folder missing: {Root}");
            return context;
        }

        // 미니맵의 window.tanukiViewer 호출문. Online의 PilotBridge 호출과 같은 결과(bool)를 돌려준다.
        // 파일명에는 공백, 쉼표, 괄호가 들어가므로 JSON 문자열로 감싼다.
        public static string ShowScreenshot(string filename) =>
            $"window.tanukiViewer?.showScreenshot({JsonSerializer.Serialize(filename)}) === true;";

        public static string IsRendered(string filename) =>
            $"window.tanukiViewer?.isRendered({JsonSerializer.Serialize(filename)}) === true;";

        public const string STATUS_SCRIPT = "window.tanukiViewer?.status() ?? 'viewer-unavailable';";

        public static string SetFaction(bool isPmc) =>
            $"window.tanukiViewer?.setFaction({(isPmc ? "true" : "false")}) === true;";

        public static string SetControlsVisible(bool visible) =>
            $"window.tanukiViewer?.setControlsVisible({(visible ? "true" : "false")}) === true;";

        public static string SetLanguage(string language) =>
            $"window.tanukiViewer?.setLanguage({JsonSerializer.Serialize(language)}) === true;";
    }
}
