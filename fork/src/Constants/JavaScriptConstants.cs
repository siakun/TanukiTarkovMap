namespace TarkovClient.Constants
{
    /// <summary>
    /// WebView2에서 사용되는 JavaScript 스크립트 상수들
    /// </summary>
    public static class JavaScriptConstants
    {
        /// <summary>
        /// 불필요한 UI 요소를 제거하는 스크립트
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

        /// <summary>
        /// 방향 표시기를 추가하는 스크립트
        /// </summary>
        public const string ADD_DIRECTION_INDICATORS_SCRIPT =
            @"
                (function () {
                    'use strict';

                    // 사용자가 제공한 SVG 데이터 URL 직접 사용
                    const svgDataUrl = 'data:image/svg+xml;utf8,%0A%20%20%20%20%20%20%20%20%20%20%20%20%3Csvg%20xmlns%3D%22http%3A%2F%2Fwww.w3.org%2F2000%2Fsvg%22%20viewBox%3D%220%200%20100%20100%22%3E%0A%20%20%20%20%20%20%20%20%20%20%20%20%20%20%20%20%3Cpath%20d%3D%22M50%2C5%20L85%2C75%20Q50%2C45%2015%2C75%20Z%22%20fill%3D%22%238a2be2%22%20stroke%3D%22%2370a800%22%20stroke-width%3D%222%22%2F%3E%0A%20%20%20%20%20%20%20%20%20%20%20%20%3C%2Fsvg%3E';

                    function injectStyle() {
                        const style = document.createElement('style');
                        style.id = 'triangle-indicator-style';
                        style.textContent = `
                        .triangle-indicator {
                            position: absolute !important;
                            top: 0% !important;
                            left: 50% !important;
                            width: 25px !important;
                            height: 60px !important;
                            background-image: url('${svgDataUrl}') !important;
                            background-repeat: no-repeat !important;
                            background-size: 100% 100% !important;
                            pointer-events: none !important;
                            z-index: 9999 !important;
                            transform: translate(-50%, -65%) !important;
                            transform-origin: 50% 100% !important;
                            transition: transform 0.1s ease !important;
                        }`;

                        const existingStyle = document.getElementById('triangle-indicator-style');
                        if (existingStyle) existingStyle.remove();
                        document.head.appendChild(style);
                    }

                    function addTriangleToMarker(marker) {
                        if (marker.querySelector('.triangle-indicator')) {
                            return;
                        }

                        const triangle = document.createElement('div');
                        triangle.className = 'triangle-indicator';

                        // 마커가 relative position을 가지도록 설정
                        marker.style.position = 'relative';

                        const computed = window.getComputedStyle(marker);
                        const transform = computed.transform;

                        if (transform && transform !== 'none') {
                            const match = transform.match(/rotate\(([\-\d.]+)deg\)/);
                            if (match) {
                                const angle = parseFloat(match[1]);
                                triangle.style.transform = `translate(-50%, -65%) rotate(${angle}deg)`;
                            }
                        }

                        marker.appendChild(triangle);
                    }

                    function initMarkers() {
                        const markers = document.querySelectorAll('.marker');
                        if (markers.length === 0) {
                            // .marker가 없으면 다른 선택자 시도
                            const altMarkers = document.querySelectorAll('#map > div');
                            altMarkers.forEach(addTriangleToMarker);
                        } else {
                            markers.forEach(addTriangleToMarker);
                        }
                    }

                    injectStyle();

                    const container = document.querySelector('#map') || document.querySelector('#map-layer') || document.body;
                    const observer = new MutationObserver(mutations => {
                        for (const mutation of mutations) {
                            if (mutation.type === 'childList') {
                                mutation.addedNodes.forEach(node => {
                                    if (!(node instanceof HTMLElement)) return;

                                    if (node.classList && node.classList.contains('marker')) {
                                        addTriangleToMarker(node);
                                    } else {
                                        node.querySelectorAll('.marker, #map > div').forEach(addTriangleToMarker);
                                    }
                                });
                            }
                        }
                    });

                    observer.observe(container, {
                        childList: true,
                        subtree: true,
                    });

                    // 2초 후에 마커 초기화 시도
                    setTimeout(initMarkers, 2000);
                })();";

        /// <summary>
        /// PiP 모드용 오버레이 생성 스크립트 (키보드 입력 차단 문제 해결)
        /// </summary>
        public const string CREATE_PIP_OVERLAY_SCRIPT =
            @"
                (function() {
                    'use strict';
                    
                    // 기존 PiP 요소들 제거
                    const existingControlBar = document.getElementById('pip-control-bar');
                    if (existingControlBar) {
                        existingControlBar.remove();
                    }
                    
                    // 기존 스타일 제거
                    const existingStyle = document.getElementById('pip-control-style');
                    if (existingStyle) existingStyle.remove();
                    
                    // 상단 컨트롤 바만 생성 (전체 화면 오버레이 제거)
                    const controlBar = document.createElement('div');
                    controlBar.id = 'pip-control-bar';
                    controlBar.style.cssText = `
                        position: fixed !important;
                        top: 0 !important;
                        left: 0 !important;
                        width: 100% !important;
                        height: 50px !important;
                        z-index: 2147483647 !important;
                        pointer-events: auto !important;
                        background: transparent !important;
                        display: flex !important;
                        opacity: 1 !important;
                        transition: opacity 0.2s ease !important;
                        border-bottom: 1px solid transparent !important;
                    `;
                    
                    // 창이동  영역 (80%) - 실제 텍스트 콘텐츠 추가
                    const dragArea = document.createElement('div');
                    dragArea.id = 'pip-drag-area';
                    dragArea.innerHTML = '';
                    dragArea.style.cssText = `
                        position: relative !important;
                        width: 80% !important;
                        height: 100% !important;
                        cursor: move !important;
                        display: flex !important;
                        align-items: center !important;
                        justify-content: center !important;
                        color: white !important;
                        font-size: 14px !important;
                        font-weight: bold !important;
                        text-align: center !important;
                        background: transparent !important;
                    `;
                    
                    // 종료 버튼 영역 (20%) - 실제 텍스트 콘텐츠 추가
                    const exitArea = document.createElement('div');
                    exitArea.id = 'pip-exit-area';
                    exitArea.innerHTML = '';
                    exitArea.style.cssText = `
                        position: relative !important;
                        width: 20% !important;
                        height: 100% !important;
                        cursor: pointer !important;
                        display: flex !important;
                        align-items: center !important;
                        justify-content: center !important;
                        color: white !important;
                        font-size: 20px !important;
                        font-weight: bold !important;
                        background: transparent !important;
                        border-left: 1px solid transparent !important;
                    `;
                    
                    // 스타일 추가
                    const style = document.createElement('style');
                    style.id = 'pip-control-style';
                    style.textContent = `
                        #pip-control-bar {
                            font-family: Arial, sans-serif !important;
                        }
                        #pip-control-bar {
                            opacity: 1 !important;
                            background: rgba(0, 0, 0, 0.85) !important;
                            border-bottom-color: rgba(255, 255, 255, 0.2) !important;
                        }
                        #pip-drag-area {
                            color: rgba(255, 255, 255, 0.8) !important;
                            font-size: 14px !important;
                        }
                        #pip-exit-area {
                            color: rgba(255, 255, 255, 0.8) !important;
                            font-size: 18px !important;
                        }
                        #pip-control-bar {
                            background: rgba(80, 80, 80, 0.8) !important;
                        }
                    `;
                    

                    document.head.appendChild(style);
                    
                    // 창이동 기능 구현 (WPF DragMove 방식)
                    dragArea.addEventListener('mousedown', function(e) {
                        
                        // WPF로 창이동 시작 알림
                        const dragStartMessage = {
                            type: 'pip-drag-start'
                        };

                        try {
                            window.chrome.webview.postMessage(JSON.stringify(dragStartMessage));
                        } catch {}
                        
                        e.preventDefault();
                    });
                    
                    // 종료 기능 구현
                    exitArea.addEventListener('click', function() {
                        const message = {
                            type: 'pip-exit'
                        };
                        try {
                            window.chrome.webview.postMessage(JSON.stringify(message));
                        } catch {}
                    });
                    
                    // 컨트롤 바 조립
                    controlBar.appendChild(dragArea);
                    controlBar.appendChild(exitArea);

                    document.body.appendChild(controlBar);
                    
                    function showControls() {
                        dragArea.innerHTML = '⋮⋮⋮ 창 이동';
                        exitArea.innerHTML = 'X';
                    }
                    
                    function hideControls() {
                        dragArea.innerHTML = '';
                        exitArea.innerHTML = '';
                    }

                    // 컨트롤 영역 호버 시 텍스트 변경
                    controlBar.addEventListener('mouseenter', function() {
                        showControls();
                    });
                    
                    controlBar.addEventListener('mouseleave', function() {
                        hideControls();
                    });
                    
                    // 마우스 움직임 감지로 컨트롤 표시 (디버깅 로그 추가)
                    document.addEventListener('mousemove', function(e) {
                        try {
                            const level3Element = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right > div.d-flex.h-space-between.layers.mb-15 > div:nth-child(1) > div:nth-child(2)');
                            
                            showControls();
                        } catch { }
                    });

                    document.addEventListener('mouseleave', function() {
                        hideControls();
                    });
                    
                    // 맵 설정 저장 기능
                    function saveMapSettings() {
                        try {
                            const mapElement = document.querySelector('#map');
                            if (mapElement && mapElement.style.transform) {
                                window.chrome.webview.postMessage(JSON.stringify({
                                    type: 'save-map-settings',
                                    transform: mapElement.style.transform
                                }));
                            }
                        } catch {}
                    }
                    
                    // 윈도우 포커스 해제 시 맵 설정 저장
                    window.addEventListener('blur', function() {
                        saveMapSettings();
                    });
                    
                    window.chrome.webview.postMessage(JSON.stringify({
                        type: 'pip-overlay-ready'
                    }));
                    
                })();";

        /// <summary>
        /// PiP 모드용 컨트롤 제거 스크립트
        /// </summary>
        public const string REMOVE_PIP_OVERLAY_SCRIPT =
            @"
                (function() {
                    // PiP 컨트롤 바 제거
                    const controlBar = document.getElementById('pip-control-bar');
                    if (controlBar) {
                        controlBar.remove();
                    }
                    
                    // PiP 스타일 제거
                    const style = document.getElementById('pip-control-style');
                    if (style) {
                        style.remove();
                    }
                })();";

        public const string TARKOV_MARGET_ELEMENT_RESTORE =
            @"
                    try {
                        // panel_left 복원
                        var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
                        if (panelLeft) {
                            panelLeft.style.display = '';
                        }

                        // panel_right 복원
                        var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
                        if (panelRight) {
                            panelRight.style.display = '';
                        }
                        
                        // panel_top 복원
                        var panelTop = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
                        if (panelTop) {
                            panelTop.style.display = '';
                        }
                        
                        // header 복원
                        var header = document.querySelector('#__nuxt > div > div > header');
                        if (header) {
                            header.style.display = '';
                        }
                        
                        // footer-wrap 복원
                        var footerWrap = document.querySelector('#__nuxt > div > div > div.footer-wrap');
                        if (footerWrap) {
                            footerWrap.style.display = '';
                        }
                        
                        // 지도 스케일링 초기화
                        var mapElement = document.querySelector('#map');
                        if (mapElement) {
                            mapElement.style.transform = '';
                            mapElement.style.transformOrigin = '';
                        }
                    } catch { }
                ";

        public const string REMOVE_TARKOV_MARGET_ELEMENT_PANNEL_LEFT =
            @"
                    try {
                        var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
                        if (panelLeft) {
                            panelLeft.style.display = 'none';
                        } else {
                        }
                    } catch { }
                ";

        public const string REMOVE_TARKOV_MARGET_ELEMENT_PANNEL_RIGHT =
            @"
                    try {
                        var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
                        if (panelRight) {
                            panelRight.style.display = 'none';
                        } else {
                        }
                    } catch { }
                ";

        public const string REMOVE_TARKOV_MARGET_ELEMENT_PANNEL_TOP =
            @"
                    try {
                        var panelTop = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
                        if (panelTop) {
                            panelTop.style.display = 'none';
                        } else {
                        }
                    } catch { }
                ";

        public const string REMOVE_TARKOV_MARGET_ELEMENT_HEADER =
            @"
                    try {
                        var header = document.querySelector('#__nuxt > div > div > header');
                        if (header) {
                            header.style.display = 'none';
                        } else {
                        }
                    } catch { }
                ";

        public const string REMOVE_TARKOV_MARGET_ELEMENT_FOOTER =
            @"
                    try {
                        var footerWrap = document.querySelector('#__nuxt > div > div > div.footer-wrap');
                        if (footerWrap) {
                            footerWrap.style.display = 'none';
                        } else {
                        }
                    } catch { }
                ";

        /// <summary>
        /// 큰 창 크기일 때 요소들을 복원하는 스크립트 (지도 스케일은 유지)
        /// </summary>
        public const string RESTORE_ELEMENTS_FOR_LARGE_SIZE =
            @"
                    try {
                        
                        // panel_left 복원
                        var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
                        if (panelLeft) {
                            panelLeft.style.display = '';
                        }

                        // panel_right 복원
                        var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
                        if (panelRight) {
                            panelRight.style.display = '';
                        }
                        
                        // panel_top 복원
                        var panelTop = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
                        if (panelTop) {
                            panelTop.style.display = '';
                        }
                        
                        // header 복원
                        var header = document.querySelector('#__nuxt > div > div > header');
                        if (header) {
                            header.style.display = '';
                        }
                        
                    } catch { }
                ";

        /// <summary>
        /// 작은 창 크기일 때 요소들을 숨기는 스크립트
        /// </summary>
        public const string HIDE_ELEMENTS_FOR_SMALL_SIZE =
            @"
                    try {
                        
                        // panel_left 숨김
                        var panelLeft = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_left');
                        if (panelLeft) {
                            panelLeft.style.display = 'none';
                        }

                        // panel_right 숨김
                        var panelRight = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_right');
                        if (panelRight) {
                            panelRight.style.display = 'none';
                        }
                        
                        // panel_top 숨김
                        var panelTop = document.querySelector('#__nuxt > div > div > div.page-content > div > div > div.panel_top');
                        if (panelTop) {
                            panelTop.style.display = 'none';
                        }
                        
                        // header 숨김
                        var header = document.querySelector('#__nuxt > div > div > header');
                        if (header) {
                            header.style.display = 'none';
                        }
                        
                    } catch { }
                ";

        /// <summary>
        /// 현재 요소들의 표시/숨김 상태를 확인하는 스크립트
        /// </summary>
        public const string CHECK_ELEMENTS_VISIBILITY_STATUS =
            @"
                    (function() {
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
                        } catch { }
                    })();
                ";
    }
}
