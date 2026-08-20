namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// 페이지가 낸 오류와 맵이 그려졌는지를 앱으로 보내는 스크립트
    ///
    /// 왜 이 방식인가:
    /// 앱 안 브라우저의 콘솔은 사람이 F12로 열어야 보이므로, 사용자가 겪은 렌더 실패의 원인이
    /// 로그에 아무것도 남지 않았다. 페이지 쪽에서 오류를 잡아 CefSharp.PostMessage로 보내면
    /// 앱 로그에 남아, 다시 같은 증상이 나와도 무엇이 실패했는지 사후에 확인할 수 있다.
    ///
    /// 넣는 시점이 다른 스크립트와 다르다:
    /// 다른 스크립트는 FrameLoadEnd에 넣지만 이 스크립트는 FrameLoadStart에 넣는다.
    /// 로딩 중에 난 실패를 잡아야 하므로 자원을 받기 전에 들어가 있어야 한다.
    ///
    /// JavaScript 파일 위치: Models/JavaScript/Scripts/page-health.js
    /// </summary>
    public static class PageHealth
    {
        /// <summary>
        /// 오류 보고와 맵 확인을 등록하는 스크립트
        /// </summary>
        public static string INIT_SCRIPT => JavaScriptLoader.Load("page-health.js");
    }
}
