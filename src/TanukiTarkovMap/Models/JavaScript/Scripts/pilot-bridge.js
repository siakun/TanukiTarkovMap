/**
 * 사이트의 위치 입력 경로는 여기서만 판별한다.
 * 사이트는 배포 때마다 위치를 받는 경로를 바꿨다. 경로마다 어댑터 하나를 두고 페이지에 있는
 * 첫 경로를 쓰며, 이름이 아니라 함수와 상태가 실제로 있는지로 판별한다.
 * 파일명의 좌표와 회전값은 앱 입력이다. 사이트의 지도별 좌표 변환을 이용하되 방향은 앱이 그린다.
 */
(function () {
    'use strict';
    const VERSION = 4;
    if (window.tanukiPilot?.version === VERSION && typeof window.tanukiPilot.sendScreenshot === 'function'
        && typeof window.tanukiPilot.isReady === 'function' && typeof window.tanukiPilot.status === 'function'
        && typeof window.tanukiPilot.getMapHeading === 'function'
        && typeof window.tanukiPilot.isRendered === 'function'
        && typeof window.tanukiPilot.revealPosition === 'function') return true;

    let lastFailure = null;
    let requestVersion = 0;
    const number = '(-?\\d+(?:\\.\\d+)?(?:e[+-]?\\d+)?)';
    const screenshotPattern = new RegExp('_' + [number, number, number].join(',\\s*') +
        '_' + [number, number, number, number].join(',\\s*') + '(?:_|\\s|$)', 'i');

    // 화면 밖이나 가장자리에 걸친 마커는 보이지 않는 것과 같다. 이 비율 안쪽에 없으면 가운데로 옮긴다.
    const REVEAL_INSET = 0.15;

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

    // 렌더 중인 Vue 트리에서 조건에 맞는 컴포넌트 props를 찾는다. 템플릿의 prop 이름은 번들을
    // 압축해도 남지만 빌드마다 바뀌는 청크 이름과 setup의 축약 변수명은 남지 않으므로 그쪽은 쓰지 않는다.
    // INTENT: 같은 주소에서 컴포넌트만 교체되면 이전 지도가 잠시 남는다. 결과를 캐시하지 않는다.
    function findProps(predicate) {
        const nodes = [document.getElementById('__nuxt')?._vnode];
        const seen = new Set();
        for (let i = 0; i < nodes.length; i++) {
            const node = nodes[i];
            if (!node || typeof node !== 'object' || seen.has(node)) continue;
            seen.add(node);
            if (node.props && predicate(node.props)) return node.props;
            nodes.push(node.component?.subTree, node.suspense?.activeBranch);
            if (Array.isArray(node.children)) nodes.push(...node.children);
        }
        return null;
    }

    // 지도별 각도를 복사해 두면 지도 갱신 때 화살표만 틀어진다. 좌표 변환 기능을 가진 map 속성을
    // 찾아 사이트와 같은 변환으로 방향 벡터를 투영하고, 위치 확인과 화면 이동에도 같은 객체를 쓴다.
    function getMap() {
        return findProps(props => props.map?.wrap?.isConnected
            && typeof props.map.gamePosToMapPos === 'function'
            && typeof props.map.mapPosToScreenPos === 'function')?.map ?? null;
    }

    function pilotService() {
        return document.getElementById('__nuxt')?.__vue_app__?.config?.globalProperties?.$nuxt?.$pilot ?? null;
    }

    // INTENT
    // 사이트가 제공해 온 위치 입력 경로들이다. 앞쪽 두 경로는 사이트의 프로그램용 입력이라 있으면
    // 그쪽을 쓰고, 둘 다 없는 지금 판(2026-10 이후)은 사용자가 스크린샷 파일명을 붙여 넣는
    // "Where am i" 입력의 처리기를 쓴다. 이 처리기는 파일명 해석과 마커 표시를 사이트가 직접 한다.
    const inputs = [
        {
            name: 'global-pilot',
            find() {
                const pilot = window.pilot;
                return typeof pilot?.positionFromScreenshot === 'function' ? pilot : null;
            },
            send(pilot, filename) { return pilot.positionFromScreenshot(filename); }
        },
        {
            name: 'pilot-service',
            find() {
                const pilot = pilotService();
                return typeof pilot?.positionUpdate === 'function' ? pilot : null;
            },
            // Pilot 좌표는 평면 (게임 Z, 게임 X)과 높이 (게임 Y)를 사용한다.
            send(pilot, filename, position) { return pilot.positionUpdate(position.z, position.x, position.y); }
        },
        {
            name: 'where-am-i',
            find() {
                return findProps(props => typeof props.onScreenPositionChange === 'function'
                    && props.playerData && typeof props.playerData === 'object'
                    && 'isPlayerMarkerVisible' in props.playerData);
            },
            send(panel, filename) {
                // 붙여 넣기 처리기는 좌표만 바꾸고 마커는 "Where am i" 창이 열린 동안만 보인다.
                // Pilot 입력과 같은 표시 상태를 켜 창을 열지 않고도 마커가 남게 한다.
                panel.playerData.isPlayerMarkerVisible = true;
                panel.onScreenPositionChange({ target: { value: filename } });
                panel.onMakePlayerMarkerPing?.();
            }
        }
    ];

    function findInput() {
        for (const input of inputs) {
            const target = input.find();
            if (target) return { input, target };
        }
        return null;
    }

    function isReady() {
        return !!document.body && !!findInput();
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

    function markerElement() {
        return document.querySelector('.marker, .marker-arrow');
    }

    function positionMatches(position, map) {
        const current = map?.playerPos;
        return !!position && !!markerElement() && !!current &&
            current.x === position.z && current.y === position.x && current.z === position.y;
    }

    function isRendered(filename) {
        return positionMatches(parseScreenshot(filename), getMap());
    }

    // INTENT
    // 새 위치가 지도 화면 밖이면 사용자는 위치를 받지 못한 것과 같다. 사이트의 Pilot 입력은 확대한
    // 상태에서만 가운데로 옮기는데, 작은 오버레이 창에서는 그보다 낮은 배율에서도 마커가 화면 밖에
    // 놓인다. 마커가 화면 안쪽에 있으면 사용자가 맞춘 화면을 그대로 두고, 아닐 때만 사이트의
    // 이동 함수로 가운데에 놓는다. 주기 점검(isRendered)에서는 화면을 옮기지 않는다.
    // 화면을 옮겼으면 true를 돌려준다. map-keep-visible.js가 지도를 맞춘 뒤에도 불러, 맞춤 때문에
    // 마커가 화면 밖에 남지 않게 한다(지형이 창보다 커서 최소 배율에 걸리는 맵, 맞추기 전에 받은 위치).
    function revealMarker(map) {
        const marker = markerElement();
        const view = map.cont?.getBoundingClientRect?.();
        if (!marker || !view?.width || !view.height || typeof map.centerOnPosition !== 'function') return false;
        const box = marker.getBoundingClientRect();
        const x = box.left + box.width / 2, y = box.top + box.height / 2;
        const insetX = view.width * REVEAL_INSET, insetY = view.height * REVEAL_INSET;
        if (x >= view.left + insetX && x <= view.right - insetX
            && y >= view.top + insetY && y <= view.bottom - insetY) return false;
        const point = map.gamePosToMapPos(map.playerPos.x, map.playerPos.y);
        map.centerOnPosition(point.x, point.y);
        return true;
    }

    async function sendScreenshot(filename) {
        const position = parseScreenshot(filename);
        if (!position) { lastFailure = 'invalid-screenshot'; return false; }
        const found = findInput();
        if (!document.body || !found) { lastFailure = 'pilot-unavailable'; return false; }
        const version = ++requestVersion;
        const mapPath = location.pathname;
        const initialMap = getMap();
        const bridge = window.tanukiPilot;
        try {
            await found.input.send(found.target, filename, position);
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
                lastFailure = 'position-not-rendered (' + found.input.name + ')';
                return false;
            }
            revealMarker(map);
            if (window.tanukiDirection?.setHeading(position.heading) !== true) {
                lastFailure = 'direction-not-ready';
                return false;
            }
            lastFailure = null;
            return true;
        } catch (error) {
            if (version !== requestVersion || bridge !== window.tanukiPilot) return false;
            lastFailure = 'site-error (' + found.input.name + '): ' + error.message;
            return false;
        }
    }

    window.tanukiPilot = {
        version: VERSION, isReady, isRendered, sendScreenshot, getMapHeading, getMap,
        hasPosition: () => !!markerElement(),
        revealPosition() {
            const map = getMap();
            return !!map?.playerPos && revealMarker(map);
        },
        input: () => findInput()?.input.name ?? null,
        status: () => lastFailure ?? (isReady() ? 'ready' : 'pilot-unavailable'),
        completeQuest(questId) {
            const pilot = window.pilot ?? pilotService();
            if (typeof pilot?.questComplete !== 'function') return false;
            try { pilot.questComplete(questId); return true; }
            catch (error) { return false; }
        }
    };
    return true;
})();
