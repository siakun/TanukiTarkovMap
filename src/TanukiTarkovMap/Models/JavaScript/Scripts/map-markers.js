/**
 * 방향 표시는 앱이 읽은 스크린샷의 회전값으로 그린다.
 * 페이지의 마커 회전값이 없거나 달라져도 위치를 관리하는 사이트의 스타일은 덮어쓰지 않는다.
 * 새 문서뿐 아니라 SPA 이동, DOM 교체와 반복 주입에서도 감시자가 하나만 살아 있도록 복구한다.
 */
(function () {
    'use strict';
    const version = 5;
    // 공개 진입점이 지워져도 기존 감시자를 찾는다. 새 감시자가 다른 각도를 번갈아 쓰는 일을 막는다.
    const stateKey = Symbol.for('TanukiTarkovMap.direction');
    const existing = document[stateKey];
    if (existing?.version === version && typeof existing.ensure === 'function'
        && typeof existing.setHeading === 'function') {
        window.tanukiDirection = existing;
        return existing.ensure();
    }
    existing?.dispose?.();

    const svgDataUrl = 'data:image/svg+xml;utf8,%0A%20%20%20%20%20%20%20%20%20%20%20%20%3Csvg%20xmlns%3D%22http%3A%2F%2Fwww.w3.org%2F2000%2Fsvg%22%20viewBox%3D%220%200%20100%20100%22%3E%0A%20%20%20%20%20%20%20%20%20%20%20%20%20%20%20%20%3Cpath%20d%3D%22M50%2C5%20L85%2C75%20Q50%2C45%2015%2C75%20Z%22%20fill%3D%22%238a2be2%22%20stroke%3D%22%2370a800%22%20stroke-width%3D%222%22%2F%3E%0A%20%20%20%20%20%20%20%20%20%20%20%20%3C%2Fsvg%3E';
    const styleId = 'triangle-indicator-style';
    // 이전 버전의 감시자를 교체할 때 CSS도 함께 교체한다.
    document.getElementById(styleId)?.remove();
    let observer = null;
    let observedRoot = null;
    let pending = false;
    let heading = null;
    let headingPath = null;
    let disposed = false;

    // INTENT
    // 원은 위치를, 원 바로 앞의 화살표는 시선 방향을 나타낸다. 부모 마커를 움직이면
    // 지도 좌표가 어긋나므로 화살표만 원 중심에 정렬하고, 회전하는 축을 따라 밖으로 뺀다.
    // 겹침을 피하되 두 표시가 따로 놀지 않게 붙인다. 정사각형 SVG의 아래쪽 투명 여백은
    // 거리에서 빼고, 실제 화살표 도형의 뒤쪽 끝과 원 사이에 작은 간격만 둔다.
    const styleText = `
                .triangle-indicator {
                    position: absolute !important;
                    top: 50% !important;
                    left: 50% !important;
                    width: 25px !important;
                    height: 25px !important;
                    background-image: url('${svgDataUrl}') !important;
                    background-repeat: no-repeat !important;
                    background-size: 100% 100% !important;
                    pointer-events: none !important;
                    z-index: 9999 !important;
                    transform: translate(-50%, -50%) rotate(var(--tanuki-direction, 0deg))
                        translateY(calc(-25% - var(--tanuki-marker-radius) - 1px)) !important;
                    transform-origin: 50% 50% !important;
                }`;

    function ensure() {
        if (disposed) return false;
        const root = document.documentElement;
        if (!root) return false;
        if (observedRoot !== root) {
            observer?.disconnect();
            observer = new MutationObserver(() => {
                if (pending) return;
                pending = true;
                queueMicrotask(() => { pending = false; ensure(); });
            });
            observer.observe(root, { childList: true, subtree: true, attributes: true,
                characterData: true, attributeFilter: ['class', 'style', 'disabled', 'media'] });
            observedRoot = root;
        }
        if (!document.head || !document.body) return false;
        if (headingPath !== location.pathname) heading = null;

        let style = document.getElementById(styleId);
        if (style?.tagName !== 'STYLE') {
            style?.remove();
            style = document.createElement('style');
            style.id = styleId;
            document.head.appendChild(style);
        }
        // ID가 남아 있어도 스타일이 비거나 꺼지면 화살표는 보이지 않는다.
        // 매번 덮어쓰면 감시자가 자신을 다시 깨우므로 달라진 값만 복구한다.
        if (style.textContent !== styleText || style.sheet?.cssRules.length === 0)
            style.textContent = styleText;
        if (style.disabled) style.disabled = false;
        if (style.hasAttribute('media')) style.removeAttribute('media');

        // 위치 마커가 없으면 기다린다. 지도 컨테이너에 대체 화살표를 만들지 않는다.
        const mapHeading = heading === null ? null : window.tanukiPilot?.getMapHeading?.(heading);
        document.querySelectorAll('.marker').forEach(marker => {
            let triangle = marker.querySelector('.triangle-indicator');
            if (!triangle) {
                triangle = document.createElement('div');
                triangle.className = 'triangle-indicator';
                marker.appendChild(triangle);
            }
            // 부모의 회전은 그대로 두고 자식 화살표에 차이만 적용해 이중 회전을 막는다.
            const matrix = new DOMMatrixReadOnly(getComputedStyle(marker).transform);
            const parentAngle = Math.atan2(matrix.b, matrix.a) * 180 / Math.PI;
            const correction = mapHeading == null ? 0 : mapHeading - parentAngle;
            const value = correction + 'deg';
            if (triangle.style.getPropertyValue('--tanuki-direction') !== value)
                triangle.style.setProperty('--tanuki-direction', value);
            // 부모의 확대/축소가 함께 적용되므로 화면 크기가 아닌 마커 자체의 반지름을 사용한다.
            const radius = Math.max(marker.offsetWidth, marker.offsetHeight) / 2 + 'px';
            if (triangle.style.getPropertyValue('--tanuki-marker-radius') !== radius)
                triangle.style.setProperty('--tanuki-marker-radius', radius);
        });
        return true;
    }

    function setHeading(value) {
        if (!Number.isFinite(value)) return false;
        heading = value;
        headingPath = location.pathname;
        return ensure() && window.tanukiPilot?.getMapHeading?.(value) != null;
    }

    window.tanukiDirection = {
        version, ensure, setHeading,
        dispose() {
            disposed = true;
            observer?.disconnect();
            window.removeEventListener('pageshow', ensure);
            window.removeEventListener('popstate', ensure);
            document.removeEventListener('DOMContentLoaded', ensure);
            if (window.tanukiDirection?.ensure === ensure) delete window.tanukiDirection;
            if (document[stateKey]?.ensure === ensure) delete document[stateKey];
        }
    };
    Object.defineProperty(document, stateKey, { value: window.tanukiDirection, configurable: true });
    window.addEventListener('pageshow', ensure);
    window.addEventListener('popstate', ensure);
    document.addEventListener('DOMContentLoaded', ensure);
    return ensure();
})();
