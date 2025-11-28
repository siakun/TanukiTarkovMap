namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// 웹 요소 제어 관련 JavaScript 스크립트
    ///
    /// tarkov-market.com 웹페이지의 UI 요소 가시성을 제어합니다.
    ///
    /// 동작 원리:
    /// 1. 페이지 로드 시 INIT_SCRIPT를 먼저 실행하여 함수들을 window 객체에 등록
    /// 2. 이후 개별 함수 호출 스크립트(HIDE_HEADER 등)로 필요한 동작 수행
    ///
    /// 숨김 정책:
    /// - 헤더/푸터: 항상 숨김 (복원 불가)
    /// - 패널(좌/우/상단): "UI 요소 숨기기" 체크박스에 따라 토글
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
        public const string RESTORE_UI_ELEMENTS_KEEP_TRANSFORM = "window.restorePanels();";

        /// <summary>
        /// 패널 복원 (헤더/푸터는 숨김 유지) - RESTORE_UI_ELEMENTS_KEEP_TRANSFORM과 동일
        /// </summary>
        public const string RESTORE_ALL_ELEMENTS = "window.restorePanels();";

        /// <summary>
        /// 요소 표시 상태 확인 (디버깅용)
        /// </summary>
        public const string CHECK_ELEMENTS_VISIBILITY_STATUS = "window.checkElementsVisibility();";
    }
}
