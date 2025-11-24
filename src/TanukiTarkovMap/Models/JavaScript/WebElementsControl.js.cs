namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// 웹 요소 제어 관련 JavaScript 스크립트
    /// - 맵 UI 요소(패널, 헤더, 푸터) 숨기기/복원
    /// - "UI 요소 숨기기" 체크박스 기능 지원
    ///
    /// 주의: 이 스크립트는 함수 정의 모음이므로, 각 함수를 개별적으로 호출해야 합니다.
    /// JavaScript 파일 위치: Models/JavaScript/Scripts/web-elements-control.js
    /// </summary>
    public static class WebElementsControl
    {
        // JavaScript 파일을 한 번만 로드하고 캐시
        private static readonly string _script = JavaScriptLoader.Load("web-elements-control.js");

        /// <summary>
        /// UI 요소 복원 (창 크기는 유지)
        /// </summary>
        public const string RESTORE_UI_ELEMENTS_KEEP_TRANSFORM =
            @"try {
                var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
                if (panelLeft) panelLeft.style.display = '';

                var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
                if (panelRight) panelRight.style.display = '';

                var panelTop = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
                if (panelTop) panelTop.style.display = '';

                var header = document.querySelector('#__nuxt > div > div > header');
                if (header) header.style.display = '';

                var footerWrap = document.querySelector('#__nuxt > div > div > div.footer-wrap');
                if (footerWrap) footerWrap.style.display = '';
            } catch (e) {}";

        /// <summary>
        /// 모든 요소 복원
        /// </summary>
        public const string RESTORE_ALL_ELEMENTS =
            @"try {
                var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
                if (panelLeft) panelLeft.style.display = '';

                var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
                if (panelRight) panelRight.style.display = '';

                var panelTop = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
                if (panelTop) panelTop.style.display = '';

                var header = document.querySelector('#__nuxt > div > div > header');
                if (header) header.style.display = '';

                var footerWrap = document.querySelector('#__nuxt > div > div > div.footer-wrap');
                if (footerWrap) footerWrap.style.display = '';
            } catch (e) {}";

        /// <summary>
        /// 왼쪽 패널 숨기기
        /// </summary>
        public const string HIDE_PANEL_LEFT =
            @"try {
                var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
                if (panelLeft) panelLeft.style.display = 'none';
            } catch (e) {}";

        /// <summary>
        /// 오른쪽 패널 숨기기
        /// </summary>
        public const string HIDE_PANEL_RIGHT =
            @"try {
                var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
                if (panelRight) panelRight.style.display = 'none';
            } catch (e) {}";

        /// <summary>
        /// 상단 패널 숨기기
        /// </summary>
        public const string HIDE_PANEL_TOP =
            @"try {
                var panelTop = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
                if (panelTop) panelTop.style.display = 'none';
            } catch (e) {}";

        /// <summary>
        /// 헤더 숨기기
        /// </summary>
        public const string HIDE_HEADER =
            @"try {
                var header = document.querySelector('#__nuxt > div > div > header');
                if (header) header.style.display = 'none';
            } catch (e) {}";

        /// <summary>
        /// 푸터 숨기기 및 C#에 완료 알림
        /// </summary>
        public const string HIDE_FOOTER =
            @"try {
                var footerWrap = document.querySelector('#__nuxt > div > div > div.footer-wrap');
                if (footerWrap) footerWrap.style.display = 'none';

                setTimeout(() => {
                    try {
                        window.chrome.webview.postMessage(JSON.stringify({
                            type: 'ui-elements-removed'
                        }));
                    } catch (e) {}
                }, 100);
            } catch (e) {}";

        /// <summary>
        /// 큰 창일 때 요소 복원
        /// </summary>
        public const string RESTORE_ELEMENTS_FOR_LARGE_SIZE =
            @"try {
                var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
                if (panelLeft) panelLeft.style.display = '';

                var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
                if (panelRight) panelRight.style.display = '';

                var panelTop = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
                if (panelTop) panelTop.style.display = '';

                var header = document.querySelector('#__nuxt > div > div > header');
                if (header) header.style.display = '';
            } catch (e) {}";

        /// <summary>
        /// 작은 창일 때 요소 숨김
        /// </summary>
        public const string HIDE_ELEMENTS_FOR_SMALL_SIZE =
            @"try {
                var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
                if (panelLeft) panelLeft.style.display = 'none';

                var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
                if (panelRight) panelRight.style.display = 'none';

                var panelTop = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
                if (panelTop) panelTop.style.display = 'none';

                var header = document.querySelector('#__nuxt > div > div > header');
                if (header) header.style.display = 'none';
            } catch (e) {}";

        /// <summary>
        /// 요소 표시 상태 확인
        /// </summary>
        public const string CHECK_ELEMENTS_VISIBILITY_STATUS =
            @"(function() {
                try {
                    var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
                    var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
                    var header = document.querySelector('#__nuxt > div > div > header');

                    var status = {
                        panelLeftVisible: panelLeft ? (panelLeft.style.display !== 'none') : false,
                        panelRightVisible: panelRight ? (panelRight.style.display !== 'none') : false,
                        headerVisible: header ? (header.style.display !== 'none') : false
                    };

                    return JSON.stringify(status);
                } catch (e) { return '{}'; }
            })();";
    }
}
