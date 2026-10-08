/**
 * Online에서 내 위치를 사이트 지도의 어디에 그릴지 정하고, 앱이 부르는 위치 표시 계약을 연다.
 *
 * INTENT
 * 내 위치와 바라보는 방향은 앱이 파일명에서 직접 읽고 map-markers.js가 직접 그린다. 사이트의 위치 입력
 * 경로(Pilot 함수, "Where am i" 처리기)와 사이트의 내 위치 그리기는 쓰지 않는다. 둘은 배포 때마다 바뀌어
 * 위치 표시를 끊어 왔고, 그동안 지도 상태의 좌표 변환과 화면 변환은 같은 이름으로 남았다. 그래서 사이트에서는
 * 지도 컨테이너와 두 변환만 빌린다. 지형과 함께 움직이는 자리는 사이트와 같은 변환으로 구해야 맞는다. 변환을
 * 앱에 따로 구현하면 사이트가 식을 바꿀 때 경고 없이 틀린 자리에 그린다. 변환이 사라지면 그리지 않고
 * map-unavailable로 알린다.
 * 층은 사이트가 고른다. 위치를 보일 때 같은 좌표를 사이트 지도 상태의 playerPos에도 써서 사이트의 층 자동
 * 선택만 깨운다(아래 syncSitePosition). 사이트가 그 좌표로 하는 일은 층 선택뿐이고, 사이트의 원과 핑은 사이트의
 * 표시 상태가 꺼져 있어 그려지지 않는다. 그 밖의 사이트 내 위치 기능(스쿼드 공유, 사이트 UI의 위치 표시)에는
 * 반영되지 않으며, 다른 마커와 지도 기능은 사이트의 것을 그대로 쓴다.
 */
