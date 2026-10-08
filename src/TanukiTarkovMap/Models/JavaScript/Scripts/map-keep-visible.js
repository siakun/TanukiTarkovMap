/**
 * 맵을 화면에 붙잡아 두는 스크립트
 *
 * 두 가지를 한다. 맵을 열 때 그림을 창에 맞추고, 끄는 동안 그림이 화면 가운데를 벗어나지
 * 못하게 막는다. 둘 다 "그림이 실제로 차지하는 범위"를 알아야 해서 한 파일에 둔다.
 *
 * 목적: 맵을 끌다가 화면에서 놓치지 않게 하고, 열자마자 쓸 수 있는 크기로 보이게 한다.
 *
 * 사이트가 쓰는 것 (번들에서 확인):
 *   panzoom(.pan.map-cont, { autocenter: true, bounds: true, smoothScroll: false, ... })
 *
 * - anvaka/panzoom 이다. mousedown 때 커서 위치를 기억하고, mousemove마다
 *   dx = clientX - 기억한 위치 만큼 맵을 옮긴 뒤 그 위치를 갱신한다. 즉 커서와 1:1로 움직이며,
 *   배율이 달라도 화면 픽셀 기준이라 환산이 필요 없다 (실측: 커서 100px -> 맵 100px).
 * - smoothScroll: false 라 손을 뗀 뒤 관성이 없다 (실측: 놓은 뒤 이동 0px).
 * - bounds: true 라 사이트에도 경계가 있다. 다만 그 기준이 맵 좌표 공간 전체이고 남기는 양이 창의
 *   5%뿐이라(실측: 오른쪽 끝 60.6px = 1211의 5%), 가장자리가 빈 여백인 맵은 화면에 아무것도
 *   없는 것처럼 된다. 그래서 우리가 더 엄한 규칙을 얹는다.
 *
 * 우리 규칙: 화면 한가운데에는 언제나 맵이 있어야 한다.
 * 드래그는 움직이는 동안 막고, 휠 확대와 창 크기 변경은 사이트가 처리한 다음 프레임에 범위로
 * 되돌린다. 휠은 커서 자리를 기준으로 확대하므로 지형 바깥에서 확대하면 지형이 화면 밖으로 밀린다.
 *
 * 무엇을 "맵"으로 보는가: 맵 좌표 공간 전체(맵 상자)가 아니라 그 안에 실제로 그려진 영역이다.
 * 상자는 여백까지 포함한 맵 좌표 공간 전체(예: Ground Zero는 2800x3100)이고 그림은 그 안의
 * 일부(800x1100)뿐이라, 상자를 기준으로 막으면 상자가 가운데를 덮은 채로 그림만 화면 밖으로
 * 나간다. 실제로 겪은 증상이 그것이다.
 *
 * 맵 상자는 사이트의 지도 상태로 계산한다. 2026-10 판부터 지도를 캔버스로만 그려 이 상자를 가진
 * 요소(예전의 .map-wrap)가 없다. 지도 상태의 panzoom 변환(x, y, zoom)과 회전을 반영한 맵 크기
 * (viewSize)가 곧 그 상자이고, 사이트도 이 값으로 캔버스를 그린다. 지도 상태는 Pilot 브리지에서 얻는다.
 *
 * 그림은 사이트가 바닥 맵을 그리는 canvas.doc-map-canvas에서 투명하지 않은 픽셀의 범위로 잰다.
 * 같은 클래스의 캔버스가 둘이다. 지도 전체를 담은 캔버스와 창보다 조금 큰 캔버스이고, 사이트가
 * 배율에 따라 하나만 보인다(보통 배율에서는 첫째가 display: none, 많이 줄이면 둘째가 visibility:
 * hidden). 그래서 DOM 순서가 아니라 화면에 표시된 하나를 골라 잰다. 예전 판은 바닥 맵을
 * svg.svg-map으로 그렸고 그때는 svg 자식의 bbox를 쟀지만, 지금 판에는 그 svg가 없어 그 경로는 뺐다.
 * 잰 범위는 맵 상자에 대한 비율로 저장한다. 사이트는 캔버스와 상자를 같은 변환으로 움직이므로
 * 이 비율은 끌고 확대해도 그대로다(실측: 확대와 이동 뒤 차이 0.001 미만).
 *
 * 그림을 재지 못하면 상자로 대신하지 않는다. 예전에는 대신했는데, 사이트가 바닥 맵을 캔버스로
 * 옮기자 이 대체가 조용히 쓰여 맵이 창의 절반도 안 되게 뜨고 이동 제한도 헐거워졌다. 로그로는
 * 맞추기가 정상으로 끝난 것처럼 보였다. 재지 못하면 맞추기와 이동 제한을 쉬고, 무엇으로 쟀는지는
 * debugLog의 source에 남긴다.
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
 * 열 때 맞추기: 사이트는 맵 좌표 공간 전체를 기준으로 첫 배율을 잡는데 이 공간이 그림보다 훨씬
 * 크다 (Streets 기준 좌표 공간 3260x3500, 그림 1260x1700으로 면적의 19%). 그래서 맵을 열면 그림이
 * 작게 뜨고 둘레가 비어 보인다. 그림이 창을 채우도록 배율과 위치를 한 번 맞춰 준다.
 *
 * 맞추기는 배율을 휠로, 위치를 사이트 panzoom의 moveBy로 옮긴다. 드래그는 쓰지 않는다. 사이트는
 * 누른 자리에 마커나 그린 도형이 있으면 그것을 옮기는 동작으로 보고 panzoom의 이동을 꺼 버린다
 * (번들의 마커 층 mousedown에서 togglePanEnabled(false)). 중심을 맞추려면 화면 한가운데에서
 * 끌어야 하는데 거기가 바로 마커가 몰려 있는 자리라, 맵과 위치에 따라 되기도 하고 안 되기도 했다.
 *
 * 휠 한 칸의 배율은 사이트가 정한다. deltaY 25.6이면 0.8배, -32면 1.25배다(실측, 오차 없음).
 * 예전에는 위치도 휠 두 칸(서로 다른 자리에서 축소와 확대)으로 옮겼는데, 최소 배율에서는 축소
 * 칸이 막혀 배율까지 바뀌었다. 최소 배율에서도 지형이 창보다 큰 맵(Icebreaker처럼 세로로 긴 맵)
 * 에서 가운데 맞추기가 어긋나고 배율이 한 칸씩 올라갔다. moveBy는 배율을 건드리지 않는다.
 *
 * 내 위치가 표시되면 화면 가운데는 위치가 정한다. 맞추기는 그림 가운데를 창 가운데에 두는데,
 * 지형이 창보다 크면 그 자리에서 마커가 화면 밖에 남는다. 앱은 맵을 열자마자 위치를 보내고 이
 * 스크립트는 그 뒤에 맞추므로 열 때마다 겪는 순서다. 그래서 맞추기가 끝날 때마다 Pilot 브리지에
 * 마커를 보이게 하라고 맡기고(revealPosition), 맞춘 뒤의 재확인은 위치가 표시된 동안 그림
 * 가운데로 다시 옮기지 않는다. 다시 옮기면 마커를 보이려고 옮긴 화면과 번갈아 움직인다.
 */

