namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// 웹 요소 제어 관련 JavaScript 스크립트
    ///
    /// tarkov-market.com 웹페이지의 UI 요소 가시성과 추출구 진영 필터를 제어합니다.
    ///
    /// 동작 원리:
    /// 1. 페이지 로드 시 INIT_SCRIPT를 먼저 실행하여 함수들을 window 객체에 등록
    /// 2. 이후 개별 함수 호출 스크립트(HIDE_HEADER 등)로 필요한 동작 수행
    ///
    /// 숨김 정책:
    /// - 헤더/푸터: 항상 숨김 (복원 불가)
    /// - 맵 위 UI: "UI 요소 숨기기" 체크박스에 따라 토글 (맵 레이어만 남기는 화이트리스트)
    ///
    /// 숨김은 요소의 style.display가 아니라 스타일시트 규칙으로 겁니다.
    /// 인라인 방식은 나중에 만들어진 요소를 놓치고, 다른 스크립트가 style.cssText를 대입하면
    /// 함께 지워집니다. 실제로 ui-customization.js가 헤더의 cssText를 덮어써 숨김이 풀렸습니다
    ///
    /// JavaScript 파일 위치: Models/JavaScript/Scripts/web-elements-control.js
    /// </summary>
    public static class WebElementsControl
    {
        /// <summary>
        /// 초기화 스크립트 - 페이지 로드 시 먼저 실행하여 함수들을 등록
        /// </summary>
        public static string INIT_SCRIPT => JavaScriptLoader.Load("web-elements-control.js");

        /// <summary>
        /// 헤더 숨기기 (항상 숨김 유지)
        /// </summary>
        public const string HIDE_HEADER = "window.hideHeader();";

        /// <summary>
        /// 푸터 숨기기 (항상 숨김 유지)
        /// </summary>
        public const string HIDE_FOOTER = "window.hideFooter();";

        /// <summary>
        /// 좌측 패널 숨기기
        /// </summary>
        public const string HIDE_PANEL_LEFT = "window.hidePanelLeft();";

        /// <summary>
        /// 우측 패널 숨기기
        /// </summary>
        public const string HIDE_PANEL_RIGHT = "window.hidePanelRight();";

        /// <summary>
        /// 상단 패널 숨기기
        /// </summary>
        public const string HIDE_PANEL_TOP = "window.hidePanelTop();";

        /// <summary>
        /// 패널 복원 (헤더/푸터는 숨김 유지)
        /// </summary>
        public const string RESTORE_PANELS = "window.restorePanels();";

        /// <summary>
        /// 추출구 진영 전환 (상단바 PMC/SCAV). 스크립트가 Promise를 돌려주므로 결과를 기다려 실행합니다.
        /// 결과는 true 또는 실패 이유 문자열(no-filter-panel, rows-missing, not-applied, not-installed)입니다
        /// </summary>
        public static string SetExtractionFaction(bool isPmc) =>
            $"return window.setExtractionFaction ? window.setExtractionFaction({(isPmc ? "true" : "false")}) : 'not-installed';";
    }
}
