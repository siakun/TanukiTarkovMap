/**
 * 웹 요소 제어 스크립트
 *
 * tarkov-market.com 웹페이지의 UI 요소 가시성을 제어합니다.
 *
 * 구조:
 * - 각 함수는 window 객체에 등록되어 C#에서 호출 가능
 * - 헤더/푸터는 항상 숨김 유지
 * - 패널(좌/우/상단)과 좁은 창의 모바일 UI는 "UI 요소 숨기기" 체크박스에 따라 토글
 */

(function() {
    'use strict';

    // ============================================================
    // 숨김 규칙 (스타일시트 한 장으로 관리)
    //
    // 요소마다 style.display를 넣지 않고 규칙을 쓴다. 인라인 방식은 두 가지에 약하다.
    // 나중에 만들어진 요소는 놓치고, 다른 스크립트가 style.cssText를 대입하면 함께 지워진다.
    // 실제로 ui-customization.js가 헤더의 cssText를 덮어써 숨김이 풀리는 문제가 있었다.
    // !important를 붙인 규칙은 인라인 스타일보다 우선하므로 그 두 경우를 모두 막는다
    // ============================================================
    var STYLE_ID = 'tanuki-visibility-rules';
    var PANEL_HIDDEN_CLASS = 'tanuki-panels-hidden';

    /**
     * 숨김 규칙을 문서에 한 번만 넣는다
     */
    function ensureRules() {
        if (document.getElementById(STYLE_ID)) return;

        var style = document.createElement('style');
        style.id = STYLE_ID;
        style.textContent =
            // 헤더와 푸터는 언제나 숨긴다 (복원 대상이 아니다)
            'header, .footer-wrap { display: none !important; }' +
            // 쿠키 안내 줄도 숨긴다. 화면에서만 가리는 것이고 동의를 누르지는 않는다.
            // position: fixed로 지도 아래쪽을 덮고 있어, 이 창에서는 지도를 가리는 방해물이다
            '.cookie-consent { display: none !important; }' +
            // 패널은 <html>의 클래스로 켜고 끈다
            'html.' + PANEL_HIDDEN_CLASS + ' .panel_left,' +
            'html.' + PANEL_HIDDEN_CLASS + ' .panel_right,' +
            'html.' + PANEL_HIDDEN_CLASS + ' .panel_top,' +
            // 창이 좁으면 사이트가 데스크톱 패널을 접고 대신 모바일 UI(위 검색 줄, 아래 탭 줄)를
            // 편다. 이 요소들은 폭과 상관없이 늘 문서에 있고 사이트의 미디어 쿼리가 display만
            // 바꾸므로(실측: 넓을 때 none, 좁을 때 block), 만들어지고 지워지는 것이 아니라
            // 켜지고 꺼지는 것이다. 그래서 지우지 않고 같은 클래스로 함께 끈다.
            // 지우는 쪽은 사이트가 다시 그릴 때마다 되살아나고, 우리가 지운 자리를 사이트가
            // 참조하면 그쪽이 깨진다
            'html.' + PANEL_HIDDEN_CLASS + ' .mobile-map-ui { display: none !important; }';

        (document.head || document.documentElement).appendChild(style);
    }

    // ============================================================
    // 헤더 숨기기 (항상 숨김 유지)
    // ============================================================
    window.hideHeader = function() {
        try {
            ensureRules();

            // 숨김으로 빈 자리가 생기므로 지도 쪽 레이아웃을 다시 계산하게 한다
            window.dispatchEvent(new Event('resize'));
        } catch (e) {
            console.error('[WebElements] hideHeader error:', e);
        }
    };

    // ============================================================
    // 푸터 숨기기 (항상 숨김 유지)
    // ============================================================
    window.hideFooter = function() {
        try {
            ensureRules();
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
    //
    // 좌/우/상단을 따로 호출하는 C# 쪽 순서를 그대로 두되, 실제로는 한 클래스가 셋을 함께
    // 다룬다. 세 패널이 늘 같이 사라지고 같이 돌아오므로 상태를 셋으로 나눌 이유가 없다
    // ============================================================
    function hidePanels() {
        try {
            ensureRules();
            document.documentElement.classList.add(PANEL_HIDDEN_CLASS);
        } catch (e) {
            console.error('[WebElements] hidePanels error:', e);
        }
    }

    window.hidePanelLeft = hidePanels;
    window.hidePanelRight = hidePanels;
    window.hidePanelTop = hidePanels;

    // ============================================================
    // 패널 복원 (UI 요소 숨기기 해제 시) - 헤더/푸터는 복원하지 않음
    // ============================================================
    window.restorePanels = function() {
        try {
            document.documentElement.classList.remove(PANEL_HIDDEN_CLASS);
        } catch (e) {
            console.error('[WebElements] restorePanels error:', e);
        }
    };

    // ============================================================
    // PMC Extraction 필터 활성화 (SCAV 비활성화 후 PMC 활성화)
    // ============================================================
    window.clickPmcExtraction = function() {
        try {
            var items = document.querySelector('.two-columns > div:nth-child(1) > div:nth-child(2)');
            if (!items) {
                console.warn('[WebElements] Extraction filter container not found');
                return false;
            }

            var pmcFilter = items.querySelector('div:nth-child(2)');
            var scavFilter = items.querySelector('div:nth-child(3)');

            if (!pmcFilter || !scavFilter) {
                console.warn('[WebElements] PMC or SCAV filter not found');
                return false;
            }

            // PMC가 inactive면 클릭하여 활성화
            if (pmcFilter.classList.contains('inactive')) {
                pmcFilter.click();
                console.log('[WebElements] PMC Extraction filter activated');
            }

            // SCAV가 active면 클릭하여 비활성화
            if (!scavFilter.classList.contains('inactive')) {
                scavFilter.click();
                console.log('[WebElements] SCAV Extraction filter deactivated');
            }

            return true;
        } catch (e) {
            console.error('[WebElements] clickPmcExtraction error:', e);
            return false;
        }
    };

    // ============================================================
    // SCAV Extraction 필터 활성화 (PMC 비활성화 후 SCAV 활성화)
    // ============================================================
    window.clickScavExtraction = function() {
        try {
            var items = document.querySelector('.two-columns > div:nth-child(1) > div:nth-child(2)');
            if (!items) {
                console.warn('[WebElements] Extraction filter container not found');
                return false;
            }

            var pmcFilter = items.querySelector('div:nth-child(2)');
            var scavFilter = items.querySelector('div:nth-child(3)');

            if (!pmcFilter || !scavFilter) {
                console.warn('[WebElements] PMC or SCAV filter not found');
                return false;
            }

            // SCAV가 inactive면 클릭하여 활성화
            if (scavFilter.classList.contains('inactive')) {
                scavFilter.click();
                console.log('[WebElements] SCAV Extraction filter activated');
            }

            // PMC가 active면 클릭하여 비활성화
            if (!pmcFilter.classList.contains('inactive')) {
                pmcFilter.click();
                console.log('[WebElements] PMC Extraction filter deactivated');
            }

            return true;
        } catch (e) {
            console.error('[WebElements] clickScavExtraction error:', e);
            return false;
        }
    };

})();