(function () {
    'use strict';

    var CONTAINER_SELECTOR = '.pan.map-cont';

    // 지금 온라인 판이 바닥 맵을 그리는 캔버스
    var CANVAS_SELECTOR = 'canvas.doc-map-canvas';

    // 캔버스를 이만큼 줄여서 읽는다. 그림 범위는 몇 px 틀려도 맞추기와 이동 제한에 지장이 없고,
    // 읽는 데 드는 시간이 크게 준다 (실측: 원본 약 4ms, 1/4로 줄이면 1ms 미만)
    var PICTURE_SAMPLE_DIVISOR = 4;

    // 우리가 보낸 이벤트를 우리가 다시 가로채지 않기 위한 표시.
    // 이 표시가 없으면 우리 처리기가 자기 이벤트를 또 줄여 막아 사이트에 아무것도 닿지 않는다
    var OURS = '__tanukiAdjustedMove';

    // 가운데를 덮은 뒤에도 이만큼은 더 들어와 있어야 한다 (창 크기에 대한 비율)
    var MARGIN_RATIO = 0.1;

    // 그림이 창에서 차지하는 비율이 이 사이에 들면 맞은 것으로 본다.
    // 휠 한 칸이 1.25배라 이보다 좁게 잡으면 어느 칸에서도 만족하지 못하고 오간다
    var FIT_MIN = 0.75;
    var FIT_MAX = 0.98;

    // 휠 한 칸의 deltaY. 사이트는 이 값을 0.8배와 1.25배로 받아들인다
    var WHEEL_OUT = 25.6;
    var WHEEL_IN = -32;

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

    // 맞추기가 사이트의 최소나 최대 배율에 닿아 더 맞출 수 없는지. 그러면 채움 비율이 범위 밖이어도
    // 재확인이 다시 맞추지 않는다. 다시 맞춰 봐야 배율은 그대로이고 화면만 다시 가운데로 옮긴다
    var zoomLimited = false;

    // 사이트가 마지막으로 본 커서 위치. 사이트는 이 값과의 차이로 맵을 옮긴다
    var deliveredX = 0;
    var deliveredY = 0;

    // 막느라 넘기지 못한 양. 커서가 돌아오면 이 값이 상쇄되어 곧바로 다시 움직인다
    var offsetX = 0;
    var offsetY = 0;

    // 재 둔 그림 범위. 맵 상자에 대한 비율(0~1)이라 끌고 확대해도 그대로 쓴다.
    // source는 잰 곳(canvas)이고, complete는 그림 전체가 보일 때 쟀는지다.
    // 한 번 재는 비용은 작지만, 끄는 도중 이동마다 캔버스를 다시 읽지 않으려고 담아 둔다
    var contentCache = null;

    // 사이트 캔버스를 줄여 옮겨 두고 읽는 우리 쪽 캔버스
    var sampleCanvas = null;

    function siteMap() {
        var pilot = window.tanukiPilot;
        return (pilot && typeof pilot.getMap === 'function' && pilot.getMap()) || null;
    }

    /**
     * 지도 컨테이너와, 맵 좌표 공간 전체(맵 상자)가 화면에서 차지하는 자리를 구한다. 없으면 null.
     *
     * 상자는 지도 상태의 panzoom 변환과 회전을 반영한 맵 크기로 계산한다. 사이트가 캔버스에 맵을
     * 그리는 변환과 같다. 변환은 컨테이너의 안쪽(테두리 안) 왼쪽 위가 원점이다.
     * turn은 회전 각도다. 돌리면 같은 상자 안의 그림 자리가 달라지므로 다시 재는 기준으로 쓴다
     */
    function mapFrame() {
        var map = siteMap();
        var container = map && map.cont;
        if (!container || !container.isConnected || !map.viewSize || !(map.zoom > 0)) return null;

        var view = container.getBoundingClientRect();
        if (!view.width || !view.height) return null;

        var left = view.left + container.clientLeft + map.x;
        var top = view.top + container.clientTop + map.y;
        return {
            container: container,
            view: view,
            box: { left: left, top: top, width: map.viewSize.width * map.zoom, height: map.viewSize.height * map.zoom },
            turn: String(map.viewRotation || 0)
        };
    }

    function mapContainer() {
        var map = siteMap();
        return (map && map.cont && map.cont.isConnected && map.cont) || document.querySelector(CONTAINER_SELECTOR);
    }

    /**
     * 같은 맵 컨테이너에서 화면에 표시된 지형 캔버스를 찾는다.
     *
     * 사이트는 같은 클래스의 캔버스 둘을 배율에 따라 맞바꿔 보이므로 DOM 순서로 고르지 않는다.
     * 숨은 쪽은 display: none이라 화면 크기가 0이거나, visibility: hidden이라 크기는 있어도 보이지
     * 않는다. 표시된 후보가 여러 개면 어느 것이 지형인지 보장할 수 없어 측정을 중단한다.
     */
    function findTerrainCanvas(container) {
        var canvases = container.querySelectorAll(CANVAS_SELECTOR);
        var terrain = null;

        for (var index = 0; index < canvases.length; index++) {
            var canvas = canvases[index];
            var screen = canvas.getBoundingClientRect();
            if (!canvas.width || !canvas.height || !screen.width || !screen.height) continue;

            var style = getComputedStyle(canvas);
            if (style.visibility === 'hidden' || style.visibility === 'collapse' || style.opacity === '0') continue;

            if (terrain) return null;
            terrain = canvas;
        }

        return terrain;
    }

    /**
     * 캔버스에서 투명하지 않은 픽셀의 범위를 맵 상자에 대한 비율로 구한다.
     *
     * 캔버스는 창에 보이는 부분만 그리므로, 그림이 창 밖으로 이어지면 가장자리에서 잘린다.
     * 그때는 complete를 false로 두어 맞추기가 먼저 줄여서 다시 재게 한다.
     *
     * 사이트 캔버스에서 getImageData를 부르지 않고 줄인 사본을 우리 캔버스에 그려 읽는다.
     * 읽는 양이 줄고, 브라우저는 자주 읽히는 캔버스를 그리기보다 읽기에 맞춰 다룰 수 있으므로
     * (willReadFrequently) 사이트의 그리기에 영향을 줄 여지를 남기지 않는다
     */
    function readCanvasPicture(frame, canvas) {
        if (!canvas || !canvas.width || !canvas.height) return null;

        var width = Math.max(1, Math.round(canvas.width / PICTURE_SAMPLE_DIVISOR));
        var height = Math.max(1, Math.round(canvas.height / PICTURE_SAMPLE_DIVISOR));

        if (!sampleCanvas) sampleCanvas = document.createElement('canvas');

        // 크기를 다시 정하면 앞서 옮겨 둔 그림도 함께 지워진다
        sampleCanvas.width = width;
        sampleCanvas.height = height;

        var pixels;
        try {
            var context = sampleCanvas.getContext('2d', { willReadFrequently: true });
            context.drawImage(canvas, 0, 0, width, height);
            pixels = context.getImageData(0, 0, width, height).data;
        } catch (e) {
            // 다른 출처의 그림이 섞여 읽기가 막힌 경우다
            return null;
        }

        var left = width, top = height, right = -1, bottom = -1;
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                if (pixels[(y * width + x) * 4 + 3] === 0) continue;
                if (x < left) left = x;
                if (x > right) right = x;
                if (y < top) top = y;
                if (y > bottom) bottom = y;
            }
        }

        // 아직 아무것도 그려지지 않았다
        if (right < 0) return null;

        var screen = canvas.getBoundingClientRect();
        var box = frame.box;
        var unitX = screen.width / width;
        var unitY = screen.height / height;

        return {
            source: 'canvas',
            left: (screen.left + left * unitX - box.left) / box.width,
            top: (screen.top + top * unitY - box.top) / box.height,
            width: (right - left + 1) * unitX / box.width,
            height: (bottom - top + 1) * unitY / box.height,
            // 가장자리에 닿았으면 창 밖으로 이어지는 부분이 잘렸을 수 있다
            complete: left > 0 && top > 0 && right < width - 1 && bottom < height - 1
        };
    }

    /**
     * 그림 범위를 맵 상자에 대한 비율로 돌려준다. 재지 못하면 null.
     *
     * allowMeasure가 false면 재 둔 값만 쓴다. 끄는 도중에는 이동마다 캔버스를 읽지 않으려고
     * 그렇게 부른다. 맵을 바꾸거나 돌리면 그림이 달라지므로 다시 잰다. 돌렸는지는 지도 상태의
     * 회전 각도로 안다. 끌고 확대하는 변환은 맵 상자를 옮길 뿐 상자 안의 그림 자리를 바꾸지 않는다
     */
    function readPicture(frame, allowMeasure) {
        // 캔버스가 교체되거나 사이트가 배율에 따라 보이는 캔버스를 맞바꾸면 이전 그림 범위를 버린다.
        var owner = findTerrainCanvas(frame.container);
        var turn = frame.turn;

        if (contentCache && (contentCache.owner !== owner || contentCache.turn !== turn)) contentCache = null;
        if (!owner) return null;
        if (contentCache && (contentCache.complete || !allowMeasure)) return contentCache;
        if (!allowMeasure) return null;

        var picture = readCanvasPicture(frame, owner);
        if (!picture) return null;

        picture.owner = owner;
        picture.turn = turn;
        contentCache = picture;
        return picture;
    }

    /**
     * 지금 위치와 허용 범위를 읽는다. 맵이나 그림이 아직 없으면 null
     *
     * @param {boolean} allowMeasure - 재 둔 그림 범위가 없거나 일부만 잰 것이면 새로 잰다
     */
    function readBounds(allowMeasure) {
        var frame = mapFrame();
        if (!frame) return null;

        var box = frame.box;
        var view = frame.view;
        if (!box.width) return null;

        // 상자 안에서 그림이 차지하는 만큼으로 좁힌다. 재지 못했으면 상자로 대신하지 않는다
        var picture = readPicture(frame, allowMeasure);
        if (!picture) return null;

        var map = {
            left: box.left + picture.left * box.width,
            top: box.top + picture.top * box.height,
            width: picture.width * box.width,
            height: picture.height * box.height
        };

        // 맵이 작으면 여유까지 요구할 수 없으므로 맵 크기의 절반보다 작게 잡는다
        var marginX = Math.min(view.width * MARGIN_RATIO, map.width / 2);
        var marginY = Math.min(view.height * MARGIN_RATIO, map.height / 2);

        return {
            x: map.left - view.left,
            y: map.top - view.top,
            width: map.width,
            height: map.height,
            source: picture.source,
            complete: picture.complete,

            // 맵 상자의 폭은 배율에만 따라 변한다. 그림이 잘려 재어지는 동안에는 그림 폭이 배율을
            // 따라 변하지 않으므로, 배율이 움직였는지는 이 값으로 본다
            boxWidth: box.width,
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

        // 이미 범위 밖이면 안쪽으로 가는 이동만 허용한다 (휠 보정 전에 끌기 시작한 경우가 여기다)
        if ((delta < 0 && room >= 0) || (delta > 0 && room <= 0)) return 0;

        return delta < 0 ? Math.max(delta, room) : Math.min(delta, room);
    }

    /**
     * 범위로 되돌리는 데 필요한 이동량. 범위 안이면 0이다
     */
    function overflow(value, min, max) {
        if (value < min) return min - value;
        if (value > max) return max - value;
        return 0;
    }

    /**
     * 배율은 그대로 두고 화면을 dx, dy(px)만큼 옮긴다. 옮겼으면 true.
     *
     * 사이트의 panzoom이 공개한 moveBy를 쓴다. panzoom이 변환을 바꾸면 사이트가 캔버스와 마커를
     * 함께 다시 그리므로 층이 어긋나지 않는다. panzoom은 Pilot 브리지가 찾은 지도 객체에서 얻으며,
     * 찾지 못하면 옮기지 않는다. 변환은 panzoom이 다음 프레임에 DOM에 쓴다
     */
    function panBy(dx, dy) {
        var map = siteMap();
        var panzoom = map && map.panzoom;
        if (!panzoom || typeof panzoom.moveBy !== 'function') return false;

        panzoom.moveBy(dx, dy, false);
        return true;
    }

    function hasPosition() {
        var pilot = window.tanukiPilot;
        return !!pilot && typeof pilot.hasPosition === 'function' && pilot.hasPosition();
    }

    /**
     * 내 위치 마커가 화면 안쪽에 오게 Pilot 브리지에 맡긴다. 위치가 없거나 마커가 이미 안쪽에
     * 있으면 브리지는 화면을 옮기지 않는다
     */
    function revealPosition(reason) {
        var pilot = window.tanukiPilot;
        if (!pilot || typeof pilot.revealPosition !== 'function') return;
        if (pilot.revealPosition()) note({ 단계: 'reveal', 이유: reason });
    }

    var correctionPending = false;

    /**
     * 휠 확대와 창 크기 변경 뒤에 그림을 범위로 되돌린다. 끄는 동안은 onMove가 막는다.
     *
     * 두 프레임 뒤에 잰다. 캡처 단계의 이 처리기가 사이트보다 먼저 프레임을 예약하므로, 한
     * 프레임 뒤에는 panzoom이 휠의 변환을 아직 DOM에 쓰지 않았다. 그때 재면 직전 휠의 화면으로
     * 계산한 이동량이 새 화면에 더해져 보정이 한 칸씩 늦는다(실측: 이동량이 앞 휠의 값과 일치)
     */
    function correctLater(reason) {
        if (correctionPending) return;
        correctionPending = true;

        requestAnimationFrame(function () { requestAnimationFrame(function () {
            correctionPending = false;
            if (dragging) return;

            var bounds = readBounds(false);
            if (!bounds) return;

            var dx = overflow(bounds.x, bounds.minX, bounds.maxX);
            var dy = overflow(bounds.y, bounds.minY, bounds.maxY);
            if (Math.abs(dx) < 1 && Math.abs(dy) < 1) return;

            var moved = panBy(dx, dy);
            note({ 단계: 'correct', 이유: reason, dx: Math.round(dx), dy: Math.round(dy), 이동: moved });
        }); });
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

        var container = mapContainer();
        (container || document).dispatchEvent(clone);
    }

    function onDown(event) {
        if (event.button !== 0) return;

        if (event.isTrusted) userTookOver = true;

        // 끄는 도중에는 그림을 새로 재지 않으므로 누르는 순간에 잰다. 그림 전체를 보고 잰 값이
        // 이미 있으면 다시 재지 않고, 맵을 바꾸거나 돌렸으면 readPicture가 새로 잰다
        var frame = mapFrame();
        if (frame) readPicture(frame, true);

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

        var bounds = readBounds(false);
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
        var container = mapContainer();
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
     * 그림 중심을 창 중심으로 옮기고, 끝나면 내 위치 마커가 화면 안쪽에 있는지 확인한다.
     *
     * 한 번이면 맞지만, 사이트가 경계로 잘라 내거나 층이 늦게 그려져 값이 달라질 수 있어
     * 남은 어긋남이 없어질 때까지 몇 번 더 확인한다. 맞추기의 모든 갈래가 여기서 끝난다
     */
    function centerStep(remaining) {
        if (userTookOver) return;

        var bounds = readBounds(true);
        var shift = bounds && centerShift(bounds);
        if (bounds) note({ 단계: 'center', shiftX: Math.round(shift.x), shiftY: Math.round(shift.y), 남은시도: remaining });

        var finished = !bounds || remaining <= 0
            || (Math.abs(shift.x) < CENTER_TOLERANCE && Math.abs(shift.y) < CENTER_TOLERANCE);

        // 지도 객체를 찾지 못해 옮길 수 없을 때도 맞추기는 여기서 끝난다
        if (finished || !panBy(shift.x, shift.y)) {
            revealPosition('fit');
            return;
        }

        setTimeout(function () { centerStep(remaining - 1); }, 120);
    }

    /**
     * 그림이 창을 채우도록 배율을 한 칸씩 맞춘다.
     *
     * 칸수를 미리 계산해 한 번에 보내지 않고 매번 다시 재는 이유는, 사이트가 최소와 최대
     * 배율을 따로 두고 있어 계산대로 끝나지 않을 수 있기 때문이다.
     * 고정점을 그림 중심에 두어 배율을 맞추는 동안 그림이 제자리에 있게 한다.
     *
     * 그림 일부가 창 밖에 있어 잘린 채로 재었으면 채움 비율을 믿지 않고 먼저 줄인다.
     * 잘린 그림은 실제보다 작게 재어지므로, 그 값으로 판단하면 창보다 큰 그림을 맞았다고 본다
     */
    function fitStep(remaining) {
        if (userTookOver) return;

        var bounds = readBounds(true);
        if (!bounds || remaining <= 0) {
            centerStep(CENTER_MAX_TRIES);
            return;
        }

        var fill = fillRatio(bounds);

        note({ 단계: 'fit', 채움: +fill.toFixed(3), 전체: bounds.complete, source: bounds.source, 남은칸: remaining });

        if (bounds.complete && fill >= FIT_MIN && fill <= FIT_MAX) {
            centerStep(CENTER_MAX_TRIES);
            return;
        }

        var anchorX = bounds.viewLeft + bounds.x + bounds.width / 2;
        var anchorY = bounds.viewTop + bounds.y + bounds.height / 2;
        var before = bounds.boxWidth;
        var zoomIn = bounds.complete && fill < FIT_MIN;

        fireWheel(anchorX, anchorY, zoomIn ? WHEEL_IN : WHEEL_OUT);

        setTimeout(function () {
            var after = readBounds(true);

            // 배율이 더 움직이지 않으면 사이트의 한계에 닿은 것이다
            if (!after || Math.abs(after.boxWidth - before) < 1) {
                if (after) zoomLimited = true;
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
     * 등)은 처음 잰 그림 범위가 낡는다. 그때는 다시 맞춘다. 배율 한계에 닿았으면 다시 맞춰도
     * 배율이 그대로라 채움과 잘림은 따지지 않고, 내 위치가 표시된 동안은 가운데를 위치가 정하므로
     * 그림 가운데에서 벗어난 것도 따지지 않는다. 대신 마커가 화면 안쪽에 있는지 확인한다
     */
    function verifyFit(remaining) {
        if (remaining <= 0 || userTookOver) return;

        setTimeout(function () {
            if (userTookOver) return;

            // 확인할 때는 그림 범위를 다시 잰다. 끄는 도중에는 캐시를 그대로 써서 값이 비싸지지 않게 한다
            contentCache = null;

            var bounds = readBounds(true);
            if (!bounds) {
                note({ 단계: 'verify', source: null, 다시: false });
                return;
            }

            var fill = fillRatio(bounds);
            var shift = centerShift(bounds);
            var positionShown = hasPosition();
            var refit = !zoomLimited && (!bounds.complete || fill < FIT_MIN || fill > FIT_MAX);
            var recenter = !positionShown
                && (Math.abs(shift.x) > FIT_CENTER_TOLERANCE || Math.abs(shift.y) > FIT_CENTER_TOLERANCE);

            note({ 단계: 'verify', 채움: +fill.toFixed(3), 전체: bounds.complete, source: bounds.source,
                offX: Math.round(shift.x), offY: Math.round(shift.y), 한계: zoomLimited, 위치: positionShown,
                다시: refit ? 'fit' : recenter ? 'center' : false });

            if (refit) fitStep(FIT_MAX_STEPS);
            else if (recenter) centerStep(CENTER_MAX_TRIES);
            else revealPosition('verify');

            verifyFit(remaining - 1);
        }, 1200);
    }

    /**
     * 맵이 그려지면 맞춘다
     */
    function fitWhenReady(deadline) {
        if (userTookOver) return;

        var bounds = readBounds(true);

        if (bounds && bounds.width > 0) {
            fitStep(FIT_MAX_STEPS);
            verifyFit(3);
            return;
        }

        // 그림을 끝내 재지 못했으면 맞추지 않는다. 왜 안 움직였는지는 여기서 알 수 있다
        if (Date.now() > deadline) {
            note({ 단계: 'wait', source: null, 결과: '그림을 재지 못해 맞추지 않음' });
            return;
        }

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

            // 새 맵은 그림과 배율 한계가 다르므로 다시 잰다
            contentCache = null;
            userTookOver = false;
            zoomLimited = false;
            setTimeout(function () { fitWhenReady(Date.now() + FIT_WAIT); }, 600);
        }, 500);
    }

    /**
     * 사용자가 배율을 직접 바꾸면 맞추기를 그만두고, 바뀐 화면을 범위로 되돌린다.
     *
     * 우리가 보내는 휠은 isTrusted가 false라 여기에 걸리지 않는다.
     * Alt 휠은 사이트가 배율 대신 층 전환에 쓰므로 화면이 움직이지 않는다
     */
    function onWheel(event) {
        if (!event.isTrusted) return;
        userTookOver = true;
        if (!event.altKey) correctLater('wheel');
    }

    // 캡처 단계에서 먼저 받아야 사이트 처리보다 앞선다
    window.addEventListener('wheel', onWheel, true);
    window.addEventListener('mousedown', onDown, true);
    window.addEventListener('mouseup', onUp, true);
    window.addEventListener('mousemove', onMove, true);
    window.addEventListener('pointerdown', onDown, true);
    window.addEventListener('pointerup', onUp, true);
    window.addEventListener('pointercancel', onUp, true);
    window.addEventListener('resize', function () { correctLater('resize'); });

    // 앱 안에서 어떤 판이 도는지 CDP로 바로 확인하기 위한 표시.
    // 옛 판이 남아 있는 채로 증상을 쫓다 시간을 버린 적이 있어 둔다
    window.__tanukiKeepVisible = {
        version: 10,
        rule: 'fit-on-open + center-clamp on drag, wheel and resize, picture from the displayed terrain canvas, map box from the site map state, position owns the center once shown',
        marginRatio: MARGIN_RATIO,
        // 맞추기가 사이트의 배율 한계에 닿아 채움 비율을 맞추지 못했는지
        zoomLimited: function () { return zoomLimited; },
        // 지금 무엇으로 잰 그림 범위를 쓰는지. null이면 아직 재지 못해 맞추기와 이동 제한이 쉬는 중이다
        picture: function () {
            return contentCache && {
                source: contentCache.source,
                complete: contentCache.complete,
                left: contentCache.left,
                top: contentCache.top,
                width: contentCache.width,
                height: contentCache.height
            };
        },
        log: function () { return debugLog; }
    };

    // 사이트가 첫 배율과 위치를 잡은 뒤에 맞춘다. 너무 이르면 사이트가 다시 가운데로 옮긴다
    setTimeout(function () { fitWhenReady(Date.now() + FIT_WAIT); }, 600);
    watchMapChange();

    console.log('[Map Keep Visible] Ready (v10 fit-on-open + center-clamp on drag, wheel and resize)');
})();
