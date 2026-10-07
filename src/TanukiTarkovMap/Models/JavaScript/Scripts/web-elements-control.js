/**
 * 웹 요소 제어 스크립트
 *
 * tarkov-market.com 웹페이지의 UI 요소 가시성을 제어합니다.
 *
 * 구조:
 * - 각 함수는 window 객체에 등록되어 C#에서 호출 가능
 * - 헤더/푸터는 항상 숨김 유지
 * - "UI 요소 숨기기" 체크 시 맵 컨테이너에서 맵 레이어만 남기고 나머지 UI를 숨김
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

    // ------------------------------------------------------------
    // 맵 컨테이너(.map-cont)의 직계 자식 가운데 맵을 그리는 레이어
    //
    // 맵 위 UI의 숨김은 숨길 것을 나열하지 않고 이 목록에 없는 직계 자식 전부에 건다.
    // 숨길 것을 나열하면 사이트가 맵 위에 UI를 새로 얹을 때마다 목록이 낡는다. 맵 레이어는
    // UI보다 드물게 바뀌므로 바뀌지 않는 쪽을 적는다. 사이트의 Hide panels 버튼은
    // 좌우 패널만 접고 상단 도구 줄과 나머지 UI를 남기므로 이 기능을 대신하지 못한다.
    //
    // 대가로 실패하는 방향이 뒤집힌다. 사이트가 맵 레이어를 새로 만들면 그 레이어는 체크했을
    // 때만 사라진다. 체크를 풀어야 보이는 것이 맵 위에 있으면 그 레이어를 여기에 추가한다.
    //
    // 컨테이너는 .pan.map-cont가 아니라 .map-cont로 찾는다. pan은 사이트가 조작 상태에 따라
    // 붙였다 떼는 클래스다 (사이트 CSS에 .map-cont.draw, .map-cont.edit 규칙이 함께 있다)
    // ------------------------------------------------------------
    var MAP_LAYER_SELECTORS = [
        '.map-wrap',            // 확대와 이동이 걸리는 맵 요소 (SVG 레이어들을 담는다)
        'canvas',               // 바닥 맵, 사용자가 그린 도형, 마커를 그리는 캔버스
        '.map-compass-anchor',  // 맵과 함께 움직이는 나침반
        '.squad-layer',         // 현재 위치 마커(.marker)와 스쿼드원 위치
        '.popup-layer',         // 마커를 누르면 뜨는 설명 창
        // 현재 위치 표시는 이 숨김이 절대 가리면 안 되는 것이라, 위치 마커를 담은 레이어는 이름이
        // 바뀌어 위 목록이 낡아도 남긴다
        ':has(.marker)'
    ].join(', ');

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
            // 맵 위 UI는 <html>의 클래스로 켜고 끈다. 좌우와 상단 패널을 비롯해 맵 레이어가
            // 아닌 직계 자식이 모두 여기에 걸린다.
            // 창이 좁으면 사이트가 데스크톱 패널을 접고 대신 모바일 UI(위 검색 줄, 아래 탭 줄)를
            // 편다. 이 요소들은 폭과 상관없이 늘 문서에 있고 사이트의 미디어 쿼리가 display만
            // 바꾸므로(실측: 넓을 때 none, 좁을 때 block), 만들어지고 지워지는 것이 아니라
            // 켜지고 꺼지는 것이다. 그래서 지우지 않고 이 규칙으로 함께 끈다.
            // 지우는 쪽은 사이트가 다시 그릴 때마다 되살아나고, 우리가 지운 자리를 사이트가
            // 참조하면 그쪽이 깨진다
            'html.' + PANEL_HIDDEN_CLASS + ' .map-cont > :not(' + MAP_LAYER_SELECTORS + ')' +
            ' { display: none !important; }';

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
    // 좌/우/상단을 따로 호출하는 C# 쪽 순서를 그대로 두되, 실제로는 한 클래스가 맵 레이어 밖의
    // UI를 한꺼번에 다룬다. 모두 같이 사라지고 같이 돌아오므로 상태를 나눌 이유가 없다
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

    function findExtractionFilter(items, name) {
        if (!items) return null;

        for (var index = 0; index < items.children.length; index++) {
            var row = items.children[index];
            var label = row.firstElementChild;
            if (label && label.textContent.trim() === name) return row;
        }
        return null;
    }

    function setFilterActive(filter, active) {
        if (!filter) return;
        if (filter.classList.contains('inactive') === active) filter.click();
    }

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

            var pmcFilter = findExtractionFilter(items, 'PMC Extraction');
            var scavFilter = findExtractionFilter(items, 'Scav Extraction');

            if (!pmcFilter || !scavFilter) {
                console.warn('[WebElements] PMC or SCAV filter not found');
                return false;
            }

            setFilterActive(pmcFilter, true);
            setFilterActive(scavFilter, false);

            console.log('[WebElements] PMC Extraction filter activated');
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

            var pmcFilter = findExtractionFilter(items, 'PMC Extraction');
            var scavFilter = findExtractionFilter(items, 'Scav Extraction');

            if (!pmcFilter || !scavFilter) {
                console.warn('[WebElements] PMC or SCAV filter not found');
                return false;
            }

            setFilterActive(pmcFilter, false);
            setFilterActive(scavFilter, true);

            console.log('[WebElements] SCAV Extraction filter activated');
            return true;
        } catch (e) {
            console.error('[WebElements] clickScavExtraction error:', e);
            return false;
        }
    };

})();
