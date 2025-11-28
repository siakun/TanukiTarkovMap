/**
 * 웹 요소 제어 스크립트
 *
 * tarkov-market.com 웹페이지의 UI 요소 가시성을 제어합니다.
 *
 * 구조:
 * - 각 함수는 window 객체에 등록되어 C#에서 호출 가능
 * - 헤더/푸터는 항상 숨김 유지
 * - 패널(좌/우/상단)은 "UI 요소 숨기기" 체크박스에 따라 토글
 */

(function() {
    'use strict';

    // ============================================================
    // 헤더 숨기기 (항상 숨김 유지)
    // ============================================================
    window.hideHeader = function() {
        try {
            function hideAllHeaders() {
                // 맵 페이지 header
                var header = document.querySelector('#__nuxt > div > div > header');
                if (header) header.style.display = 'none';

                // 모든 header 태그 숨기기 (pilot 페이지 포함)
                var headers = document.querySelectorAll('header');
                headers.forEach(function(h) { h.style.display = 'none'; });

                // 레이아웃 재계산을 위해 resize 이벤트 발생
                window.dispatchEvent(new Event('resize'));
            }

            // 즉시 실행
            hideAllHeaders();

            // 동적 로딩을 위해 지연 실행
            setTimeout(hideAllHeaders, 300);
            setTimeout(hideAllHeaders, 600);
            setTimeout(hideAllHeaders, 1000);
        } catch (e) {
            console.error('[WebElements] hideHeader error:', e);
        }
    };

    // ============================================================
    // 푸터 숨기기 (항상 숨김 유지)
    // ============================================================
    window.hideFooter = function() {
        try {
            var footerWrap = document.querySelector('#__nuxt > div > div > div.footer-wrap');
            if (footerWrap) footerWrap.style.display = 'none';

            // 레이아웃 재계산을 위해 resize 이벤트 발생
            window.dispatchEvent(new Event('resize'));

            // UI 제거 완료 후 C#에 메시지 전송
            setTimeout(function() {
                try {
                    CefSharp.PostMessage(JSON.stringify({
                        type: 'ui-elements-removed'
                    }));
                } catch (e) {}
            }, 100);
        } catch (e) {
            console.error('[WebElements] hideFooter error:', e);
        }
    };

    // ============================================================
    // 패널 숨기기 (UI 요소 숨기기 체크 시)
    // ============================================================
    window.hidePanelLeft = function() {
        try {
            var panel = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
            if (panel) panel.style.display = 'none';
        } catch (e) {}
    };

    window.hidePanelRight = function() {
        try {
            var panel = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
            if (panel) panel.style.display = 'none';
        } catch (e) {}
    };

    window.hidePanelTop = function() {
        try {
            var panel = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
            if (panel) panel.style.display = 'none';
        } catch (e) {}
    };

    // ============================================================
    // 패널 복원 (UI 요소 숨기기 해제 시) - 헤더/푸터는 복원하지 않음
    // ============================================================
    window.restorePanels = function() {
        try {
            var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
            if (panelLeft) panelLeft.style.display = '';

            var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
            if (panelRight) panelRight.style.display = '';

            var panelTop = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
            if (panelTop) panelTop.style.display = '';

            // 헤더와 푸터는 복원하지 않음 (항상 숨김 유지)
        } catch (e) {
            console.error('[WebElements] restorePanels error:', e);
        }
    };

    // ============================================================
    // 요소 표시 상태 확인 (디버깅용)
    // ============================================================
    window.checkElementsVisibility = function() {
        try {
            var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
            var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
            var header = document.querySelector('#__nuxt > div > div > header');

            return JSON.stringify({
                panelLeftVisible: panelLeft ? (panelLeft.style.display !== 'none') : false,
                panelRightVisible: panelRight ? (panelRight.style.display !== 'none') : false,
                headerVisible: header ? (header.style.display !== 'none') : false
            });
        } catch (e) {
            return '{}';
        }
    };

})();
