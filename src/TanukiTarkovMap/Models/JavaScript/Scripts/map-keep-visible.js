/**
 * 맵 이동 제한 스크립트
 *
 * 목적: 맵을 끌다가 화면에서 놓치지 않게 한다.
 *
 * 사이트가 쓰는 것 (번들에서 확인):
 *   panzoom(.map-wrap, { autocenter: true, bounds: true, smoothScroll: false, ... })
 *
 * - anvaka/panzoom 이다. mousedown 때 커서 위치를 기억하고, mousemove마다
 *   dx = clientX - 기억한 위치 만큼 맵을 옮긴 뒤 그 위치를 갱신한다. 즉 커서와 1:1로 움직이며,
 *   배율이 달라도 화면 픽셀 기준이라 환산이 필요 없다 (실측: 커서 100px -> 맵 100px).
 * - smoothScroll: false 라 손을 뗀 뒤 관성이 없다 (실측: 놓은 뒤 이동 0px).
 * - bounds: true 라 사이트에도 경계가 있다. 다만 그 기준이 맵 문서 상자이고 남기는 양이 창의
 *   5%뿐이라(실측: 오른쪽 끝 60.6px = 1211의 5%), 가장자리가 빈 여백인 맵은 화면에 아무것도
 *   없는 것처럼 된다. 그래서 우리가 더 엄한 규칙을 얹는다.
 *
 * 우리 규칙: 화면 한가운데에는 언제나 맵이 있어야 한다.
 *
 * 무엇을 "맵"으로 보는가: .map-wrap 상자가 아니라 그 안에 실제로 그려진 영역이다.
 * 상자는 맵 문서 전체(예: Ground Zero는 2800x3100)이고 그림은 그 안의 일부(800x1100)뿐이라,
 * 상자를 기준으로 막으면 상자가 가운데를 덮은 채로 그림만 화면 밖으로 나간다. 실제로 겪은
 * 증상이 그것이다. 그래서 svg 자식들의 bbox를 합쳐 그림의 범위를 구하고, 캔버스 전체를 덮는
 * 배경은 뺀다.
 *
 * 어떻게 얹는가: 사이트는 "마지막으로 본 커서 위치"와의 차이로만 움직이므로, 우리가 보여 주는
 * 커서 위치를 조절하면 그만큼만 움직인다. 넘치는 만큼은 offset에 쌓아 두고 커서가 돌아올 때
 * 그대로 상쇄한다. 그래서 경계에서 멈춘 뒤 방향을 바꾸면 즉시 따라온다.
 *
 * 하면 안 되는 것: transform을 우리가 직접 쓰는 것. 바닥 맵은 canvas에 그려지고 마커는
 * transform으로 움직이는데 둘 다 사이트 상태에서 나오므로, 우리가 고치면 두 층이 어긋난다.
 * 합성 드래그로 되돌리는 애니메이션도 만들지 않는다. 사이트가 기억하는 커서 위치와 실제 커서가
 * 어긋나 튀는 동작이 반복해서 나왔다.
 */

