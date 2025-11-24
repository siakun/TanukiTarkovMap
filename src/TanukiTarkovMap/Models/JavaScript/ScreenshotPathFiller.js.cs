namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// 스크린샷 경로 자동 입력 관련 JavaScript 스크립트
    /// - 웹 페이지의 "Screenshots folder" 입력란에 경로 자동 입력
    /// - Vue/Nuxt 프레임워크 이벤트 트리거
    /// </summary>
    public static class ScreenshotPathFiller
    {
        /// <summary>
        /// 스크린샷 폴더 경로를 자동으로 입력하는 스크립트
        ///
        /// JavaScript 파일 위치: Models/JavaScript/Scripts/screenshot-path-filler.js
        ///
        /// 사용 방법:
        /// string script = ScreenshotPathFiller.AUTO_FILL_SCREENSHOTS_PATH("C:\\Screenshots");
        /// await webView.CoreWebView2.ExecuteScriptAsync(script);
        ///
        /// 실행 절차:
        /// 1. div.mb-5 요소들 중 "Screenshots folder (case sensitive)" 텍스트를 가진 라벨 찾기
        /// 2. 라벨의 부모 요소(mb-15 컨테이너)를 찾기
        /// 3. 부모 요소의 두 번째 자식 요소에서 input 요소 찾기
        /// 4. input.value에 경로 설정
        /// 5. Vue/Nuxt 프레임워크를 위해 input, change, blur 이벤트 발생
        /// 6. 각 단계의 성공/실패 정보를 JSON으로 반환
        /// </summary>
        /// <param name="screenshotPath">스크린샷 폴더 경로 (예: "C:\\Users\\...\\Screenshots")</param>
        /// <returns>경로가 삽입된 JavaScript 코드</returns>
        public static string AUTO_FILL_SCREENSHOTS_PATH(string screenshotPath)
        {
            // JavaScriptLoader.LoadTemplate: {0}에 경로를 넣어서 반환
            return JavaScriptLoader.LoadTemplate("screenshot-path-filler.js", screenshotPath);
        }
    }
}
