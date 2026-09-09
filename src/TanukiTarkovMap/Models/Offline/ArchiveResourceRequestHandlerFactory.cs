using CefSharp;
using CefSharp.Handler;
using TanukiTarkovMap.Models.Utils;

/**
ArchiveResourceRequestHandlerFactory - 로컬 모드에서 브라우저 요청을 사본으로 응답한다

Purpose: 사이트가 죽어 있어도 맵이 뜨게 한다. 온라인 모드에서는 아무것도 하지 않는다.

Architecture: CefSharp이 자원을 요청할 때마다 이 공장에 묻는다. 로컬 모드가 꺼져 있으면
null을 돌려 평소대로 네트워크를 타게 두고, 켜져 있으면 MapArchive에서 찾은 파일로 응답한다.
사본에 없는 주소는 네트워크로 내보내지 않고 404로 막는다. 반쯤 온라인인 상태를 만들면
"로컬인데 왜 느리지", "왜 어떤 것만 최신이지"를 가려낼 수 없기 때문이다.

State Management:
- LocalModeEnabled: 생성 때 정하고 브라우저 수명 동안 바꾸지 않는다. 모드를 바꿀 때는
  저장 공간과 처리기를 갖춘 새 브라우저를 만든다

Method Flow:
  로컬 처리기 생성 -> MapArchive.Prefetch()를 배경에서 실행 (본문을 미리 읽어 둔다)
  CefSharp 자원 요청 -> GetResourceRequestHandler
    -> 로컬 모드 꺼짐: null (네트워크)
    -> 로컬 모드 켜짐: ArchiveHandler -> MapArchive.Find(주소)
        -> 있으면 그 본문으로 응답 (메모리에서 낸다)
        -> 없으면 404 (네트워크로 새지 않게)

Design Rationale: 커스텀 스킴(local://)을 쓰지 않는다. 사이트의 절대 주소와 라우팅이 그대로
유지되어야 사본이 온라인과 같은 코드로 돌고, 우리 주입 스크립트도 주소로 판정할 수 있다.

Critical Warnings: HasHandlers를 로컬 모드에 따라 바꾸지 않는다. CefSharp이 이 값을 언제
읽는지 보장되지 않아, 껐다 켜는 시점에 가로채기가 통째로 빠질 수 있다. 항상 true로 두고
분기는 GetResourceRequestHandler 안에서 한다.

Last Updated: 2026-08-18 | .NET 8 / CefSharp 141 | 첫 로딩에서 맵이 빠지는 문제 대응
*/
namespace TanukiTarkovMap.Models.Offline
{
    public sealed class ArchiveResourceRequestHandlerFactory : IResourceRequestHandlerFactory
    {
        private readonly MapArchive _archive;

        public ArchiveResourceRequestHandlerFactory(MapArchive archive, bool localModeEnabled)
        {
            _archive = archive;
            LocalModeEnabled = localModeEnabled;
            if (localModeEnabled) Task.Run(_archive.Prefetch);
        }

        /// <summary>
        /// 로컬 모드 여부. 켜져 있는 동안에만 요청을 사본으로 응답한다.
        ///
        /// 켤 때 사본 본문을 배경에서 미리 읽어 둔다. 첫 페이지는 요청 백 개를 한꺼번에 보내는데,
        /// 그때 디스크를 처음 읽으면 응답이 늦어 사이트가 늦게 온 조각을 쓰지 못할 수 있다
        /// </summary>
        // INTENT: 처리 중인 요청이 모드 토글에 따라 다른 출처를 읽지 않게 생성 때 고정한다.
        public bool LocalModeEnabled { get; }

        /// <summary> 위 Critical Warnings 참고. 언제나 true로 둔다 </summary>
        public bool HasHandlers => true;

        public IResourceRequestHandler? GetResourceRequestHandler(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            bool isNavigation,
            bool isDownload,
            string requestInitiator,
            ref bool disableDefaultHandling)
        {
            if (!LocalModeEnabled) return null;

            return new ArchiveHandler(_archive);
        }

        private sealed class ArchiveHandler : ResourceRequestHandler
        {
            private readonly MapArchive _archive;

            public ArchiveHandler(MapArchive archive)
            {
                _archive = archive;
            }

            protected override IResourceHandler? GetResourceHandler(
                IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request)
            {
                var entry = _archive.Find(request.Url);

                if (entry != null)
                {
                    var body = _archive.ReadBody(entry);

                    if (body is { Length: > 0 })
                    {
                        return ResourceHandler.FromByteArray(body, entry.MimeType);
                    }

                    // 사이트에 원래 빈 파일이 있다(예: 내용이 없는 css 조각). 그때는 빈 200으로
                    // 그대로 돌려준다. CefSharp의 FromByteArray는 길이 0인 배열에 예외를 던지므로
                    // 여기서는 본문 없는 응답을 만든다
                    if (body != null)
                    {
                        return new ResourceHandler
                        {
                            StatusCode = 200,
                            MimeType = entry.MimeType,
                        };
                    }

                    Logger.SimpleLog($"[MapArchive] Body read failed: {request.Url}");
                }
                else
                {
                    Logger.SimpleLog($"[MapArchive] Not in archive: {request.Url}");
                }

                var missing = new ResourceHandler
                {
                    StatusCode = 404,
                    MimeType = "text/plain",
                };
                return missing;
            }
        }
    }
}