(function () {
    'use strict';

    var WRAP_SELECTOR = '.map-wrap';
    var CONTAINER_SELECTOR = '.pan.map-cont';

    // 우리가 보낸 이벤트를 우리가 다시 가로채지 않기 위한 표시.
    // 이 표시가 없으면 우리 처리기가 자기 이벤트를 또 줄여 막아 사이트에 아무것도 닿지 않는다
    var OURS = '__tanukiAdjustedMove';

    // 가운데를 덮은 뒤에도 이만큼은 더 들어와 있어야 한다 (창 크기에 대한 비율)
    var MARGIN_RATIO = 0.1;

    var dragging = false;

    // 사이트가 마지막으로 본 커서 위치. 사이트는 이 값과의 차이로 맵을 옮긴다
    var deliveredX = 0;
    var deliveredY = 0;

    // 막느라 넘기지 못한 양. 커서가 돌아오면 이 값이 상쇄되어 곧바로 다시 움직인다
    var offsetX = 0;
    var offsetY = 0;

    // 그림 범위를 담아 둔다. getBBox는 값이 비싸고 맵이 바뀌기 전에는 변하지 않는다
    var contentCache = null;

    /**
     * svg 안에 실제로 그려진 범위를 사용자 단위로 구한다.
     *
     * 캔버스 전체를 덮는 자식(배경, 격자)은 제외한다. 그것까지 넣으면 상자와 같아져
     * 이 계산의 의미가 없어진다
     */
    function readContentBox(wrap) {
        var svg = wrap.querySelector('svg.svg-map');
        if (!svg || !svg.viewBox || !svg.viewBox.baseVal) return null;

        if (contentCache && contentCache.svg === svg) return contentCache;

        var view = svg.viewBox.baseVal;
        var left = null, top = null, right = null, bottom = null;

        Array.prototype.forEach.call(svg.children, function (child) {
            if (!child.getBBox) return;

            var box;
            try { box = child.getBBox(); } catch (e) { return; }
            if (!box || !box.width || !box.height) return;

            // 캔버스를 거의 다 덮는 자식은 배경이다
            if (box.width >= view.width * 0.98 && box.height >= view.height * 0.98) return;

            left = left === null ? box.x : Math.min(left, box.x);
            top = top === null ? box.y : Math.min(top, box.y);
            right = right === null ? box.x + box.width : Math.max(right, box.x + box.width);
            bottom = bottom === null ? box.y + box.height : Math.max(bottom, box.y + box.height);
        });

        // 배경만 있는 맵이면 캔버스 전체를 그림으로 본다
        if (left === null) {
            left = view.x; top = view.y; right = view.x + view.width; bottom = view.y + view.height;
        }

        contentCache = {
            svg: svg,
            viewWidth: view.width,
            viewHeight: view.height,
            x: left,
            y: top,
            width: right - left,
            height: bottom - top
        };

        return contentCache;
    }

    /**
     * 지금 위치와 허용 범위를 읽는다. 맵이 아직 없으면 null
     */
    function readBounds() {
        var wrap = document.querySelector(WRAP_SELECTOR);
        var container = document.querySelector(CONTAINER_SELECTOR);
        if (!wrap || !container) return null;

        var box = wrap.getBoundingClientRect();
        var view = container.getBoundingClientRect();
        if (!box.width || !view.width) return null;

        // 상자 안에서 그림이 차지하는 만큼으로 좁힌다
        var content = readContentBox(wrap);
        var map = box;

        if (content && content.viewWidth && content.viewHeight) {
            var scaleX = box.width / content.viewWidth;
            var scaleY = box.height / content.viewHeight;

            map = {
                left: box.left + content.x * scaleX,
                top: box.top + content.y * scaleY,
                width: content.width * scaleX,
                height: content.height * scaleY
            };
        }

        // 맵이 작으면 여유까지 요구할 수 없으므로 맵 크기의 절반보다 작게 잡는다
        var marginX = Math.min(view.width * MARGIN_RATIO, map.width / 2);
        var marginY = Math.min(view.height * MARGIN_RATIO, map.height / 2);

        return {
            x: map.left - view.left,
            y: map.top - view.top,

            // 맵의 오른쪽 끝이 가운데를 지나야 하므로 왼쪽으로는 여기까지만 간다
            minX: view.width / 2 + marginX - map.width,

            // 맵의 왼쪽 끝이 가운데를 넘으면 안 되므로 오른쪽으로는 여기까지만 간다
            maxX: view.width / 2 - marginX,

            minY: view.height / 2 + marginY - map.height,
            maxY: view.height / 2 - marginY
        };
    }

    /**
     * 범위를 넘지 않는 만큼으로 이동량을 줄인다. 사이트가 1:1로 움직이므로 환산이 없다
     *
     * @param {number} value - 지금 위치
     * @param {number} delta - 이번에 커서가 움직인 양
     * @param {number} min - 허용 최소
     * @param {number} max - 허용 최대
     * @returns {number} 사이트에 넘길 이동량
     */
    function limitDelta(value, delta, min, max) {
        if (delta === 0) return 0;

        var room = delta < 0 ? min - value : max - value;

        // 이미 범위 밖이면 안쪽으로 가는 이동만 허용한다 (축소로 밀려난 경우가 여기다)
        if ((delta < 0 && room >= 0) || (delta > 0 && room <= 0)) return 0;

        return delta < 0 ? Math.max(delta, room) : Math.min(delta, room);
    }

    /**
     * 줄인 위치로 바꾼 이동 이벤트를 대신 보낸다.
     *
     * 원래 이벤트의 target에 보내면 안 된다. 커서가 창 밖으로 나가면 그 자리에 요소가 없어
     * 이벤트가 사이트에 닿지 않고, 그동안 사이트가 기억하는 커서 위치가 멈춰 있다가 커서가
     * 돌아오는 순간 그 차이만큼 한꺼번에 움직여 튄다. 맵 컨테이너로 보내면 사이트가 어디에
     * 처리기를 달았든(컨테이너, document, window) 그 위로 전파된다.
     *
     * 포인터 이벤트는 같은 종류로 만들어야 사이트가 pointerId 같은 값을 잃지 않는다
     */
    function replaceMove(event, x, y) {
        var Constructor = (window.PointerEvent && event instanceof window.PointerEvent)
            ? window.PointerEvent
            : MouseEvent;

        var clone = new Constructor(event.type, {
            bubbles: true,
            cancelable: true,
            composed: true,
            view: window,
            clientX: x,
            clientY: y,
            screenX: x,
            screenY: y,
            button: event.button,
            buttons: event.buttons,
            ctrlKey: event.ctrlKey,
            shiftKey: event.shiftKey,
            altKey: event.altKey,
            metaKey: event.metaKey,
            pointerId: event.pointerId,
            pointerType: event.pointerType,
            isPrimary: event.isPrimary
        });

        clone[OURS] = true;

        var container = document.querySelector(CONTAINER_SELECTOR);
        (container || document).dispatchEvent(clone);
    }

    function onDown(event) {
        if (event.button !== 0) return;

        // 맵을 바꾸면 svg가 새로 만들어지므로 그때 다시 잰다
        var wrap = document.querySelector(WRAP_SELECTOR);
        if (contentCache && wrap && contentCache.svg !== wrap.querySelector('svg.svg-map')) contentCache = null;

        dragging = true;
        offsetX = 0;
        offsetY = 0;
        deliveredX = event.clientX;
        deliveredY = event.clientY;
    }

    function onUp() {
        dragging = false;
    }

    function onMove(event) {
        if (!dragging || event[OURS]) return;

        var bounds = readBounds();
        if (!bounds) return;

        // offset을 뺀 자리가 사이트에 보여 주고 싶은 커서 위치다.
        // 그 자리와 사이트가 마지막으로 본 위치의 차이가 이번에 커서가 실제로 움직인 양이다
        var deltaX = event.clientX - offsetX - deliveredX;
        var deltaY = event.clientY - offsetY - deliveredY;

        var allowedX = limitDelta(bounds.x, deltaX, bounds.minX, bounds.maxX);
        var allowedY = limitDelta(bounds.y, deltaY, bounds.minY, bounds.maxY);

        // 넘기지 못한 만큼을 쌓아 둔다. 커서가 돌아오면 이 값이 상쇄되어 곧바로 다시 움직인다
        offsetX += deltaX - allowedX;
        offsetY += deltaY - allowedY;

        var nextX = deliveredX + allowedX;
        var nextY = deliveredY + allowedY;

        deliveredX = nextX;
        deliveredY = nextY;

        // 줄일 것이 없으면 원래 이벤트를 그대로 보낸다
        if (nextX === event.clientX && nextY === event.clientY) return;

        event.stopImmediatePropagation();
        event.preventDefault();
        replaceMove(event, nextX, nextY);
    }

    // 캡처 단계에서 먼저 받아야 사이트 처리보다 앞선다
    window.addEventListener('mousedown', onDown, true);
    window.addEventListener('mouseup', onUp, true);
    window.addEventListener('mousemove', onMove, true);
    window.addEventListener('pointerdown', onDown, true);
    window.addEventListener('pointerup', onUp, true);
    window.addEventListener('pointercancel', onUp, true);

    // 앱 안에서 어떤 판이 도는지 CDP로 바로 확인하기 위한 표시.
    // 옛 판이 남아 있는 채로 증상을 쫓다 시간을 버린 적이 있어 둔다
    window.__tanukiKeepVisible = { version: 4, rule: 'center-clamp-content', marginRatio: MARGIN_RATIO };

    console.log('[Map Keep Visible] Ready (v4 center-clamp on drawn content)');
})();
