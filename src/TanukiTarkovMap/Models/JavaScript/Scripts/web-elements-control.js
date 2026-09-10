/**
 * 웹 요소 제어 스크립트
 *
 * tarkov-market.com 웹페이지의 UI 요소 가시성을 제어합니다.
 *
 * 구조:
 * - 각 함수는 window 객체에 등록되어 C#에서 호출 가능
 * - 헤더/푸터는 항상 숨김 유지
 * - 패널(좌/우/상단)과 좁은 창의 모바일 UI는 "UI 요소 숨기기" 체크박스에 따라 토글
 * - 로컬 모드에서는 지도, 층 전환, 추출구와 현재 위치/방향 외의 UI와 마커를 숨김
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
    var LOCAL_CORE_CLASS = 'tanuki-local-core';
    var SELECTED_CATEGORIES_KEY = 'sel_cats_map';
    var ONLINE_CATEGORIES_BACKUP_KEY = 'tanuki-online-map-categories';

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
            // 패널은 <html>의 클래스로 켜고 끈다. 로컬 모드에서도 그대로 적용한다.
            // 로컬 모드는 "부가 UI를 늘 감추는 것"이고 이 체크박스는 "남은 UI까지 감추는 것"이라
            // 둘은 층이 다르다. 로컬에서 이 규칙을 빼면 체크를 해도 Levels가 남아, 사용자에게는
            // 체크박스가 고장난 것으로 보인다
            'html.' + PANEL_HIDDEN_CLASS + ' .panel_left,' +
            'html.' + PANEL_HIDDEN_CLASS + ' .panel_right,' +
            'html.' + PANEL_HIDDEN_CLASS + ' .panel_top,' +
            // 창이 좁으면 사이트가 데스크톱 패널을 접고 대신 모바일 UI(위 검색 줄, 아래 탭 줄)를
            // 편다. 이 요소들은 폭과 상관없이 늘 문서에 있고 사이트의 미디어 쿼리가 display만
            // 바꾸므로(실측: 넓을 때 none, 좁을 때 block), 만들어지고 지워지는 것이 아니라
            // 켜지고 꺼지는 것이다. 그래서 지우지 않고 같은 클래스로 함께 끈다.
            // 지우는 쪽은 사이트가 다시 그릴 때마다 되살아나고, 우리가 지운 자리를 사이트가
            // 참조하면 그쪽이 깨진다
            'html.' + PANEL_HIDDEN_CLASS + ' .mobile-map-ui,' +
            // 로컬 모드는 코어만 남긴다. panel_right 자체를 끄지 않아 층 전환은 유지한다.
            'html.' + LOCAL_CORE_CLASS + ' .maps-site-chrome > .head-pilot,' +
            'html.' + LOCAL_CORE_CLASS + ' .maps-site-chrome > .alert-box,' +
            'html.' + LOCAL_CORE_CLASS + ' .panel_left,' +
            'html.' + LOCAL_CORE_CLASS + ' .panel_top,' +
            'html.' + LOCAL_CORE_CLASS + ' .panel_right .squad-panel,' +
            'html.' + LOCAL_CORE_CLASS + ' .panel_right .user-layers-panel,' +
            'html.' + LOCAL_CORE_CLASS + ' .panel_right .tools_quests,' +
            // 맵 오른쪽 아래에 겹쳐 뜨는 출처 줄. 코어가 아니므로 로컬에서는 체크와 무관하게 끈다
            'html.' + LOCAL_CORE_CLASS + ' .map-credits,' +
            'html.' + LOCAL_CORE_CLASS + ' .mobile-map-ui { display: none !important; }';

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
    // 로컬 모드 코어 화면과 마커 선택
    //
    // 사이트는 모듈을 평가할 때 sel_cats_map을 한 번 읽어 마커를 그린다. 렌더 뒤 DOM을
    // 지우면 캔버스에 그린 마커가 남으므로, 페이지 스크립트보다 먼저 저장값을 바꾼다.
    // 현재 앱은 모드마다 저장 공간을 분리한다. 아래 백업 복원은 저장 공간을 공유하던 버전에서
    // 남긴 온라인 선택값도 복구하려고 유지한다.
    // ============================================================
    function applyLocalMarkerSelection(enabled, isPmc) {
        var backupText = localStorage.getItem(ONLINE_CATEGORIES_BACKUP_KEY);

        if (enabled) {
            if (backupText === null) {
                var onlineValue = localStorage.getItem(SELECTED_CATEGORIES_KEY);
                localStorage.setItem(ONLINE_CATEGORIES_BACKUP_KEY, JSON.stringify({
                    exists: onlineValue !== null,
                    value: onlineValue
                }));
            }

            // archive의 지도 bundle은 Transition을 PMC Extraction과 같은 색으로 분류한다.
            // Co-Op은 두 진영이 함께 쓰므로 PMC와 SCAV 양쪽에서 남긴다.
            var localCategories = { 'Extractions_Co-Op Extraction': true };
            if (isPmc) {
                localCategories['Extractions_Transition'] = true;
                localCategories['Extractions_PMC Extraction'] = true;
            } else {
                localCategories['Extractions_Scav Extraction'] = true;
            }
            localStorage.setItem(SELECTED_CATEGORIES_KEY, JSON.stringify(localCategories));
            return;
        }

        if (backupText === null) return;

        var backup = JSON.parse(backupText);
        if (backup.exists) {
            localStorage.setItem(SELECTED_CATEGORIES_KEY, backup.value);
        } else {
            localStorage.removeItem(SELECTED_CATEGORIES_KEY);
        }
        localStorage.removeItem(ONLINE_CATEGORIES_BACKUP_KEY);
    }

    window.setLocalMapMode = function(enabled, isPmc) {
        try {
            // 이 함수는 FrameLoadStart에서 실행된다. 저장값은 사이트 모듈보다 먼저 바꾸고,
            // DOM이 아직 없으면 클래스와 스타일만 DOMContentLoaded까지 미룬다.
            applyLocalMarkerSelection(enabled, isPmc);

            var applyClass = function() {
                ensureRules();
                document.documentElement.classList.toggle(LOCAL_CORE_CLASS, enabled);
                window.dispatchEvent(new Event('resize'));
            };

            if (document.documentElement) {
                applyClass();
            } else {
                document.addEventListener('DOMContentLoaded', applyClass, { once: true });
            }
            return true;
        } catch (e) {
            console.error('[WebElements] setLocalMapMode error:', e);
            return false;
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

            // 저장 키는 Transition이지만 사이트가 화면에 붙이는 이름은 Transit이다.
            var transitionFilter = findExtractionFilter(items, 'Transit');
            var pmcFilter = findExtractionFilter(items, 'PMC Extraction');
            var scavFilter = findExtractionFilter(items, 'Scav Extraction');
            var coOpFilter = findExtractionFilter(items, 'Co-Op Extraction');
            var localMode = document.documentElement.classList.contains(LOCAL_CORE_CLASS);

            if (!localMode && (!pmcFilter || !scavFilter)) {
                console.warn('[WebElements] PMC or SCAV filter not found');
                return false;
            }

            setFilterActive(pmcFilter, true);
            setFilterActive(scavFilter, false);
            if (localMode) {
                setFilterActive(transitionFilter, true);
                setFilterActive(coOpFilter, true);
            }

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

            // 저장 키는 Transition이지만 사이트가 화면에 붙이는 이름은 Transit이다.
            var transitionFilter = findExtractionFilter(items, 'Transit');
            var pmcFilter = findExtractionFilter(items, 'PMC Extraction');
            var scavFilter = findExtractionFilter(items, 'Scav Extraction');
            var coOpFilter = findExtractionFilter(items, 'Co-Op Extraction');
            var localMode = document.documentElement.classList.contains(LOCAL_CORE_CLASS);

            if (!localMode && (!pmcFilter || !scavFilter)) {
                console.warn('[WebElements] PMC or SCAV filter not found');
                return false;
            }

            setFilterActive(pmcFilter, false);
            setFilterActive(scavFilter, true);
            if (localMode) {
                setFilterActive(transitionFilter, false);
                setFilterActive(coOpFilter, true);
            }

            console.log('[WebElements] SCAV Extraction filter activated');
            return true;
        } catch (e) {
            console.error('[WebElements] clickScavExtraction error:', e);
            return false;
        }
    };

})();
