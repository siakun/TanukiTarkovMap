/**
 * UI 커스터마이징 스크립트
 *
 * 목적:
 * - 웹페이지의 불필요한 UI 요소 제거
 * - "Tarkov Pilot" 브랜딩을 "Tarkov Client"로 변경
 * - DOM 변경 감시하여 자동으로 재적용
 *
 * JavaScript 기본 개념:
 * 1. IIFE (즉시 실행 함수): 전역 공간 오염 방지
 * 2. let/const: 변수 선언 (let은 재할당 가능, const는 불가능)
 * 3. document.querySelector: HTML 요소 찾기
 * 4. MutationObserver: DOM 변경 감지
 */

(function() {
    // 중복 실행 방지 플래그
    // let: 값을 변경할 수 있는 변수 선언
    let isProcessed = false;

    /**
     * 커스터마이징 적용 함수
     *
     * 실행 순서:
     * 1. 중복 실행 체크
     * 2. 불필요한 요소 제거
     * 3. 브랜딩 변경
     * 4. 텍스트 일괄 치환
     */
    function applyCustomizations() {
        try {
            // 이미 처리되었으면 종료
            if (isProcessed) return;

            // ========================================
            // Step 1: 불필요한 요소 제거
            // ========================================

            // querySelector: CSS 선택자로 HTML 요소를 찾습니다
            // 예: '.class-name' (클래스), '#id-name' (ID), 'div > span' (자식 요소)
            var panelTopElement = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top > div > div.d-flex.ml-15.fs-0');
            if (panelTopElement) {
                panelTopElement.remove(); // 요소 삭제
            }

            var mb15Element = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div:nth-child(1) > div.mb-15');
            if (mb15Element) {
                mb15Element.remove();
            }

            var firstAElement = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div:nth-child(2) > div.mb-15 > div > a:first-child');
            if (firstAElement) {
                firstAElement.remove();
            }

            var mb15DivElement = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div:nth-child(1) > div.mb-15 > div');
            if (mb15DivElement) {
                mb15DivElement.remove();
            }

            var mb25Element = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div:nth-child(1) > div.mb-25');
            if (mb25Element) {
                mb25Element.remove();
            }

            var pRelativeSpanElement = document.querySelector('#__nuxt > div > div > div.p-relative > div > span');
            if (pRelativeSpanElement) {
                pRelativeSpanElement.remove();
            }

            // ========================================
            // Step 2: 요소 숨김 (display: none)
            // remove()는 완전 삭제, style.display는 숨김
            // ========================================
            var pRelativeElement = document.querySelector('#__nuxt > div > div > div.p-relative > a > div:nth-child(3)');
            if (pRelativeElement) {
                pRelativeElement.style.display = 'none';
            }

            var pilotStatusElement = document.querySelector('#__nuxt > div > div > div.p-relative > a > div.pilot-status.mr-10.connected');
            if (pilotStatusElement) {
                pilotStatusElement.style.display = 'none';
            }

            // ========================================
            // Step 3: CSS 가상 요소 제거
            // ::before는 CSS로 생성된 요소라 JavaScript로 직접 제어 불가
            // 따라서 <style> 태그를 만들어서 CSS 규칙 추가
            // ========================================
            var beforeTargetElement = document.querySelector('#__nuxt > div > div > header > div:nth-child(3) > div > div');
            if (beforeTargetElement) {
                beforeTargetElement.style.position = 'relative';
                beforeTargetElement.style.setProperty('--before-display', 'none');

                // document.createElement: 새로운 HTML 요소 생성
                var style = document.createElement('style');
                style.textContent = '#__nuxt > div > div > header > div:nth-child(3) > div > div::before { display: none !important; content: none !important; }';
                document.head.appendChild(style);
            }

            var brandDescElement = document.querySelector('#__nuxt > div > div > header > div.brand > div.desc');
            if (brandDescElement) {
                brandDescElement.remove();
            }

            // querySelectorAll: 여러 요소를 배열처럼 가져옵니다
            var containers = document.querySelectorAll('.container');
            // for 반복문: 배열의 각 요소를 순회
            for (var i = 0; i < containers.length; i++) {
                containers[i].remove();
            }

            // ========================================
            // Step 4: 브랜딩 변경 - "Tarkov Client" 추가
            // ========================================
            var tarkovPilotElement = document.querySelector('.p-relative a');
            var brandContainer = document.querySelector('#__nuxt > div > div > header > div.brand');

            if (brandContainer) {
                var originalTitle = brandContainer.querySelector('div.title > a');

                // 이미 커스터마이징되었는지 확인 (중복 방지)
                if (!brandContainer.querySelector('.tarkov-client-separator')) {
                    // 구분자 생성
                    var separator = document.createElement('span');
                    separator.className = 'tarkov-client-separator';
                    separator.textContent = ' | ';
                    separator.style.cssText = 'color: inherit; margin: 0 8px; opacity: 1; font-weight: normal; display: inline;';

                    // "Tarkov Client" 링크 생성
                    var clientLink = document.createElement('a');
                    clientLink.className = 'tarkov-client-link';
                    clientLink.href = '/pilot';
                    clientLink.textContent = 'Tarkov Client';
                    clientLink.style.cssText = 'font-family: inherit; font-size: inherit; font-weight: inherit; color: inherit; text-decoration: none; white-space: nowrap;';

                    if (originalTitle) {
                        // window.getComputedStyle: 요소의 실제 적용된 CSS 스타일 가져오기
                        var computedStyle = window.getComputedStyle(originalTitle);
                        clientLink.style.fontFamily = computedStyle.fontFamily;
                        clientLink.style.fontSize = computedStyle.fontSize;
                        clientLink.style.fontWeight = computedStyle.fontWeight;
                        clientLink.style.color = computedStyle.color;

                        separator.style.fontFamily = computedStyle.fontFamily;
                        separator.style.fontSize = computedStyle.fontSize;
                        separator.style.color = computedStyle.color;
                    }

                    brandContainer.style.cssText = 'display: flex; align-items: center; flex-wrap: nowrap; max-width: 45%; width: auto; overflow: visible; box-sizing: border-box; flex-shrink: 1;';

                    var titleDiv = brandContainer.querySelector('div.title');
                    if (titleDiv) {
                        titleDiv.style.cssText = 'display: flex; align-items: center; flex-wrap: nowrap; overflow: visible;';
                        // appendChild: 자식 요소로 추가
                        titleDiv.appendChild(separator);
                        titleDiv.appendChild(clientLink);
                    }
                }

                var headerContainer = document.querySelector('#__nuxt > div > div > header');
                if (headerContainer) {
                    headerContainer.style.cssText = 'display: flex; justify-content: space-between; align-items: center; width: 100%; box-sizing: border-box; overflow: visible; padding: 0 20px; position: relative;';
                }

                // closest(): 가장 가까운 부모 요소 찾기
                if (tarkovPilotElement) {
                    var pRelativeContainer = tarkovPilotElement.closest('.p-relative');
                    if (pRelativeContainer) {
                        tarkovPilotElement.style.display = 'none';
                    }
                }
            }

            // ========================================
            // Step 5: 텍스트 일괄 변경
            // "Tarkov Pilot" → "Tarkov Client"
            // ========================================

            // TreeWalker: DOM 트리를 순회하는 효율적인 방법
            var walker = document.createTreeWalker(
                document.body,
                NodeFilter.SHOW_TEXT,  // 텍스트 노드만 찾기
                null,
                false
            );

            var node;
            var textNodes = [];

            // while 반복문: 조건이 true인 동안 계속 반복
            while (node = walker.nextNode()) {
                // includes(): 문자열에 특정 텍스트가 포함되어 있는지 확인
                if (node.textContent.includes('Tarkov Pilot')) {
                    textNodes.push(node);
                }
            }

            for (var j = 0; j < textNodes.length; j++) {
                // replace(): 문자열 치환 (/g는 "모두 찾기")
                textNodes[j].textContent = textNodes[j].textContent.replace(/Tarkov Pilot/g, 'Tarkov Client');
            }

            // 처리 완료 표시
            isProcessed = true;
        } catch (e) {
            // 에러 발생 시 무시 (웹페이지 구조가 달라질 수 있음)
        }
    }

    // ========================================
    // 초기 실행
    // ========================================
    applyCustomizations();

    // ========================================
    // MutationObserver: DOM 변경 감시
    //
    // 웹페이지가 동적으로 변경될 때 (예: React, Vue 등)
    // 자동으로 커스터마이징을 다시 적용합니다
    // ========================================

    // new: 객체 생성자
    const observer = new MutationObserver(function(mutations) {
        let shouldReapply = false;

        // forEach: 배열의 각 요소에 대해 함수 실행
        mutations.forEach(function(mutation) {
            if (mutation.type === 'childList') {
                // addedNodes: 새로 추가된 요소들
                mutation.addedNodes.forEach(function(node) {
                    // nodeType === 1: Element 노드 (HTML 요소)
                    if (node.nodeType === 1) {
                        // matches(): CSS 선택자와 일치하는지 확인
                        if (node.matches && (
                            node.matches('.mb-15') ||
                            node.matches('.mb-25') ||
                            node.matches('.panel_top') ||
                            node.matches('.container') ||
                            node.matches('.pilot') ||
                            node.querySelector('.mb-15') ||
                            node.querySelector('.mb-25') ||
                            node.querySelector('.panel_top') ||
                            node.querySelector('.container') ||
                            node.querySelector('.pilot') ||
                            node.matches('#__nuxt > div > div > div.page-content > div > div > div:nth-child(1) > div.mb-15 > div') ||
                            node.querySelector('#__nuxt > div > div > div.page-content > div > div > div:nth-child(1) > div.mb-15 > div') ||
                            node.matches('#__nuxt > div > div > div.page-content > div > div > div:nth-child(1) > div.mb-25') ||
                            node.querySelector('#__nuxt > div > div > div.page-content > div > div > div:nth-child(1) > div.mb-25')
                        )) {
                            shouldReapply = true;
                        }
                    }
                });
            }
        });

        if (shouldReapply) {
            isProcessed = false;
            // setTimeout: 지정된 시간 후에 함수 실행 (100ms = 0.1초)
            setTimeout(applyCustomizations, 100);
        }
    });

    // observe: 감시 시작
    observer.observe(document.body, {
        childList: true,  // 자식 요소 추가/제거 감시
        subtree: true     // 모든 하위 요소도 감시
    });

})();
