/**
 * 사이트의 위치 연동 경로는 여기서만 판별한다.
 * 온라인 페이지와 저장된 사본이 서로 다른 진입점을 제공할 수 있어 함수 존재 여부로 선택한다.
 * 파일명의 좌표와 회전값은 앱 입력이다. 사이트의 지도별 좌표 변환을 이용하되 방향은 앱이 그린다.
 */
(function () {
    'use strict';
    if (window.tanukiPilot?.version === 3 && typeof window.tanukiPilot.sendScreenshot === 'function'
        && typeof window.tanukiPilot.isReady === 'function' && typeof window.tanukiPilot.status === 'function'
        && typeof window.tanukiPilot.getMapHeading === 'function'
        && typeof window.tanukiPilot.isRendered === 'function') return true;

    let lastFailure = null;
    let requestVersion = 0;
    const number = '(-?\\d+(?:\\.\\d+)?(?:e[+-]?\\d+)?)';
    const screenshotPattern = new RegExp('_' + [number, number, number].join(',\\s*') +
        '_' + [number, number, number, number].join(',\\s*') + '(?:_|\\s|$)', 'i');

    function parseScreenshot(filename) {
        const match = screenshotPattern.exec(filename);
        if (!match) return null;
        const values = match.slice(1).map(Number);
        if (!values.every(Number.isFinite)) return null;
        const [x, y, z, qx, qy, qz, qw] = values;
        const norm = Math.hypot(qx, qy, qz, qw);
        if (norm === 0) return null;
        const [a, b, c, d] = [qx, qy, qz, qw].map(v => v / norm);
        const heading = Math.atan2(2 * (a * c + d * b), 1 - 2 * (a * a + b * b)) * 180 / Math.PI;
        return { x, y, z, heading };
    }

    function getPilot() {
        const legacy = window.pilot;
        if (typeof legacy?.positionFromScreenshot === 'function') return legacy;
        // Nuxt가 제공한 Pilot 서비스에는 온라인 지도의 위치 전달 함수가 있다.
        // 빌드마다 바뀌는 청크 이름이나 축약 변수명으로 함수를 찾지 않는다.
        const root = document.getElementById('__nuxt');
        return root?.__vue_app__?.config?.globalProperties?.$nuxt?.$pilot ?? null;
    }

    function isReady() {
        const pilot = getPilot();
        return !!document.body && (typeof pilot?.positionFromScreenshot === 'function' ||
            typeof pilot?.positionUpdate === 'function');
    }

    // 지도별 각도를 복사해 두면 지도 갱신 때 화살표만 틀어진다. 렌더 중인 Vue 트리에서
    // 좌표 변환 기능을 가진 map 속성을 찾고, 사이트와 같은 변환으로 방향 벡터를 투영한다.
    // 컴포넌트 이름과 setup의 축약 변수명에는 의존하지 않는다.
    function getMap() {
        // INTENT: 같은 주소에서 컴포넌트만 교체되면 이전 지도와 wrap이 잠시 남을 수 있다.
        // 주소와 isConnected만으로 캐시를 재사용하지 않고 현재 Vue 트리에서 다시 찾는다.
        const nodes = [document.getElementById('__nuxt')?._vnode];
        const seen = new Set();
        for (let i = 0; i < nodes.length; i++) {
            const node = nodes[i];
            if (!node || typeof node !== 'object' || seen.has(node)) continue;
            seen.add(node);
            const map = node.props?.map;
            if (map?.wrap?.isConnected && typeof map.gamePosToMapPos === 'function'
                && typeof map.mapPosToScreenPos === 'function') {
                return map;
            }
            nodes.push(node.component?.subTree, node.suspense?.activeBranch);
            if (Array.isArray(node.children)) nodes.push(...node.children);
        }
        return null;
    }

    function getMapHeading(heading) {
        const map = getMap();
        if (!map) return null;
        const angle = heading * Math.PI / 180;
        const origin = map.gamePosToMapPos(0, 0);
        // 사이트의 좌표 반올림이 각도를 흔들지 않도록 단위 벡터를 확대한 뒤 변환한다.
        const forward = map.gamePosToMapPos(100 * Math.cos(angle), 100 * Math.sin(angle));
        const start = map.mapPosToScreenPos(origin.x, origin.y);
        const end = map.mapPosToScreenPos(forward.x, forward.y);
        const dx = end.x - start.x, dy = end.y - start.y;
        return Number.isFinite(dx) && Number.isFinite(dy) && Math.hypot(dx, dy) > 0
            ? Math.atan2(dx, -dy) * 180 / Math.PI : null;
    }

    function positionMatches(position, map) {
        const current = map?.playerPos;
        return !!position && !!document.querySelector('.marker') && !!current &&
            current.x === position.z && current.y === position.x && current.z === position.y;
    }

    function isRendered(filename) {
        return positionMatches(parseScreenshot(filename), getMap());
    }

    async function sendScreenshot(filename) {
        const position = parseScreenshot(filename);
        if (!position) { lastFailure = 'invalid-screenshot'; return false; }
        const pilot = getPilot();
        if (!isReady()) { lastFailure = 'pilot-unavailable'; return false; }
        const version = ++requestVersion;
        const mapPath = location.pathname;
        const initialMap = getMap();
        const bridge = window.tanukiPilot;
        try {
            if (typeof pilot.positionUpdate === 'function') {
                // Pilot 좌표는 평면 (게임 Z, 게임 X)과 높이 (게임 Y)를 사용한다.
                await pilot.positionUpdate(position.z, position.x, position.y);
            } else {
                await pilot.positionFromScreenshot(filename);
            }
            // 호출 성공만으로 완료 처리하지 않는다. 늦게 마운트되는 지도는 사건을 받지 못할 수 있다.
            await new Promise(resolve => setTimeout(resolve, 0));
            // INTENT: CEF의 Promise 대기 시간이 끝나도 페이지 작업은 계속될 수 있다.
            // 새 요청이나 지도 이동 뒤 도착한 응답으로 새 화면의 방향을 바꾸지 않는다.
            if (version !== requestVersion || bridge !== window.tanukiPilot) return false;
            const map = getMap();
            if (mapPath !== location.pathname || (initialMap && initialMap !== map)) {
                lastFailure = 'map-changed';
                return false;
            }
            if (!positionMatches(position, map)) {
                lastFailure = 'position-not-rendered';
                return false;
            }
            if (window.tanukiDirection?.setHeading(position.heading) !== true) {
                lastFailure = 'direction-not-ready';
                return false;
            }
            lastFailure = null;
            return true;
        } catch (error) {
            if (version !== requestVersion || bridge !== window.tanukiPilot) return false;
            lastFailure = 'site-error: ' + error.message;
            return false;
        }
    }

    window.tanukiPilot = {
        version: 3, isReady, isRendered, sendScreenshot, getMapHeading,
        status: () => lastFailure ?? (isReady() ? 'ready' : 'pilot-unavailable'),
        completeQuest(questId) {
            const pilot = getPilot();
            if (typeof pilot?.questComplete !== 'function') return false;
            try { pilot.questComplete(questId); return true; }
            catch (error) { return false; }
        }
    };
    return true;
})();
