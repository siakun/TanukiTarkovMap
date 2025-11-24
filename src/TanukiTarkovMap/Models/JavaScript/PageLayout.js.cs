namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// 페이지 레이아웃 조정 관련 JavaScript 스크립트
    /// - 웹 페이지의 마진과 패딩 제거
    /// - C# WebView2 크기 조정 트리거
    /// </summary>
    public static class PageLayout
    {
        /// <summary>
        /// 웹 페이지의 마진과 패딩을 제거하는 스크립트
        ///
        /// JavaScript 파일 위치: Models/JavaScript/Scripts/page-layout.js
        ///
        /// 실행 절차:
        /// 1. .wrap, .content.wide, .p-relative, .alert-box 클래스 요소들의 마진/패딩을 0으로 설정
        /// 2. body 요소의 마진/패딩도 0으로 설정
        /// 3. 콘솔에 로그 출력
        /// 4. 100ms 후 C#에 'margins-removed' 메시지 전송 (WebView 크기 조정 트리거용)
        /// </summary>
        public static string REMOVE_PAGE_MARGINS_SCRIPT =>
            JavaScriptLoader.Load("page-layout.js");
    }
}
