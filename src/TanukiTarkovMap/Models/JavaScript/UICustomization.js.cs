namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// WebView2 UI 커스터마이징 관련 JavaScript 스크립트
    /// - 불필요한 UI 요소 제거
    /// - "Tarkov Pilot" → "Tarkov Client" 브랜딩 변경
    /// - DOM 변경 감시 및 자동 재적용
    /// </summary>
    public static class UICustomization
    {
        /// <summary>
        /// 불필요한 UI 요소를 제거하고 브랜딩을 변경하는 스크립트
        ///
        /// JavaScript 파일 위치: Models/JavaScript/Scripts/ui-customization.js
        ///
        /// 실행 절차:
        /// 1. 페이지 하단 및 상단의 불필요한 요소들을 제거
        /// 2. 브랜드 영역에 "Tarkov Client" 링크 추가
        /// 3. "Tarkov Pilot" 텍스트를 "Tarkov Client"로 일괄 변경
        /// 4. MutationObserver를 설정하여 DOM 변경 시 자동 재적용
        /// 5. 중복 실행 방지를 위한 플래그 관리
        /// </summary>
        public static string REMOVE_UNWANTED_ELEMENTS_SCRIPT =>
            JavaScriptLoader.Load("ui-customization.js");
    }
}
