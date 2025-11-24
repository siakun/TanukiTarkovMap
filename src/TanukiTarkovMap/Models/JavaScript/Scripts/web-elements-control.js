/**
 * 웹 요소 제어 스크립트
 *
 * 이 파일은 함수가 아닌 "스크립트 조각"들의 모음입니다.
 * C#에서 필요한 스크립트를 선택해서 실행합니다.
 *
 * 주의: 이 파일은 직접 실행되지 않고, C#에서 개별 함수를 호출합니다.
 */

// ============================================================
// UI 요소 복원 (창 크기는 유지)
// ============================================================
// 사용법: C#에서 이 함수를 문자열로 가져와서 ExecuteScriptAsync
var restoreUIElementsKeepTransform = function() {
    try {
        // 왼쪽 패널 복원
        var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
        if (panelLeft) {
            panelLeft.style.display = '';  // 빈 문자열 = 원래 값으로 복원
        }

        // 오른쪽 패널 복원
        var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
        if (panelRight) {
            panelRight.style.display = '';
        }

        // 상단 패널 복원
        var panelTop = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
        if (panelTop) {
            panelTop.style.display = '';
        }

        // 헤더 복원
        var header = document.querySelector('#__nuxt > div > div > header');
        if (header) {
            header.style.display = '';
        }

        // 푸터 복원
        var footerWrap = document.querySelector('#__nuxt > div > div > div.footer-wrap');
        if (footerWrap) {
            footerWrap.style.display = '';
        }
    } catch (e) {
        // 에러 무시
    }
};

// ============================================================
// 모든 요소 복원 (일반 모드)
// ============================================================
var restoreAllElements = function() {
    try {
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
    } catch (e) {}
};

// ============================================================
// 개별 패널 숨기기 함수들
// ============================================================

var hidePanelLeft = function() {
    try {
        var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
        if (panelLeft) {
            panelLeft.style.display = 'none';  // none = 숨김
        }
    } catch (e) {}
};

var hidePanelRight = function() {
    try {
        var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
        if (panelRight) panelRight.style.display = 'none';
    } catch (e) {}
};

var hidePanelTop = function() {
    try {
        var panelTop = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
        if (panelTop) panelTop.style.display = 'none';
    } catch (e) {}
};

var hideHeader = function() {
    try {
        var header = document.querySelector('#__nuxt > div > div > header');
        if (header) header.style.display = 'none';
    } catch (e) {}
};

var hideFooter = function() {
    try {
        var footerWrap = document.querySelector('#__nuxt > div > div > div.footer-wrap');
        if (footerWrap) {
            footerWrap.style.display = 'none';
        }

        // UI 제거 완료 후 C#에 메시지 전송
        setTimeout(() => {
            try {
                window.chrome.webview.postMessage(JSON.stringify({
                    type: 'ui-elements-removed'
                }));
                console.log('[UI Elements] Sent ui-elements-removed message to C#');
            } catch (e) {
                console.error('[UI Elements] Failed to send message:', e);
            }
        }, 100);
    } catch (e) {}
};

// ============================================================
// 큰 창일 때 요소 복원
// ============================================================
var restoreElementsForLargeSize = function() {
    try {
        var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
        if (panelLeft) panelLeft.style.display = '';

        var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
        if (panelRight) panelRight.style.display = '';

        var panelTop = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
        if (panelTop) panelTop.style.display = '';

        var header = document.querySelector('#__nuxt > div > div > header');
        if (header) header.style.display = '';
    } catch (e) {}
};

// ============================================================
// 작은 창일 때 요소 숨김
// ============================================================
var hideElementsForSmallSize = function() {
    try {
        var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
        if (panelLeft) panelLeft.style.display = 'none';

        var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
        if (panelRight) panelRight.style.display = 'none';

        var panelTop = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
        if (panelTop) panelTop.style.display = 'none';

        var header = document.querySelector('#__nuxt > div > div > header');
        if (header) header.style.display = 'none';
    } catch (e) {}
};

// ============================================================
// 요소 표시 상태 확인
// ============================================================
var checkElementsVisibilityStatus = function() {
    try {
        var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
        var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
        var header = document.querySelector('#__nuxt > div > div > header');

        // 객체 리터럴: { key: value } 형태로 데이터 저장
        var status = {
            panelLeftVisible: panelLeft ? (panelLeft.style.display !== 'none') : false,
            panelRightVisible: panelRight ? (panelRight.style.display !== 'none') : false,
            headerVisible: header ? (header.style.display !== 'none') : false
        };

        // JSON.stringify: 객체를 JSON 문자열로 변환
        return JSON.stringify(status);
    } catch (e) {
        return '{}';
    }
};
