/**
 * 사이트 지도 위에 앱의 내 위치 표시(원과 방향 삼각형)를 그린다.
 *
 * INTENT
 * 모양은 Local 미니맵의 내 위치(viewer/style.css의 .marker와 .triangle-indicator)와 같다. 같은 스크린샷이 두
 * 모드에서 같은 모양으로 보이도록 크기, 색, 삼각형의 자리, 핑을 그대로 옮긴다. 붙일 컨테이너와 자리, 각도는
 * Pilot 브리지가 넘긴 locate()가 사이트의 좌표 변환으로 구하고, 이 스크립트는 사이트의 내부를 모른다.
 * 사이트는 끌기, 휠, 회전과 창 크기 변경마다 지도를 다시 그리지만 그 사건을 밖에 알리지 않는다. 그래서 브리지가
 * 위치를 계속 따라가라고 하는 동안(alive()) 매 프레임 자리를 다시 구하고, 값이 바뀐 프레임에만 DOM에 쓴다.
 * 새 문서뿐 아니라 SPA 이동, DOM 교체와 반복 주입에서도 표시와 갱신 루프가 하나만 살아 있도록 복구한다.
 */
(function () {
    'use strict';
    const version = 7;
    // 공개 진입점이 지워져도 기존 표시를 찾는다. 새 표시가 다른 자리를 번갈아 그리는 일을 막는다.
    const stateKey = Symbol.for('TanukiTarkovMap.positionMarker');
    const existing = document[stateKey];
    if (existing?.version === version && typeof existing.ensure === 'function'
        && typeof existing.show === 'function') {
        window.tanukiMarker = existing;
        return existing.ensure();
    }
    existing?.dispose?.();

    const MARKER_CLASS = 'tanuki-position';
    const DIRECTION_CLASS = 'tanuki-direction';

    // viewer/style.css의 --marker-size와 같은 지름(테두리 포함)이다
    const MARKER_SIZE = 20;
    const DIRECTION_SIZE = 25;

    // INTENT
    // 원은 위치를, 원 바로 앞의 삼각형은 시선 방향을 나타낸다. 삼각형 중심을 원 중심에서 시선 방향으로 밀어
    // 삼각형 도형의 뒤쪽 끝과 원(바깥 반지름 10px) 사이에 1px만 남긴다. 정사각형 그림의 아래쪽 투명 여백
    // (높이의 25%)도 거리에 넣는다. 핑으로 원이 잠시 커지는 동안에는 삼각형이 원 위에 겹친다(Local과 같다).
    const DIRECTION_OFFSET = DIRECTION_SIZE * 0.25 + MARKER_SIZE / 2 + 1;
    const TRIANGLE_MARKUP = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100">'
        + '<path d="M50,5 L85,75 Q50,45 15,75 Z" fill="#8a2be2" stroke="#70a800" stroke-width="2"/></svg>';

    // 핑은 0.7초 동안 지름을 2.5배까지 키웠다 되돌린다(viewer/style.css의 markerSizeAnimation).
    // CSS 애니메이션처럼 구간마다 ease-in-out을 건다.
    const PING_KEYFRAMES = [
        { width: MARKER_SIZE + 'px', height: MARKER_SIZE + 'px', easing: 'ease-in-out' },
        { width: MARKER_SIZE * 2.5 + 'px', height: MARKER_SIZE * 2.5 + 'px', easing: 'ease-in-out' },
        { width: MARKER_SIZE + 'px', height: MARKER_SIZE + 'px' }
    ];
    const PING_DURATION = 700;

    // INTENT
    // 사이트 스타일이 덮어쓰지 못하게 인라인 important로 둔다. 스타일시트를 따로 두면 그것까지 복구해야 한다.
    // 원의 폭과 높이만 important 없이 둔다. important 선언은 애니메이션보다 앞서므로 핑이 보이지 않게 된다.
    // 층은 사이트의 내 위치 캔버스(players-canvas, z-index 7) 위, 마커 설명 창(popup-layer, 10) 아래다.
    const markerStyle = {
        position: 'absolute', left: '0', top: '0', right: 'auto', bottom: 'auto', margin: '0', padding: '0',
        'box-sizing': 'border-box', border: '2px solid #70a800', 'border-radius': '50%', background: '#8a2be2',
        'z-index': '8', 'pointer-events': 'none', transition: 'none', display: 'none'
    };
    const directionStyle = {
        position: 'absolute', left: '50%', top: '50%', width: DIRECTION_SIZE + 'px', height: DIRECTION_SIZE + 'px',
        margin: '0', padding: '0', border: '0',
        'background-image': `url("data:image/svg+xml;utf8,${encodeURIComponent(TRIANGLE_MARKUP)}")`,
        'background-repeat': 'no-repeat', 'background-size': '100% 100%', 'pointer-events': 'none',
        'transform-origin': '50% 50%', transition: 'none'
    };

    // 브리지가 맡긴 표시. locate는 매 프레임 붙일 컨테이너와 자리, 화면 각도를 돌려주고(그리지 않을 때는 null),
    // alive는 그 위치를 계속 따라갈지 알려 준다.
    let target = null;
    let marker = null;
    let direction = null;
    let displayed = false;
    // 마지막으로 쓴 transform. 매 프레임 불리므로 값이 바뀐 프레임에만 DOM에 써서 다시 그리게 한다.
    let shownMarker = null;
    let shownDirection = null;
    let ping = null;
    let frame = 0;
    let disposed = false;
    let pose = { shown: false, x: null, y: null, angle: null };

    function setStyles(element, styles) {
        for (const [name, value] of Object.entries(styles)) element.style.setProperty(name, value, 'important');
    }

    function create() {
        marker = document.createElement('div');
        marker.className = MARKER_CLASS;
        setStyles(marker, markerStyle);
        marker.style.width = MARKER_SIZE + 'px';
        marker.style.height = MARKER_SIZE + 'px';
        direction = document.createElement('div');
        direction.className = DIRECTION_CLASS;
        setStyles(direction, directionStyle);
        marker.append(direction);
    }

    function hide() {
        if (marker && displayed) marker.style.setProperty('display', 'none', 'important');
        displayed = false;
        pose = { shown: false, x: null, y: null, angle: null };
    }

    // 맡은 함수가 던지더라도 갱신 루프와 앱의 점검 호출이 함께 멈추지 않게, 그리지 않는 것으로 다룬다
    function placeOf(current) {
        try { return current.locate(); }
        catch (error) { return null; }
    }

    function update() {
        const place = target ? placeOf(target) : null;
        if (!place?.container?.isConnected) { hide(); return; }
        if (!marker) create();
        if (marker.parentElement !== place.container) {
            place.container.appendChild(marker);
            shownMarker = null;
            shownDirection = null;
        }
        const markerTransform = `translate(${place.x}px, ${place.y}px) translate(-50%, -50%)`;
        if (markerTransform !== shownMarker) {
            marker.style.setProperty('transform', markerTransform, 'important');
            shownMarker = markerTransform;
        }
        const directionTransform = `translate(-50%, -50%) rotate(${place.angle}deg) translateY(${-DIRECTION_OFFSET}px)`;
        if (directionTransform !== shownDirection) {
            direction.style.setProperty('transform', directionTransform, 'important');
            shownDirection = directionTransform;
        }
        if (!displayed) {
            marker.style.setProperty('display', 'block', 'important');
            displayed = true;
        }
        pose = { shown: true, x: place.x, y: place.y, angle: place.angle };
    }

    // INTENT
    // 브리지가 위치를 계속 따라가라고 하는 동안 매 프레임 따라간다. 끝났다고 하면(맵 이동, 지도 제거) 맡은
    // 표시를 버리고 루프를 멈추며, 브리지가 새 지도에 다시 맡기면(show) 시작한다. 앱의 2초 점검(ensure)은
    // 지워진 표시를 다시 붙이고 멈춘 루프를 살린다.
    function following() {
        if (!target) return false;
        let alive;
        try { alive = target.alive() === true; }
        catch (error) { alive = false; }
        if (!alive) { target = null; hide(); }
        return alive;
    }

    function tick() {
        frame = 0;
        if (disposed) return;
        update();
        if (following()) frame = requestAnimationFrame(tick);
    }

    function start() {
        if (!frame && !disposed) frame = requestAnimationFrame(tick);
    }

    // 사이트와 Local처럼 새 위치를 받으면 한 번 크게 깜빡인다. 앞선 핑이 남아 있으면 처음부터 다시 한다.
    function startPing() {
        ping?.cancel();
        ping = typeof marker.animate === 'function' ? marker.animate(PING_KEYFRAMES, PING_DURATION) : null;
    }

    // 표시가 지도에 붙어 화면에 그려지는지. 지도를 옮겨 화면 밖에 있어도 그려진 것으로 본다.
    function isShown() {
        if (!displayed || !marker?.isConnected) return false;
        const box = marker.getBoundingClientRect();
        return box.width > 0 && box.height > 0
            && (typeof marker.checkVisibility !== 'function' || marker.checkVisibility());
    }

    // next: { locate, alive, ping }. 지금 보이면 true. ping이면 보인 순간 한 번 깜빡인다.
    function show(next) {
        if (disposed || typeof next?.locate !== 'function' || typeof next.alive !== 'function') return false;
        target = { locate: next.locate, alive: next.alive };
        update();
        start();
        const visible = isShown();
        if (visible && next.ping) startPing();
        return visible;
    }

    function ensure() {
        if (disposed || !document.body) return false;
        if (following()) { update(); start(); }
        return true;
    }

    window.tanukiMarker = {
        version, ensure, show, isShown,
        // 검사 도구가 표시의 자리(지도 컨테이너 기준)와 삼각형의 화면 각도를 확인한다
        pose: () => ({ ...pose }),
        dispose() {
            disposed = true;
            if (frame) cancelAnimationFrame(frame);
            frame = 0;
            ping?.cancel();
            marker?.remove();
            window.removeEventListener('pageshow', ensure);
            window.removeEventListener('popstate', ensure);
            if (window.tanukiMarker?.ensure === ensure) delete window.tanukiMarker;
            if (document[stateKey]?.ensure === ensure) delete document[stateKey];
        }
    };
    Object.defineProperty(document, stateKey, { value: window.tanukiMarker, configurable: true });
    window.addEventListener('pageshow', ensure);
    window.addEventListener('popstate', ensure);
    return ensure();
})();
