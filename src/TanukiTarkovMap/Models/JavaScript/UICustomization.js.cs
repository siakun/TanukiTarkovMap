namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// WebView2 UI 커스터마이징 관련 JavaScript 스크립트
    /// - 불필요한 UI 요소 제거
    /// - "Tarkov Pilot" → "Tarkov Client" 브랜딩 변경
    /// - DOM 변경 감시 및 자동 재적용
    /// </summary>
    public static class UICustomization
    {
        /// <summary>
        /// 불필요한 UI 요소를 제거하고 브랜딩을 변경하는 스크립트
        ///
        /// 실행 절차:
        /// 1. 페이지 하단 및 상단의 불필요한 요소들을 제거
        /// 2. 브랜드 영역에 "Tarkov Client" 링크 추가
        /// 3. "Tarkov Pilot" 텍스트를 "Tarkov Client"로 일괄 변경
        /// 4. MutationObserver를 설정하여 DOM 변경 시 자동 재적용
        /// 5. 중복 실행 방지를 위한 플래그 관리
        /// </summary>
        public const string REMOVE_UNWANTED_ELEMENTS_SCRIPT =
            @"
                (function() {
                    let isProcessed = false;

                    function applyCustomizations() {
                        try {
                            // 이미 처리된 경우 중복 실행 방지
                            if (isProcessed) return;

                            // 페이지 하단 요소들 제거
                            var panelTopElement = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top > div > div.d-flex.ml-15.fs-0');
                            if (panelTopElement) {
                                panelTopElement.remove();
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

                            // p-relative 내부 요소는 숨김으로만 처리
                            var pRelativeElement = document.querySelector('#__nuxt > div > div > div.p-relative > a > div:nth-child(3)');
                            if (pRelativeElement) {
                                pRelativeElement.style.display = 'none';
                            }

                            var pilotStatusElement = document.querySelector('#__nuxt > div > div > div.p-relative > a > div.pilot-status.mr-10.connected');
                            if (pilotStatusElement) {
                                pilotStatusElement.style.display = 'none';
                            }

                            // ::before 가상 요소 제거
                            var beforeTargetElement = document.querySelector('#__nuxt > div > div > header > div:nth-child(3) > div > div');
                            if (beforeTargetElement) {
                                beforeTargetElement.style.position = 'relative';
                                beforeTargetElement.style.setProperty('--before-display', 'none');

                                var style = document.createElement('style');
                                style.textContent = '#__nuxt > div > div > header > div:nth-child(3) > div > div::before { display: none !important; content: none !important; }';
                                document.head.appendChild(style);
                            }

                            var brandDescElement = document.querySelector('#__nuxt > div > div > header > div.brand > div.desc');
                            if (brandDescElement) {
                                brandDescElement.remove();
                            }

                            var containers = document.querySelectorAll('.container');
                            for (var i = 0; i < containers.length; i++) {
                                containers[i].remove();
                            }

                            var tarkovPilotElement = document.querySelector('.p-relative a');
                            var brandContainer = document.querySelector('#__nuxt > div > div > header > div.brand');

                            if (brandContainer) {
                                var originalTitle = brandContainer.querySelector('div.title > a');

                                // 이미 커스터마이징되었는지 확인
                                if (!brandContainer.querySelector('.tarkov-client-separator')) {
                                    // 구분자 추가
                                    var separator = document.createElement('span');
                                    separator.className = 'tarkov-client-separator';
                                    separator.textContent = ' | ';
                                    separator.style.cssText = 'color: inherit; margin: 0 8px; opacity: 1; font-weight: normal; display: inline;';

                                    var clientLink = document.createElement('a');
                                    clientLink.className = 'tarkov-client-link';
                                    clientLink.href = '/pilot';
                                    clientLink.textContent = 'Tarkov Client';
                                    clientLink.style.cssText = 'font-family: inherit; font-size: inherit; font-weight: inherit; color: inherit; text-decoration: none; white-space: nowrap;';

                                    if (originalTitle) {
                                        var computedStyle = window.getComputedStyle(originalTitle);
                                        clientLink.style.fontFamily = computedStyle.fontFamily;
                                        clientLink.style.fontSize = computedStyle.fontSize;
                                        clientLink.style.fontWeight = computedStyle.fontWeight;
                                        clientLink.style.color = computedStyle.color;

                                        // 구분자도 같은 스타일 적용
                                        separator.style.fontFamily = computedStyle.fontFamily;
                                        separator.style.fontSize = computedStyle.fontSize;
                                        separator.style.color = computedStyle.color;
                                    }

                                    brandContainer.style.cssText = 'display: flex; align-items: center; flex-wrap: nowrap; max-width: 45%; width: auto; overflow: visible; box-sizing: border-box; flex-shrink: 1;';

                                    var titleDiv = brandContainer.querySelector('div.title');
                                    if (titleDiv) {
                                        titleDiv.style.cssText = 'display: flex; align-items: center; flex-wrap: nowrap; overflow: visible;';
                                        titleDiv.appendChild(separator);
                                        titleDiv.appendChild(clientLink);
                                    }
                                }

                                var headerContainer = document.querySelector('#__nuxt > div > div > header');
                                if (headerContainer) {
                                    headerContainer.style.cssText = 'display: flex; justify-content: space-between; align-items: center; width: 100%; box-sizing: border-box; overflow: visible; padding: 0 20px; position: relative;';
                                }

                                // p-relative 컨테이너는 드롭다운 위치를 위해 유지하고, 내부 요소만 숨김
                                if (tarkovPilotElement) {
                                    var pRelativeContainer = tarkovPilotElement.closest('.p-relative');
                                    if (pRelativeContainer) {
                                        // 컨테이너는 유지하되 내부 링크만 숨김
                                        tarkovPilotElement.style.display = 'none';
                                    }
                                }
                            }

                            var walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, null, false);
                            var node;
                            var textNodes = [];
                            while (node = walker.nextNode()) {
                                if (node.textContent.includes('Tarkov Pilot')) {
                                    textNodes.push(node);
                                }
                            }

                            for (var j = 0; j < textNodes.length; j++) {
                                textNodes[j].textContent = textNodes[j].textContent.replace(/Tarkov Pilot/g, 'Tarkov Client');
                            }

                            isProcessed = true;
                        } catch { }
                    }

                    // 초기 커스터마이징 적용
                    applyCustomizations();

                    // MutationObserver로 DOM 변경 감시
                    const observer = new MutationObserver(function(mutations) {
                        let shouldReapply = false;

                        mutations.forEach(function(mutation) {
                            if (mutation.type === 'childList') {
                                // 중요한 요소가 다시 추가되었는지 확인
                                mutation.addedNodes.forEach(function(node) {
                                    if (node.nodeType === 1) { // Element node
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
                            setTimeout(applyCustomizations, 100);
                        }
                    });

                    // body 전체를 감시
                    observer.observe(document.body, {
                        childList: true,
                        subtree: true
                    });

                })();";
    }
}
