/**
 * 사이트의 지도 상태 객체와 층 데이터 표를 만들어지는 순간에 기록해 Pilot 브리지가 쓰게 한다.
 *
 * INTENT
 * 2026-10 판부터 사이트는 마커와 내 위치를 캔버스에 그리고, 좌표 변환과 이동 함수를 가진 지도 상태를
 * 하위 컴포넌트에 props로 넘기지 않는다. 하위 컴포넌트는 getter만 가진 얼린 객체(view)를 받고, 지도
 * 상태 자체는 페이지 컴포넌트의 클로저 안에만 남아 렌더 트리 어디에서도 닿지 않는다.
 * Vue는 reactive 객체를 만들 때 원본을 키로, 프록시를 값으로 WeakMap에 등록한다. 그 등록을 지켜보다가
 * 지도 상태의 모양(지도 컨테이너와 두 좌표 변환 함수 칸)을 가진 프록시만 기록한다. 같은 원본을 키로 쓰는
 * 다른 표(의존성 표)의 값은 그 모양이 아니므로 걸러진다.
 * 모양은 앱이 내 위치를 그리는 데 꼭 필요한 칸으로만 판정한다. 층 선택에만 쓰는 칸(사이트의 내 위치
 * playerPos)까지 요구하면 사이트가 그 칸만 바꿔도 기록이 비어 내 위치까지 그리지 못한다. 칸은 처음부터
 * null로 만들어지므로 함수가 들어오기 전인 등록 순간에도 판정할 수 있다.
 * 층 데이터 표(층 이름마다 층 번호 num과 함께 보일 층 목록 visible을 가진 표)도 같은 방법으로 기록한다.
 * 사이트는 지도 문서가 도착한 뒤 이 표에 층마다 높이 범위와 구역을 채우고, 그 전에 바뀐 위치로는 층을
 * 고르지 않는다. 브리지는 이 표가 채워졌는지 보고 사이트에 위치를 넘길 때를 정한다.
 * 페이지 스크립트가 지도 상태를 만들기 전에 실행돼야 하므로 page-health.js처럼 로드 시작 시점에 넣는다.
 * 늦게 들어가면 그 문서에는 기록이 없고, 브리지가 map-unavailable로 알린다. 원래 set의 동작과 반환값은
 * 그대로 두고 기록만 더한다.
 */
(function () {
    'use strict';
    const KEY = Symbol.for('TanukiTarkovMap.mapStates');
    if (window[KEY]) return true;

    const hasOwn = Object.prototype.hasOwnProperty;
    const nativeSet = WeakMap.prototype.set;
    // 맵을 옮길 때마다 새 지도 상태와 층 데이터 표가 생긴다. 지난 것은 페이지가 버리면 사라지도록 약한 참조로 둔다.
    const LIMIT = 8;
    let mapStates = [];
    let levelSets = [];

    function isMapState(value) {
        return value !== null && typeof value === 'object' && hasOwn.call(value, 'cont')
            && hasOwn.call(value, 'gamePosToMapPos') && hasOwn.call(value, 'mapPosToScreenPos');
    }

    // 원본(키)만 읽는다. 프록시의 값을 읽으면 그 순간 실행 중인 반응형 효과에 의존성이 더해질 수 있다.
    // 모양이 다른 큰 객체는 첫 칸에서 걸러지므로 등록이 잦아도 비용이 작다.
    function isLevelSet(value) {
        if (value === null || typeof value !== 'object' || Object.getPrototypeOf(value) !== Object.prototype) return false;
        let count = 0;
        for (const name in value) {
            if (!hasOwn.call(value, name)) continue;
            const level = value[name];
            if (level === null || typeof level !== 'object' || typeof level.num !== 'number' || !Array.isArray(level.visible)) return false;
            count++;
        }
        return count > 0;
    }

    // 같은 원본의 프록시인지. 의존성 표(Map)는 층 이름 칸이 없어 걸러진다.
    function hasSameNames(original, proxy) {
        if (proxy === null || typeof proxy !== 'object' || proxy instanceof Map) return false;
        for (const name in original) if (hasOwn.call(original, name) && !hasOwn.call(proxy, name)) return false;
        return true;
    }

    function remember(list, value) {
        list.push(new WeakRef(value));
        return list.length > LIMIT ? list.slice(-LIMIT) : list;
    }

    function alive(list) {
        return list.filter(record => record.deref() !== undefined);
    }

    WeakMap.prototype.set = function set(key, value) {
        const result = nativeSet.call(this, key, value);
        if (value !== key) {
            if (isMapState(key) && isMapState(value)) mapStates = remember(mapStates, value);
            else if (isLevelSet(key) && hasSameNames(key, value)) levelSets = remember(levelSets, value);
        }
        return result;
    };

    Object.defineProperty(window, KEY, {
        value: Object.freeze({
            version: 3,
            // 아직 살아 있는 지도 상태를 만든 순서대로 돌려준다
            states() {
                mapStates = alive(mapStates);
                return mapStates.map(record => record.deref());
            },
            // 아직 살아 있는 층 데이터 표를 만든 순서대로 돌려준다
            levelSets() {
                levelSets = alive(levelSets);
                return levelSets.map(record => record.deref());
            }
        })
    });
    return true;
})();
