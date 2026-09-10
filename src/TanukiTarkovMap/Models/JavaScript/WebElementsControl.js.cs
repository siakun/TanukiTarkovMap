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
    /// - 로컬 모드: 체크박스와 무관하게 지도, Levels, 추출구와 위치/방향만 유지
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
        /// 로컬 모드 코어 화면과 PMC 추출구만 활성화
        /// </summary>
        public const string ENABLE_LOCAL_MODE_PMC = "window.setLocalMapMode(true, true);";

        /// <summary>
        /// 로컬 모드 코어 화면과 SCAV 추출구만 활성화
        /// </summary>
        public const string ENABLE_LOCAL_MODE_SCAV = "window.setLocalMapMode(true, false);";

        /// <summary>
        /// 로컬 모드 표시를 끄고 온라인 마커 선택을 복원
        /// </summary>
        public const string DISABLE_LOCAL_MODE = "window.setLocalMapMode(false, true);";

        /// <summary>
        /// PMC Extraction 필터 클릭
        /// </summary>
        public const string CLICK_PMC_EXTRACTION = "window.clickPmcExtraction();";

        /// <summary>
        /// SCAV Extraction 필터 클릭
        /// </summary>
        public const string CLICK_SCAV_EXTRACTION = "window.clickScavExtraction();";
    }
}
