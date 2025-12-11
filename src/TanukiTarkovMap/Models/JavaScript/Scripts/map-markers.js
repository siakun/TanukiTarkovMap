/**
 * 맵 마커 방향 표시기 스크립트
 *
 * 목적:
 * - 플레이어 마커에 방향을 나타내는 삼각형 추가
 * - 마커 회전에 따라 삼각형도 함께 회전
 *
 * JavaScript 개념:
 * - SVG Data URL: 이미지를 문자열로 인코딩
 * - CSS background-image: 배경 이미지 설정
 * - transform: CSS 회전 변환
 */

(function () {
    'use strict';  // 엄격 모드: 더 안전한 JavaScript 코드 작성

    // ========================================
    // SVG 삼각형 아이콘 (Data URL 형식)
    // ========================================
    // Data URL: 이미지 파일 대신 문자열로 인코딩된 이미지
    // 장점: 별도 파일 필요 없음, 빠른 로딩
    const svgDataUrl = 'data:image/svg+xml;utf8,%0A%20%20%20%20%20%20%20%20%20%20%20%20%3Csvg%20xmlns%3D%22http%3A%2F%2Fwww.w3.org%2F2000%2Fsvg%22%20viewBox%3D%220%200%20100%20100%22%3E%0A%20%20%20%20%20%20%20%20%20%20%20%20%20%20%20%20%3Cpath%20d%3D%22M50%2C5%20L85%2C75%20Q50%2C45%2015%2C75%20Z%22%20fill%3D%22%238a2be2%22%20stroke%3D%22%2370a800%22%20stroke-width%3D%222%22%2F%3E%0A%20%20%20%20%20%20%20%20%20%20%20%20%3C%2Fsvg%3E';

    /**
     * CSS 스타일 삽입
     *
     * <style> 태그를 만들어서 <head>에 추가합니다.
     * 이렇게 하면 모든 .triangle-indicator 클래스에 스타일이 적용됩니다.
     */
    function injectStyle() {
        const style = document.createElement('style');
        style.id = 'triangle-indicator-style';

        // 백틱(`)을 사용한 템플릿 리터럴:
        // 여러 줄 문자열을 쉽게 작성할 수 있고, ${변수} 형태로 값을 삽입할 수 있습니다
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

        // 기존 스타일이 있으면 제거 (중복 방지)
        const existingStyle = document.getElementById('triangle-indicator-style');
        if (existingStyle) existingStyle.remove();

        document.head.appendChild(style);
    }

    /**
     * 마커에 삼각형 추가
     *
     * @param {HTMLElement} marker - 삼각형을 추가할 마커 요소
     */
    function addTriangleToMarker(marker) {
        // 이미 삼각형이 있으면 중복 추가 방지
        if (marker.querySelector('.triangle-indicator')) {
            return;
        }

        // 삼각형 div 생성
        const triangle = document.createElement('div');
        triangle.className = 'triangle-indicator';

        // 마커를 relative로 설정해야 삼각형이 마커 내부에 위치합니다
        marker.style.position = 'relative';

        // ========================================
        // 마커의 회전 각도 읽기
        // ========================================
        // getComputedStyle: 실제 적용된 CSS 값 가져오기
        const computed = window.getComputedStyle(marker);
        const transform = computed.transform;

        // transform 값이 있으면 각도 추출
        // 예: "rotate(45deg)" → 45
        if (transform && transform !== 'none') {
            // match(): 정규식으로 문자열에서 패턴 찾기
            // /rotate\(([\-\d.]+)deg\)/ : "rotate(숫자deg)" 패턴
            const match = transform.match(/rotate\(([\-\d.]+)deg\)/);
            if (match) {
                // parseFloat: 문자열을 숫자로 변환
                const angle = parseFloat(match[1]);

                // 삼각형도 같은 각도로 회전
                // translate는 위치 조정, rotate는 회전
                triangle.style.transform = `translate(-50%, -65%) rotate(${angle}deg)`;
            }
        }

        // 삼각형을 마커의 자식으로 추가
        marker.appendChild(triangle);
    }

    /**
     * 페이지 로딩 시 모든 마커 초기화
     */
    function initMarkers() {
        // 모든 .marker 클래스 요소 찾기
        const markers = document.querySelectorAll('.marker');

        if (markers.length === 0) {
            // .marker가 없으면 다른 선택자 시도
            const altMarkers = document.querySelectorAll('#map > div');
            altMarkers.forEach(addTriangleToMarker);
        } else {
            // forEach: 배열의 각 요소에 대해 함수 실행
            // 화살표 함수 (=>): 간단한 함수 표현
            markers.forEach(addTriangleToMarker);
        }
    }

    // ========================================
    // 초기 실행
    // ========================================
    injectStyle();

    // ========================================
    // MutationObserver: 새로 추가되는 마커 감지
    // ========================================
    // 맵이 동적으로 로딩되므로 새 마커가 추가될 때마다 삼각형을 붙입니다

    // 감시할 컨테이너 찾기 (없으면 body)
    const container = document.querySelector('#map') ||
                     document.querySelector('#map-layer') ||
                     document.body;

    // MutationObserver 생성
    // 화살표 함수 사용: (매개변수) => { 코드 }
    const observer = new MutationObserver(mutations => {
        // for...of: 배열을 순회하는 최신 방법
        for (const mutation of mutations) {
            if (mutation.type === 'childList') {
                mutation.addedNodes.forEach(node => {
                    // instanceof: 객체 타입 확인
                    if (!(node instanceof HTMLElement)) return;

                    // 추가된 노드가 마커인지 확인
                    if (node.classList && node.classList.contains('marker')) {
                        addTriangleToMarker(node);
                    } else {
                        // 하위 요소 중 마커가 있는지 확인
                        node.querySelectorAll('.marker, #map > div').forEach(addTriangleToMarker);
                    }
                });
            }
        }
    });

    // 감시 시작
    observer.observe(container, {
        childList: true,  // 자식 추가/제거 감시
        subtree: true,    // 모든 하위 요소 감시
    });

    // ========================================
    // 페이지 로딩 대기 후 초기화
    // ========================================
    // setTimeout: 2000ms (2초) 후에 함수 실행
    // 웹페이지가 완전히 로드될 때까지 기다립니다
    setTimeout(initMarkers, 2000);

    // ========================================
    // 오른쪽 클릭을 왼쪽 클릭으로 변환
    // ========================================
    /**
     * marker 요소의 왼쪽 클릭으로 오른쪽 클릭 메뉴 열기
     *
     * @param {HTMLElement} element - 클릭 변환을 적용할 요소
     */
    function convertRightClickToLeftClick(element) {
        // 이미 변환이 적용되었는지 확인 (중복 방지)
        if (element.dataset.clickConverted === 'true') {
            return;
        }


        // 원본 contextmenu 이벤트 리스너들을 저장
        const originalListeners = [];

        // contextmenu 이벤트 리스너 가로채기
        const originalAddEventListener = element.addEventListener;
        element.addEventListener = function(type, listener, options) {
            if (type === 'contextmenu') {
                originalListeners.push({ listener, options });
            }
            return originalAddEventListener.call(this, type, listener, options);
        };

        // 왼쪽 클릭을 우클릭 contextmenu 핸들러로 연결
        element.addEventListener('click', function (e) {
            // 기본 왼쪽 클릭 동작 차단
            e.preventDefault();
            e.stopPropagation();
            e.stopImmediatePropagation();

            // 우클릭 이벤트 생성 (isTrusted를 우회하기 위해 실제 마우스 위치 사용)
            const rect = element.getBoundingClientRect();
            const x = e.clientX || rect.left + rect.width / 2;
            const y = e.clientY || rect.top + rect.height / 2;

            // 저장된 contextmenu 리스너들을 직접 호출
            if (originalListeners.length > 0) {
                const fakeEvent = {
                    type: 'contextmenu',
                    button: 2,
                    buttons: 2,
                    clientX: x,
                    clientY: y,
                    screenX: e.screenX || x,
                    screenY: e.screenY || y,
                    target: element,
                    currentTarget: element,
                    preventDefault: () => {},
                    stopPropagation: () => {},
                    stopImmediatePropagation: () => {}
                };

                originalListeners.forEach(({ listener }) => {
                    try {
                        listener.call(element, fakeEvent);
                    } catch (err) {
                        // Ignore errors
                    }
                });
            } else {
                // contextmenu 이벤트 발생
                const contextMenuEvent = new MouseEvent('contextmenu', {
                    bubbles: true,
                    cancelable: true,
                    view: window,
                    button: 2,
                    buttons: 2,
                    clientX: x,
                    clientY: y,
                    screenX: e.screenX || x,
                    screenY: e.screenY || y
                });

                // 여러 대상에 이벤트 발생
                element.dispatchEvent(contextMenuEvent);

                // 부모 요소들에도 시도
                let parent = element.parentElement;
                while (parent && parent !== document.body) {
                    parent.dispatchEvent(contextMenuEvent);
                    parent = parent.parentElement;
                }

                // document에도 발생
                document.dispatchEvent(contextMenuEvent);
            }
        }, true);

        // addEventListener 복원
        setTimeout(() => {
            element.addEventListener = originalAddEventListener;
        }, 100);

        // 변환 완료 표시
        element.dataset.clickConverted = 'true';
    }

    /**
     * 모든 marker에 클릭 변환 적용
     */
    function initClickConverter() {
        // class 이름과 id 패턴 모두 검색
        const markers = document.querySelectorAll('.marker, [id^="marker_"]');

        // document의 contextmenu 이벤트를 가로채기
        const originalDocumentListener = document.addEventListener;
        let documentContextMenuListeners = [];

        document.addEventListener = function(type, listener, options) {
            if (type === 'contextmenu') {
                documentContextMenuListeners.push({ listener, options });
            }
            return originalDocumentListener.call(this, type, listener, options);
        };

        markers.forEach(convertRightClickToLeftClick);

        // 드래그 감지를 위한 변수
        let isDragging = false;
        let mouseDownPos = { x: 0, y: 0 };

        document.addEventListener('mousedown', function(e) {
            mouseDownPos = { x: e.clientX, y: e.clientY };
            isDragging = false;
        }, true);

        document.addEventListener('mousemove', function(e) {
            if (mouseDownPos.x !== 0 || mouseDownPos.y !== 0) {
                const deltaX = Math.abs(e.clientX - mouseDownPos.x);
                const deltaY = Math.abs(e.clientY - mouseDownPos.y);
                // 5픽셀 이상 움직이면 드래그로 간주
                if (deltaX > 5 || deltaY > 5) {
                    isDragging = true;
                }
            }
        }, true);

        document.addEventListener('mouseup', function(e) {
            // mouseup 직후 잠시 대기 후 드래그 상태 리셋
            setTimeout(() => {
                isDragging = false;
                mouseDownPos = { x: 0, y: 0 };
            }, 100);
        }, true);

        // 전역 클릭 리스너 추가
        document.addEventListener('click', function(e) {
            // 드래그 중이면 무시
            if (isDragging) {
                return;
            }
            // marker 요소나 그 자식인지 확인
            let target = e.target;
            let isMarkerClick = false;
            let isMenuClick = false;

            // 클릭한 요소가 marker인지, 메뉴인지 확인
            let checkTarget = target;
            while (checkTarget) {
                if (checkTarget.id && checkTarget.id.startsWith('marker_')) {
                    isMarkerClick = true;
                    break;
                }
                checkTarget = checkTarget.parentElement;
            }

            // 메뉴 내부 클릭인지 확인
            // 메뉴는 매우 높은 z-index를 가진 오버레이 요소여야 함
            checkTarget = target;
            while (checkTarget && checkTarget !== document.body) {
                const style = window.getComputedStyle(checkTarget);
                const position = style.position;
                const zIndex = parseInt(style.zIndex) || 0;
                const className = checkTarget.className || '';

                // 메뉴 판별 조건:
                // 1. 클래스 이름에 'menu', 'popup', 'modal', 'dropdown', 'context'가 포함
                // 2. OR z-index가 1000 이상 (일반 맵 요소는 보통 낮은 z-index)
                // 3. AND position이 absolute 또는 fixed
                if ((position === 'absolute' || position === 'fixed')) {
                    const hasMenuClass = typeof className === 'string' &&
                        (className.includes('menu') ||
                         className.includes('popup') ||
                         className.includes('modal') ||
                         className.includes('dropdown') ||
                         className.includes('context'));

                    const hasHighZIndex = zIndex >= 1000;

                    // 클래스 이름에 menu 관련 단어가 있거나 z-index가 1000 이상이면 메뉴
                    if (hasMenuClass || hasHighZIndex) {
                        isMenuClick = true;
                        break;
                    }
                }

                checkTarget = checkTarget.parentElement;
            }

            if (isMarkerClick) {
                // marker 클릭 - 메뉴 열기
                // 방법 1: React Fiber를 통한 핸들러 찾기
                const fiberKey = Object.keys(target).find(key => key.startsWith('__react'));
                if (fiberKey) {
                    const fiber = target[fiberKey];

                    // React props에서 onContextMenu 찾기
                    if (fiber && fiber.memoizedProps && fiber.memoizedProps.onContextMenu) {
                        const fakeEvent = {
                            type: 'contextmenu',
                            button: 2,
                            buttons: 2,
                            clientX: e.clientX,
                            clientY: e.clientY,
                            target: target,
                            currentTarget: target,
                            preventDefault: () => {},
                            stopPropagation: () => {},
                            nativeEvent: e
                        };
                        fiber.memoizedProps.onContextMenu(fakeEvent);
                        e.preventDefault();
                        e.stopPropagation();
                        return;
                    }
                }

                // 방법 2: 모든 이벤트 타입 시도
                ['contextmenu', 'mousedown', 'mouseup', 'pointerdown', 'pointerup'].forEach(eventType => {
                    const event = new PointerEvent(eventType, {
                        bubbles: true,
                        cancelable: true,
                        view: window,
                        button: 2,
                        buttons: 2,
                        clientX: e.clientX,
                        clientY: e.clientY,
                        pointerId: 1,
                        pointerType: 'mouse'
                    });
                    target.dispatchEvent(event);
                });

                // 방법 3: document에도 발생
                const contextEvent = new PointerEvent('contextmenu', {
                    bubbles: true,
                    cancelable: true,
                    view: window,
                    button: 2,
                    buttons: 2,
                    clientX: e.clientX,
                    clientY: e.clientY,
                    pointerId: 1,
                    pointerType: 'mouse'
                });
                document.dispatchEvent(contextEvent);

                e.preventDefault();
                e.stopPropagation();
            } else if (isMenuClick) {
                // 메뉴 내부 클릭 - 아무것도 하지 않음 (메뉴 항목 선택 허용)
            } else {
                // marker도 메뉴도 아닌 곳 클릭 - ESC 키로 메뉴 닫기
                // ESC 키 이벤트 발생
                const escapeEvent = new KeyboardEvent('keydown', {
                    key: 'Escape',
                    code: 'Escape',
                    keyCode: 27,
                    which: 27,
                    bubbles: true,
                    cancelable: true
                });

                document.dispatchEvent(escapeEvent);
                window.dispatchEvent(escapeEvent);
                document.body.dispatchEvent(escapeEvent);
            }
        }, true);
    }

    // 클릭 변환 초기화
    setTimeout(initClickConverter, 2000);

    // MutationObserver에도 클릭 변환 추가
    const clickObserver = new MutationObserver(mutations => {
        for (const mutation of mutations) {
            if (mutation.type === 'childList') {
                mutation.addedNodes.forEach(node => {
                    if (!(node instanceof HTMLElement)) return;

                    // class 또는 id 패턴으로 marker 확인
                    if ((node.classList && node.classList.contains('marker')) ||
                        (node.id && node.id.startsWith('marker_'))) {
                        convertRightClickToLeftClick(node);
                    } else {
                        // 하위 요소에서도 검색
                        node.querySelectorAll('.marker, [id^="marker_"]').forEach(convertRightClickToLeftClick);
                    }
                });
            }
        }
    });

    clickObserver.observe(container, {
        childList: true,
        subtree: true
    });
})();
