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
})();