(function () {
    'use strict';
    const VERSION = 7;
    if (window.tanukiPilot?.version === VERSION && typeof window.tanukiPilot.showScreenshot === 'function'
        && typeof window.tanukiPilot.isRendered === 'function' && typeof window.tanukiPilot.status === 'function'
        && typeof window.tanukiPilot.hasPosition === 'function'
        && typeof window.tanukiPilot.revealPosition === 'function') return true;

    // 화면 밖이나 가장자리에 걸친 마커는 보이지 않는 것과 같다. 이 비율 안쪽에 없으면 가운데로 옮긴다.
    // Local 미니맵(viewer/main.js)과 같은 값이다.
    const REVEAL_INSET = 0.15;

    // INTENT
    // 파일명 해석은 Local 미니맵(viewer/coords.js의 parseScreenshot)과 같은 식이다. 같은 파일명이 두 모드에서
    // 같은 자리와 방향에 찍혀야 하므로 받아들이는 형식, 축 순서, 방향 식을 맞춘다(verify-map-recovery.mjs가 대조).
    // 축 이름은 사이트 지도 상태의 좌표 변환이 받는 축이다. 파일명의 첫째 수가 y, 둘째(높이)가 z, 셋째가 x다.
    const NUMBER = '-?(?:\\d+(?:\\.\\d*)?|\\.\\d+)(?:e[+-]?\\d+)?';
    const SCREENSHOT_PATTERN = new RegExp(
        `\\d{4}-\\d{2}-\\d{2}\\[\\d{2}-\\d{2}\\]_(?<y>${NUMBER}),\\s*(?<z>${NUMBER}),\\s*(?<x>${NUMBER})` +
        `_(?<qx>${NUMBER}),\\s*(?<qy>${NUMBER}),\\s*(?<qz>${NUMBER}),\\s*(?<qw>${NUMBER})_`, 'i');

    // 사이트의 층 데이터가 채워지기를 기다리는 한계와 확인 간격(ms). 층 데이터는 지도 문서와 함께 도착하므로
    // 느린 연결에서도 기다릴 수 있게 한계를 넉넉히 둔다. 한계를 넘기면 기다리지 않고 좌표를 넘긴다.
    const LEVEL_WAIT = 30000;
    const LEVEL_POLL = 100;

    let lastFailure = null;
    // 마커에 마지막으로 맡긴 위치. 파일명, 해석한 좌표와 방향, 그때 고른 지도 상태와 맵 주소를 묶는다.
    let currentAnchor = null;
    // 마지막으로 핑을 켠 파일명. 같은 파일을 다시 맡길 때는 핑을 켜지 않는다.
    let pingedFilename = null;
    // 사이트에 넘길 좌표(층 데이터가 채워지기를 기다리는 중)와 그 확인 타이머, 마지막으로 넘긴 결과
    let siteSync = null;
    let siteSyncTimer = 0;
    let levelState = 'idle';

    /** 파일명에서 좌표(x, y는 평면, z는 높이)와 바라보는 방향(도, 0~360)을 읽는다. 읽지 못하면 null */
    function parseScreenshot(filename) {
        const match = SCREENSHOT_PATTERN.exec(String(filename));
        if (!match) return null;
        const value = Object.fromEntries(Object.entries(match.groups).map(([key, text]) => [key, Number(text)]));
        if (!Object.values(value).every(Number.isFinite)) return null;
        // 쿼터니언을 정규화하지 않고 수평 시선 벡터를 구한 뒤 (0, 1)과의 각을 잰다. 크기는 각에 영향이 없다.
        const directionX = 2 * (value.qx * value.qz + value.qw * value.qy);
        const directionZ = 1 - 2 * (value.qx * value.qx + value.qy * value.qy);
        if (Math.hypot(directionX, directionZ) === 0) return null;
        const look = ((Math.atan2(directionX, directionZ) * 180 / Math.PI) % 360 + 360) % 360;
        return { x: value.x, y: value.y, z: value.z, look };
    }

    // INTENT
    // 2026-10 판부터 지도 상태는 렌더 트리에 없고 map-state-capture.js가 만들어지는 순간에 기록해 둔 것만
    // 있다. 맵을 옮기면 새 상태가 생기고 새 페이지가 붙기 전에는 이전 지도가 화면에 남으므로, 화면에 붙은
    // 상태 가운데 가장 나중에 만든 것을 쓴다. 기록 몇 개만 훑으므로 매 프레임 불러도 된다.
    function mapStates() {
        return window[Symbol.for('TanukiTarkovMap.mapStates')]?.states?.() ?? null;
    }

    function getMap() {
        const states = mapStates() ?? [];
        for (let i = states.length - 1; i >= 0; i--) {
            const state = states[i];
            if (state.cont?.isConnected && typeof state.gamePosToMapPos === 'function'
                && typeof state.mapPosToScreenPos === 'function') return state;
        }
        return null;
    }

    // 기록 스크립트가 빠졌는지, 페이지가 지도 상태를 만든 뒤에 들어갔는지(또는 모양이 바뀌었는지) 구분한다
    function mapUnavailable() {
        return 'map-unavailable (' + (mapStates() ? 'not-captured' : 'capture-missing') + ')';
    }

    function pilotService() {
        return document.getElementById('__nuxt')?.__vue_app__?.config?.globalProperties?.$nuxt?.$pilot ?? null;
    }

    // 게임 평면 좌표를 지도 컨테이너 기준 화면 좌표로 옮긴다. 사이트가 자기 내 위치를 그릴 때 쓰는 변환과 같다.
    function screenPoint(map, position) {
        const point = map.gamePosToMapPos(position.x, position.y);
        const screen = point && map.mapPosToScreenPos(point.x, point.y);
        return Number.isFinite(screen?.x) && Number.isFinite(screen?.y) ? { x: screen.x, y: screen.y } : null;
    }

    // INTENT
    // 방향도 지도별 각도 식을 복사하지 않고 같은 두 변환으로 구한다. 시선 방향의 벡터를 화면으로 투영하므로
    // 지도마다 다른 회전과 사이트의 지도 회전이 함께 반영된다. 사이트의 좌표 반올림이 각도를 흔들지 않도록
    // 단위 벡터를 확대한 뒤 변환한다. 화면 위쪽이 0도이고 시계 방향으로 잰다.
    function screenAngle(map, look) {
        const angle = look * Math.PI / 180;
        const origin = map.gamePosToMapPos(0, 0);
        const forward = map.gamePosToMapPos(1000 * Math.cos(angle), 1000 * Math.sin(angle));
        const start = map.mapPosToScreenPos(origin.x, origin.y);
        const end = map.mapPosToScreenPos(forward.x, forward.y);
        const dx = end.x - start.x, dy = end.y - start.y;
        return Number.isFinite(dx) && Number.isFinite(dy) && Math.hypot(dx, dy) > 0
            ? Math.atan2(dx, -dy) * 180 / Math.PI : null;
    }

    // INTENT
    // 맡긴 위치를 계속 따라갈지 정한다. 다른 맵 주소로 옮기면 그 위치는 끝난 것으로 보고 같은 지도로 돌아와도
    // 다시 그리지 않는다. 앱은 다른 맵으로 가면 보관한 스크린샷을 버리므로, 지난 레이드의 위치가 되살아나지
    // 않게 맞춘다. 지도 상태의 컨테이너가 화면에서 떨어지면 그 지도도 끝난 것이다. 그때는 앱의 다음 위치
    // 점검이 같은 파일을 새 지도에 다시 맡긴다.
    function alive(anchor) {
        if (!anchor.expired && (location.pathname !== anchor.path || !anchor.map.cont?.isConnected)) anchor.expired = true;
        return !anchor.expired;
    }

    // INTENT
    // 맡긴 위치를 그릴 컨테이너와 자리, 화면 각도. map-markers.js가 매 프레임 부르고, 그리지 않을 때는 null이다.
    // 같은 주소에서 사이트가 지도 상태를 새로 만들면 지난 상태는 더 갱신되지 않아 그 변환으로 구한 자리가
    // 지형과 어긋난다. 그래서 화면에 붙은 가장 새 지도 상태일 때만 돌려준다. 사이트가 컨테이너 요소만 바꾸면
    // 지도 상태의 cont를 따라 새 컨테이너에 붙는다.
    function locate(anchor) {
        const map = anchor.map;
        if (!alive(anchor) || getMap() !== map) return null;
        try {
            const point = screenPoint(map, anchor.position), angle = screenAngle(map, anchor.position.look);
            anchor.error = null;
            return point && angle !== null ? { container: map.cont, x: point.x, y: point.y, angle } : null;
        } catch (error) {
            anchor.error = error.message;
            return null;
        }
    }

    function marker() {
        const view = window.tanukiMarker;
        return view && typeof view.show === 'function' && typeof view.isShown === 'function' ? view : null;
    }

    // 맡긴 위치가 지금 화면의 지도에 실제로 그려져 있는지. 사용자가 지도를 옮겨 마커가 화면 밖에 있어도 그려진 것이다.
    function isShowing() {
        return !!currentAnchor && alive(currentAnchor) && marker()?.isShown() === true;
    }

    // INTENT
    // 새 위치가 지도 화면 밖이면 사용자는 위치를 받지 못한 것과 같다. 마커가 화면 안쪽에 있으면 사용자가
    // 맞춘 화면을 그대로 두고, 아닐 때만 사이트의 이동 함수로 가운데에 놓는다. 주기 점검(isRendered)에서는
    // 화면을 옮기지 않는다. 화면을 옮겼으면 true를 돌려준다. map-keep-visible.js가 지도를 맞춘 뒤에도 불러,
    // 맞춤 때문에 마커가 화면 밖에 남지 않게 한다(지형이 창보다 커서 최소 배율에 걸리는 맵, 맞추기 전에 받은 위치).
    function revealMarker(anchor) {
        const map = anchor.map;
        const point = screenPoint(map, anchor.position);
        const view = map.cont?.getBoundingClientRect?.();
        if (!point || !view?.width || !view.height || typeof map.centerOnPosition !== 'function') return false;
        const insetX = view.width * REVEAL_INSET, insetY = view.height * REVEAL_INSET;
        if (point.x >= insetX && point.x <= view.width - insetX
            && point.y >= insetY && point.y <= view.height - insetY) return false;
        const target = map.gamePosToMapPos(anchor.position.x, anchor.position.y);
        map.centerOnPosition(target.x, target.y);
        return true;
    }

    // INTENT
    // 층은 사이트가 고른다. 사이트는 지도 문서에서 층마다 높이 범위와 구역을 받아 층 데이터 표에 채워 두고, 지도
    // 상태의 playerPos가 바뀌면 그 위치로 층을 바꾼다(구역 안이고 높이가 맞는 층을 먼저, 없으면 높이 범위로.
    // Local 미니맵이 쓰는 viewer/coords.js의 levelAtPosition과 같은 규칙이다). 앱이 위치를 직접 그리면 playerPos가
    // 그대로라 층도 그대로 남는다.
    // 그래서 위치를 보일 때 같은 좌표를 playerPos의 x, y, z에 써서 사이트의 층 선택을 깨운다.
    // - 처리기("Where am i") 대신 상태에 쓴다. 그 처리기도 같은 칸을 쓰는 일만 하고, 처리기를 찾는 경로는 배포마다
    //   바뀌어 왔다. 칸은 원래 객체에 값을 쓴다. 사이트는 그 객체를 지켜보므로 객체를 바꿔 끼우면 반응하지 않는다.
    // - 층(selectedLevel)은 쓰지 않는다. 사이트의 층 선택 상태에서 계산되는 값이라 직접 쓰면 Levels 패널과
    //   마커 필터가 어긋난다.
    // - 사이트는 층 데이터가 채워지기 전에 바뀐 위치로는 층을 고르지 않고, 데이터가 나중에 채워져도 다시 고르지
    //   않는다. 앱은 맵을 열자마자 위치를 맡기므로 층 데이터 표가 채워질 때까지 기다렸다가 쓴다. 표를 찾지
    //   못하면(기록 조건이 맞지 않는 판) 기다리지 않고 바로 쓴다.
    // - 같은 좌표를 다시 쓰면 사이트가 반응하지 않는다. 같은 스크린샷을 다시 맡겨도 사용자가 손으로 고른 층은 남는다.
    function levelSet() {
        const sets = window[Symbol.for('TanukiTarkovMap.mapStates')]?.levelSets?.() ?? [];
        return sets.length ? sets[sets.length - 1] : null;
    }

    // 사이트가 층을 고를 수 있는지. 사이트와 같은 조건(높이 범위나 구역을 가진 층이 하나라도 있음)이다.
    function levelsReady(levels) {
        return Object.values(levels).some(level => (Array.isArray(level?.height) && level.height.length === 2)
            || (Array.isArray(level?.zones) && level.zones.length > 0));
    }

    function syncSitePosition(anchor) {
        clearTimeout(siteSyncTimer);
        siteSync = { anchor, deadline: Date.now() + LEVEL_WAIT };
        flushSitePosition();
    }

    function flushSitePosition() {
        siteSyncTimer = 0;
        const request = siteSync;
        if (!request) return;
        const { anchor } = request, map = anchor.map, playerPos = map.playerPos;
        if (!alive(anchor) || getMap() !== map) { siteSync = null; levelState = 'map-changed'; return; }
        if (!playerPos || typeof playerPos !== 'object') { siteSync = null; levelState = 'no-player-position'; return; }
        const levels = levelSet();
        if (levels && Object.keys(levels).length < 2) { siteSync = null; levelState = 'single-level'; return; }
        if (levels && !levelsReady(levels) && Date.now() < request.deadline) {
            levelState = 'waiting';
            siteSyncTimer = setTimeout(flushSitePosition, LEVEL_POLL);
            return;
        }
        siteSync = null;
        playerPos.x = anchor.position.x;
        playerPos.y = anchor.position.y;
        playerPos.z = anchor.position.z;
        levelState = !levels ? 'no-level-data' : levelsReady(levels) ? 'synced' : 'level-data-timeout';
    }

    // 위치를 표시하고 마커가 지도에 실제로 보이면 true. 실패하면 이유를 status()로 남긴다.
    function showScreenshot(filename) {
        const position = parseScreenshot(filename);
        if (!position) { lastFailure = 'invalid-screenshot'; return false; }
        const map = getMap();
        if (!map) { lastFailure = mapUnavailable(); return false; }
        const view = marker();
        if (!view) { lastFailure = 'marker-unavailable'; return false; }
        const anchor = { filename, position, map, path: location.pathname, expired: false, error: null };
        // INTENT: 핑은 위치가 바뀌었다고 알리는 표시라 새 스크린샷이 처음 보일 때만 켠다. 앱은 표시가 사라졌다고
        // 판단하면 같은 파일을 다시 맡기는데, 그때마다 켜면 새 스크린샷이 없는데도 마커가 커졌다 줄어든다.
        const visible = view.show({ locate: () => locate(anchor), alive: () => alive(anchor),
            ping: pingedFilename !== filename });
        currentAnchor = anchor;
        if (!visible) {
            lastFailure = anchor.error ? 'site-error: ' + anchor.error : 'position-not-shown';
            return false;
        }
        pingedFilename = filename;
        revealMarker(anchor);
        syncSitePosition(anchor);
        lastFailure = null;
        return true;
    }

    window.tanukiPilot = {
        version: VERSION, showScreenshot, parseScreenshot, getMap,
        hasPosition: isShowing,
        // 앱의 주기 점검. 마지막으로 맡긴 파일명이고 마커가 지금 지도에 보이면 true. 화면은 옮기지 않는다.
        isRendered: (filename) => currentAnchor?.filename === filename && isShowing(),
        status: () => lastFailure ?? (getMap() ? 'ready' : mapUnavailable()),
        markerPoint: () => isShowing() ? screenPoint(currentAnchor.map, currentAnchor.position) : null,
        revealPosition: () => isShowing() && revealMarker(currentAnchor),
        // 층 자동 선택의 진행 상태와 사이트가 지금 보이는 층. 검사 도구와 CDP 진단이 읽는다
        levelSync: () => ({ state: levelState, level: getMap()?.selectedLevel ?? null }),
        completeQuest(questId) {
            const pilot = window.pilot ?? pilotService();
            if (typeof pilot?.questComplete !== 'function') return false;
            try { pilot.questComplete(questId); return true; }
            catch (error) { return false; }
        }
    };
    return true;
})();
