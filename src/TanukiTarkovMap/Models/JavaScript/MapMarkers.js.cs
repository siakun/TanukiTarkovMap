namespace TanukiTarkovMap.Models.JavaScript
{
    /// <summary>
    /// 맵 마커 및 방향 표시기 관련 JavaScript 스크립트
    /// - 플레이어 위치에 방향 표시 삼각형 추가
    /// - 마커 회전에 따른 방향 표시기 회전
    /// </summary>
    public static class MapMarkers
    {
        /// <summary>
        /// 방향 표시기를 추가하는 스크립트
        ///
        /// 실행 절차:
        /// 1. SVG 기반 삼각형 아이콘을 CSS 스타일로 정의
        /// 2. 모든 마커(.marker) 요소를 찾아서 삼각형 추가
        /// 3. 마커의 transform rotate 값을 읽어 삼각형도 같은 각도로 회전
        /// 4. MutationObserver로 새로 추가되는 마커 감지 및 자동 처리
        /// 5. 2초 후 초기 마커 초기화 실행 (DOM 로딩 대기)
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
    }
}
