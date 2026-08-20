/**
 * 맵을 화면에 붙잡아 두는 스크립트
 *
 * 두 가지를 한다. 맵을 열 때 그림을 창에 맞추고, 끄는 동안 그림이 화면 가운데를 벗어나지
 * 못하게 막는다. 둘 다 "그림이 실제로 차지하는 범위"를 알아야 해서 한 파일에 둔다.
 *
 * 목적: 맵을 끌다가 화면에서 놓치지 않게 하고, 열자마자 쓸 수 있는 크기로 보이게 한다.
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
 *
 * 열 때 맞추기: 사이트는 캔버스 기준으로 첫 배율을 잡는데 캔버스가 그림보다 훨씬 크다
 * (Streets 기준 캔버스 3260x3500, 그림 1260x1700으로 면적의 19%). 그래서 맵을 열면 그림이
 * 작게 뜨고 둘레가 비어 보인다. 그림이 창을 채우도록 배율과 위치를 한 번 맞춰 준다.
 *
 * 맞추기는 휠만으로 한다. 드래그는 쓰지 않는다. 사이트는 누른 자리에 마커나 그린 도형이
 * 있으면 그것을 옮기는 동작으로 보고 panzoom의 이동을 꺼 버린다(번들의 마커 층 mousedown에서
 * togglePanEnabled(false)). 중심을 맞추려면 화면 한가운데에서 끌어야 하는데 거기가 바로
 * 마커가 몰려 있는 자리라, 맵과 위치에 따라 되기도 하고 안 되기도 했다. 휠에는 그 차단이 없다.
 *
 * 휠 한 칸의 배율은 사이트가 정한다. deltaY 25.6이면 0.8배, -32면 1.25배다(실측, 오차 없음).
 * 두 칸을 서로 다른 자리에서 연달아 걸면 배율은 0.8 x 1.25 = 1로 돌아오고 두 자리의 차이에
 * 비례한 평행이동만 남는다. 이 성질로 배율과 위치를 따로 맞춘다. 자세한 식은 shiftContent에
 * 적어 두었다.
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

    // 그림이 창에서 차지하는 비율이 이 사이에 들면 맞은 것으로 본다.
    // 휠 한 칸이 1.25배라 이보다 좁게 잡으면 어느 칸에서도 만족하지 못하고 오간다
    var FIT_MIN = 0.75;
    var FIT_MAX = 0.98;

    // 휠 한 칸의 deltaY. 사이트는 이 값을 0.8배와 1.25배로 받아들이며 둘은 서로의 역수다.
    // 역수가 아니면 위치를 맞추는 짝이 배율까지 바꿔 버린다
    var WHEEL_OUT = 25.6;
    var WHEEL_IN = -32;

    // 축소 자리를 확대 자리에서 얼마나 떨어뜨릴지. 한 짝의 이동량이 두 자리 차이의 1/4이라 4다
    var PAIR_SHIFT_FACTOR = 4;

    // 중심이 이만큼 안에 들면 맞은 것으로 본다 (px)
    var CENTER_TOLERANCE = 3;

    // 중심 맞추기를 되풀이하는 한계. 한 번이면 끝나지만 배율이 바뀌는 중에 재면 어긋날 수 있다
    var CENTER_MAX_TRIES = 3;

    // 맞추기가 끝나지 않아도 이 횟수를 넘기지 않는다
    var FIT_MAX_STEPS = 14;

    // 맵이 그려지기를 기다리는 한계 (ms)
    var FIT_WAIT = 8000;

    // 중심이 이만큼 넘게 어긋나 있으면 다시 맞춘다 (px)
    var FIT_CENTER_TOLERANCE = 20;

    // 맞추기 과정을 밖에서 볼 수 있게 남긴다. 이 값이 없으면 왜 안 움직였는지 알 방법이 없다
    var debugLog = [];

    function note(entry) {
        entry.t = Math.round(performance.now());
        debugLog.push(entry);
        if (debugLog.length > 40) debugLog.shift();
    }

    var dragging = false;

    // 사용자가 직접 끌거나 휠을 돌리면 그 뒤의 화면은 사용자 것이다. 맞추기를 더 하지 않는다
    var userTookOver = false;

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
            width: map.width,
            height: map.height,
            viewLeft: view.left,
            viewTop: view.top,
            viewWidth: view.width,
            viewHeight: view.height,

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

        if (event.isTrusted) userTookOver = true;

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

    /**
     * 그림과 창의 크기 비를 구한다. 1이면 그림이 창을 꽉 채운 것이다
     */
    function fillRatio(bounds) {
        return Math.max(bounds.width / bounds.viewWidth, bounds.height / bounds.viewHeight);
    }

    /**
     * 그림 중심을 창 중심에 맞추려면 얼마나 옮겨야 하는지 구한다
     */
    function centerShift(bounds) {
        return {
            x: bounds.viewWidth / 2 - (bounds.x + bounds.width / 2),
            y: bounds.viewHeight / 2 - (bounds.y + bounds.height / 2)
        };
    }

    /**
     * 휠 한 칸을 보낸다. 사이트는 커서 자리를 고정점으로 삼아 배율을 바꾼다
     */
    function fireWheel(x, y, deltaY) {
        var container = document.querySelector(CONTAINER_SELECTOR);
        if (!container) return;

        var event = new WheelEvent('wheel', {
            bubbles: true,
            cancelable: true,
            view: window,
            clientX: Math.round(x),
            clientY: Math.round(y),
            deltaY: deltaY
        });

        event[OURS] = true;
        container.dispatchEvent(event);
    }

    /**
     * 배율은 그대로 두고 그림만 옮긴다.
     *
     * 휠 한 칸은 커서 자리 a를 고정점으로 삼아 위치를 x' = r*x + (1-r)*a 로 바꾼다.
     * 축소(r = 0.8)를 a에서, 확대(1/r = 1.25)를 b에서 연달아 걸면
     *   x'' = x + (1/r - 1) * (a - b) = x + (a - b) / 4
     * 가 되어 배율은 제자리로 돌아오고 평행이동만 남는다. 그래서 a를 b에서 옮길 거리의 4배만큼
     * 떨어뜨린다 (실측: 목표 200,120 -> 결과 200,120, 배율 변화 없음).
     *
     * 두 칸을 같은 틱에 보내는 이유는 중간의 축소된 화면이 그려지지 않게 하기 위해서다
     */
    function shiftContent(bounds, shift) {
        var baseX = bounds.viewLeft + bounds.viewWidth / 2;
        var baseY = bounds.viewTop + bounds.viewHeight / 2;

        fireWheel(baseX + shift.x * PAIR_SHIFT_FACTOR, baseY + shift.y * PAIR_SHIFT_FACTOR, WHEEL_OUT);
        fireWheel(baseX, baseY, WHEEL_IN);
    }

    /**
     * 그림 중심을 창 중심으로 옮긴다.
     *
     * 한 번이면 맞지만, 사이트가 경계로 잘라 내거나 층이 늦게 그려져 값이 달라질 수 있어
     * 남은 어긋남이 없어질 때까지 몇 번 더 확인한다
     */
    function centerStep(remaining) {
        if (userTookOver) return;

        var bounds = readBounds();
        if (!bounds) return;

        var shift = centerShift(bounds);

        note({ 단계: 'center', shiftX: Math.round(shift.x), shiftY: Math.round(shift.y), 남은시도: remaining });

        if (Math.abs(shift.x) < CENTER_TOLERANCE && Math.abs(shift.y) < CENTER_TOLERANCE) return;
        if (remaining <= 0) return;

        shiftContent(bounds, shift);

        setTimeout(function () { centerStep(remaining - 1); }, 120);
    }

    /**
     * 그림이 창을 채우도록 배율을 한 칸씩 맞춘다.
     *
     * 칸수를 미리 계산해 한 번에 보내지 않고 매번 다시 재는 이유는, 사이트가 최소와 최대
     * 배율을 따로 두고 있어 계산대로 끝나지 않을 수 있기 때문이다.
     * 고정점을 그림 중심에 두어 배율을 맞추는 동안 그림이 제자리에 있게 한다
     */
    function fitStep(remaining) {
        if (userTookOver) return;

        var bounds = readBounds();
        if (!bounds || remaining <= 0) {
            centerStep(CENTER_MAX_TRIES);
            return;
        }

        var fill = fillRatio(bounds);

        note({ 단계: 'fit', 채움: +fill.toFixed(3), 남은칸: remaining });

        if (fill >= FIT_MIN && fill <= FIT_MAX) {
            centerStep(CENTER_MAX_TRIES);
            return;
        }

        var anchorX = bounds.viewLeft + bounds.x + bounds.width / 2;
        var anchorY = bounds.viewTop + bounds.y + bounds.height / 2;
        var before = bounds.width;

        fireWheel(anchorX, anchorY, fill < FIT_MIN ? WHEEL_IN : WHEEL_OUT);

        setTimeout(function () {
            var after = readBounds();

            // 배율이 더 움직이지 않으면 사이트의 한계에 닿은 것이다
            if (!after || Math.abs(after.width - before) < 1) {
                centerStep(CENTER_MAX_TRIES);
                return;
            }

            fitStep(remaining - 1);
        }, 80);
    }

    /**
     * 맞춘 뒤 사이트가 다시 옮겨 놓았는지 확인한다.
     *
     * 사이트도 맵을 열 때 자기 방식으로 가운데를 잡고, 층이 늦게 그려지는 맵(Streets의 tramway
     * 등)은 처음 잰 그림 범위가 낡는다. 그때는 다시 맞춘다
     */
    function verifyFit(remaining) {
        if (remaining <= 0 || userTookOver) return;

        setTimeout(function () {
            if (userTookOver) return;

            // 확인할 때는 그림 범위를 다시 잰다. 끄는 도중에는 캐시를 그대로 써서 값이 비싸지지 않게 한다
            contentCache = null;

            var bounds = readBounds();
            if (!bounds) return;

            var fill = fillRatio(bounds);
            var shift = centerShift(bounds);
            var drifted = fill < FIT_MIN || fill > FIT_MAX
                || Math.abs(shift.x) > FIT_CENTER_TOLERANCE || Math.abs(shift.y) > FIT_CENTER_TOLERANCE;

            note({ 단계: 'verify', 채움: +fill.toFixed(3), offX: Math.round(shift.x), offY: Math.round(shift.y), 다시: drifted });

            if (drifted) fitStep(FIT_MAX_STEPS);

            verifyFit(remaining - 1);
        }, 1200);
    }

    /**
     * 맵이 그려지면 맞춘다
     */
    function fitWhenReady(deadline) {
        if (userTookOver) return;

        var bounds = readBounds();

        if (bounds && bounds.width > 0) {
            fitStep(FIT_MAX_STEPS);
            verifyFit(3);
            return;
        }

        if (Date.now() > deadline) return;

        setTimeout(function () { fitWhenReady(deadline); }, 150);
    }

    /**
     * 맵을 바꿔도 맞춘다.
     *
     * 사이트 왼쪽 목록으로 맵을 고르면 문서를 다시 읽지 않아 이 스크립트도 다시 돌지 않는다.
     * 주소가 바뀌는 것으로 새 맵을 알아낸다
     */
    function watchMapChange() {
        var fittedPath = location.pathname;

        setInterval(function () {
            if (location.pathname === fittedPath) return;

            fittedPath = location.pathname;
            if (fittedPath.indexOf('/maps/') === -1) return;

            // 새 맵은 svg가 새로 만들어지므로 그림 범위를 다시 잰다
            contentCache = null;
            userTookOver = false;
            setTimeout(function () { fitWhenReady(Date.now() + FIT_WAIT); }, 600);
        }, 500);
    }

    /**
     * 사용자가 배율을 직접 바꾸면 맞추기를 그만둔다.
     *
     * 우리가 보내는 휠은 isTrusted가 false라 여기에 걸리지 않는다
     */
    function onWheel(event) {
        if (event.isTrusted) userTookOver = true;
    }

    // 캡처 단계에서 먼저 받아야 사이트 처리보다 앞선다
    window.addEventListener('wheel', onWheel, true);
    window.addEventListener('mousedown', onDown, true);
    window.addEventListener('mouseup', onUp, true);
    window.addEventListener('mousemove', onMove, true);
    window.addEventListener('pointerdown', onDown, true);
    window.addEventListener('pointerup', onUp, true);
    window.addEventListener('pointercancel', onUp, true);

    // 앱 안에서 어떤 판이 도는지 CDP로 바로 확인하기 위한 표시.
    // 옛 판이 남아 있는 채로 증상을 쫓다 시간을 버린 적이 있어 둔다
    window.__tanukiKeepVisible = {
        version: 5,
        rule: 'fit-on-open + center-clamp',
        marginRatio: MARGIN_RATIO,
        log: function () { return debugLog; }
    };

    // 사이트가 첫 배율과 위치를 잡은 뒤에 맞춘다. 너무 이르면 사이트가 다시 가운데로 옮긴다
    setTimeout(function () { fitWhenReady(Date.now() + FIT_WAIT); }, 600);
    watchMapChange();

    console.log('[Map Keep Visible] Ready (v5 fit-on-open + center-clamp)');
})();
