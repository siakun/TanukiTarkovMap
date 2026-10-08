/**
 * 웹 요소 제어 스크립트
 *
 * tarkov-market.com 웹페이지의 UI 요소 가시성과 추출구 진영 필터를 제어합니다.
 *
 * 구조:
 * - 각 함수는 window 객체에 등록되어 C#에서 호출 가능
 * - 헤더/푸터는 항상 숨김 유지
 * - "UI 요소 숨기기" 체크 시 맵 컨테이너에서 맵 레이어만 남기고 나머지 UI를 숨김
 * - 상단바 PMC/SCAV는 사이트의 추출구 필터 행을 눌러 진영을 전환 (setExtractionFaction)
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
        '.map-wrap',            // 확대와 이동이 걸리는 맵 요소 (2026-10 이전 판, SVG 레이어들을 담는다)
        'canvas',               // 바닥 맵, 사용자가 그린 도형, 마커, 사이트의 내 위치와 스쿼드원을 그리는 캔버스
        '.map-compass-anchor',  // 맵과 함께 움직이는 나침반
        '.squad-layer',         // 현재 위치 마커(.marker)와 스쿼드원 위치 (2026-10 이전 판)
        '.popup-layer',         // 마커를 누르면 뜨는 설명 창
        '.tanuki-position',     // 앱이 그리는 내 위치와 방향 (map-markers.js)
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

    // ============================================================
    // 추출구 진영 전환 (상단바 PMC/SCAV)
    //
    // 사이트 사용자가 왼쪽 패널의 PMC Extraction, Scav Extraction 행을 누르는 것과 같은 일을 한다. 행을 누르면
    // 사이트가 선택을 저장하고 마커 목록을 다시 계산한다. 선택 상태(localStorage의 sel_cats_map과 그 반응형
    // 객체)만 변경하면 저장은 되지만 지도는 그대로다. 사이트는 패널의 전환 처리기에서만 마커 목록을 다시 계산하고,
    // 그 처리기는 production 빌드라 렌더 트리(setupState)에서 닿지 않는다.
    //
    // 창 폭이 900px 이하이면 사이트가 모바일 배치로 전환하고 왼쪽 패널을 렌더링하지 않는다. display:none이 아니라
    // DOM에 없고, 앱 창은 보통 이 폭이다. 그때는 하단 도크의 Filters 버튼으로 같은 패널을 열어 행을 누르고, 열기 전
    // 도크 상태로 되돌린다. 사이트의 도크 버튼은 같은 패널을 다시 누르면 닫는다.
    //
    // 행과 버튼은 영어 라벨로 찾는다. 사이트를 다른 언어로 열면 찾지 못한다.
    // 결과는 Promise이고 true 또는 실패 이유(no-filter-panel, rows-missing, not-applied)로 끝난다.
    // 사이트는 마커 데이터를 받은 순간에야 추출구 행을 한꺼번에 렌더링하고, 페이지가 막 열렸을 때는 도크 버튼도
    // 아직 반응하지 않는다. 그동안은 rows-missing이나 no-filter-panel이 나오며, 다시 시도는 앱이 한다.
    // ============================================================
    // 상단바를 빠르게 연달아 누르면 앞 전환이 다시 그려지기 전에 다음 전환이 행의 상태를 읽는다. 전환을 한 줄로
    // 세운다. 이 스크립트는 다시 주입될 때마다 새 클로저를 만들므로 줄은 window에 둔다
    var FACTION_QUEUE = Symbol.for('TanukiTarkovMap.factionQueue');

    function findFilterRow(name) {
        var rows = document.querySelectorAll('.panel_left .items > div');
        for (var index = 0; index < rows.length; index++) {
            var label = rows[index].firstElementChild;
            if (label && label.textContent.trim() === name) return rows[index];
        }
        return null;
    }

    function findDockButton(matches) {
        var buttons = document.querySelectorAll('nav.mobile-map-dock > button');
        for (var index = 0; index < buttons.length; index++) {
            if (matches(buttons[index])) return buttons[index];
        }
        return null;
    }

    // Vue는 클릭 처리 뒤 마이크로태스크에서 다시 그린다. 마이크로태스크로만 기다려 화면이 그려지기 전에 끝낸다.
    // 그래야 패널을 열었다 닫는 모습이 보이지 않는다. setTimeout이나 requestAnimationFrame으로 기다리면 그 사이
    // 열린 패널이 한 프레임 그려질 수 있다. 데이터가 있으면 행은 첫 마이크로태스크에서 나온다
    async function waitUntil(condition) {
        for (var turn = 0; turn < 20; turn++) {
            if (condition()) return true;
            await Promise.resolve();
        }
        return condition();
    }

    async function applyFaction(pmc) {
        // 한쪽 진영의 추출구가 없는 맵은 그 행도 없다(Labs의 Scav Extraction 등). 있는 행만 맞춘다. 그런 맵에서
        // SCAV를 고르면 PMC 추출구만 꺼지고 아무것도 남지 않는데, Local 미니맵도 같다. 행이 없는 진영의 저장된 선택은
        // 앞 맵에서 남은 값 그대로지만 그 맵에는 그 진영의 마커가 없고, 그 진영이 있는 맵을 열면 다시 맞춘다
        var targets = [{ name: 'PMC Extraction', active: pmc }, { name: 'Scav Extraction', active: !pmc }];
        var anyRow = function () {
            return targets.some(function (target) { return !!findFilterRow(target.name); });
        };
        var mismatched = function (target) {
            var row = findFilterRow(target.name);
            return !!row && row.classList.contains('inactive') === target.active;
        };
        var filtersButton = null;
        var selectedBefore = null;
        if (!anyRow()) {
            filtersButton = findDockButton(function (button) { return button.textContent.trim() === 'Filters'; });
            if (!filtersButton) return 'no-filter-panel';
            selectedBefore = findDockButton(function (button) { return button.classList.contains('selected'); });
            filtersButton.click();
        }
        var rowsFound = await waitUntil(anyRow);
        var applied = false;
        if (rowsFound) {
            targets.filter(mismatched).forEach(function (target) { findFilterRow(target.name).click(); });
            applied = await waitUntil(function () { return !targets.some(mismatched); });
        }
        if (filtersButton) {
            filtersButton.click();
            if (selectedBefore && selectedBefore !== filtersButton) selectedBefore.click();
        }
        if (applied) return true;
        return rowsFound ? 'not-applied' : 'rows-missing';
    }

    window.setExtractionFaction = function (pmc) {
        var run = (window[FACTION_QUEUE] || Promise.resolve()).then(function () {
            return applyFaction(!!pmc);
        });
        window[FACTION_QUEUE] = run.catch(function () {});
        return run.catch(function (error) {
            console.error('[WebElements] setExtractionFaction error:', error);
            return 'error: ' + (error && error.message);
        });
    };

})();
