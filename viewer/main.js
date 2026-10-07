import { createCamera } from './camera.js';
import { gameDirectionToMapDirection, gamePositionToMapPosition, parseScreenshot } from './coords.js';
import { createExtractionLayer, createPlayerMarker } from './markers.js';

/**
 * Local 미니맵의 진입점. 주소의 ?map=<맵 ID>로 resources의 지형, 설정, 추출구를 읽어 조립하고,
 * 앱이 부르는 window.tanukiViewer를 연다.
 *
 * window.tanukiViewer는 Online의 window.tanukiPilot과 같은 모양이라 앱은 두 모드를 같은 흐름으로 다룬다.
 * - showScreenshot(파일명): 위치와 방향을 표시하고, 마커가 실제로 보이면 true. 로딩 중이면 기억했다가
 *   준비되는 즉시 표시하고 false를 돌려준다. 앱은 isRendered가 true가 될 때까지 다시 부른다.
 * - isRendered(파일명): 마지막으로 표시한 파일명이고 마커가 보이면 true. 화면은 옮기지 않는다.
 * - setFaction(isPmc), setControlsVisible(visible): 로딩 전에 불러도 준비 뒤 적용한다.
 * - status(): 'loading', 'ready', 'error: <사유>'
 */
const RESOURCE_ROOT = new URL('../resources/', import.meta.url);

// 새 위치가 화면 가장자리 이 비율 안에 들면 가운데로 옮긴다. Online 브리지와 같은 값이다
const REVEAL_INSET = 0.15;

const elements = {
  container: document.getElementById('mapCont'),
  wrap: document.getElementById('mapWrap'),
  canvas: document.getElementById('markersCanvas'),
  marker: document.getElementById('playerMarker'),
  levelsPanel: document.getElementById('levelsPanel'),
  levelsList: document.getElementById('levelsList'),
  status: document.getElementById('loadStatus'),
};

const state = { status: 'loading', faction: 'pmc', pendingScreenshot: null, renderedScreenshot: null, map: null };

async function fetchResource(path, type) {
  const response = await fetch(new URL(path, RESOURCE_ROOT));
  if (!response.ok) throw new Error(`${path}를 읽지 못했습니다 (HTTP ${response.status})`);
  return type === 'json' ? response.json() : response.text();
}

function insertTerrain(svgText) {
  const parsed = new DOMParser().parseFromString(svgText, 'image/svg+xml');
  const root = parsed.documentElement;
  if (root.localName !== 'svg' || parsed.querySelector('parsererror')) throw new Error('map.svg를 해석하지 못했습니다');
  const svg = document.importNode(root, true);
  elements.wrap.replaceChildren(svg);
  return svg;
}

/**
 * 층은 한 번에 하나만 보인다. 변환기가 층마다 그 층을 골랐을 때의 완성된 그림(다른 층의 흐린
 * 윤곽 포함)을 그룹 하나로 만들었으므로, 사이트처럼 한 층을 고르는 방식으로 다룬다.
 */
function createLevels({ meta, svg, onChange }) {
  const levels = [...meta.levels].sort((a, b) => b.sourceLevel - a.sourceLevel);
  const bounds = new Map();
  let selected = (levels.find((level) => level.defaultVisible) ?? levels[0]).id;

  function group(level) {
    return level.terrainGroupId ? svg.querySelector(`[id="${CSS.escape(level.terrainGroupId)}"]`) : null;
  }

  // INTENT
  // 지형 범위는 보이는 층에서 실제로 그린 도형으로 잰다. 변환기가 층 아래에 까는 바탕 사각형
  // (data-map-background)은 좌표 공간 대부분을 덮으므로 빼고, 나머지 직계 자식의 범위를 합친다.
  // Online은 캔버스 픽셀로 재서 이 바탕까지 포함하지만, 자체 뷰어는 도형을 알 수 있어 더 좁게 잰다.
  // 표시 중인 그룹만 getBBox가 값을 돌려주므로 층을 고른 뒤에 잰다.
  function measure(target) {
    let left = Infinity, top = Infinity, right = -Infinity, bottom = -Infinity;
    for (const child of target.children) {
      if (child.matches('[data-map-background="true"], defs') || typeof child.getBBox !== 'function') continue;
      const box = child.getBBox();
      if (!box.width || !box.height) continue;
      left = Math.min(left, box.x); top = Math.min(top, box.y);
      right = Math.max(right, box.x + box.width); bottom = Math.max(bottom, box.y + box.height);
    }
    if (left < right && top < bottom) return { left, top, right, bottom };
    const box = target.getBBox();
    return box.width && box.height ? { left: box.x, top: box.y, right: box.x + box.width, bottom: box.y + box.height } : null;
  }

  function terrainBounds() {
    if (!bounds.has(selected)) bounds.set(selected, measure(group(levels.find((level) => level.id === selected)) ?? svg));
    return bounds.get(selected);
  }

  function select(levelId) {
    if (!levels.some((level) => level.id === levelId)) return;
    selected = levelId;
    for (const level of levels) {
      const element = group(level);
      if (element) element.style.display = level.id === selected ? '' : 'none';
    }
    for (const input of elements.levelsList.querySelectorAll('input')) {
      input.checked = input.value === selected;
      input.parentElement.classList.toggle('story', input.checked);
    }
    onChange(selected);
  }

  // 사이트의 Alt 휠과 같은 순서다. 목록은 위층부터 놓이고, 위로 굴리면 한 칸 위층으로 간다
  function step(up) {
    const index = levels.findIndex((level) => level.id === selected);
    const next = levels[Math.min(levels.length - 1, Math.max(0, index + (up ? -1 : 1)))];
    if (next.id !== selected) select(next.id);
  }

  // 사이트의 높이 기반 층 선택: 영역(zones)에 들고 높이가 맞는 층을 먼저, 없으면 높이 범위로 고른다.
  // 리소스에 높이 정보가 없는 맵은 층을 바꾸지 않는다.
  function inHeight(range, height) {
    return Array.isArray(range) && range.length === 2 && height >= range[0] && height < range[1];
  }
  function inZone(zone, x, y) {
    if (zone.rect?.length === 2) {
      let [[left, top], [right, bottom]] = zone.rect;
      if (zone.rotate) {
        const radians = -zone.rotate * Math.PI / 180;
        const rotate = ([px, py]) => [px * Math.cos(radians) - py * Math.sin(radians), px * Math.sin(radians) + py * Math.cos(radians)];
        [x, y] = rotate([x, y]);
        [left, top] = rotate([left, top]);
        [right, bottom] = rotate([right, bottom]);
      }
      return x >= Math.min(left, right) && x <= Math.max(left, right) && y >= Math.min(top, bottom) && y <= Math.max(top, bottom);
    }
    if (zone.poly?.length >= 3) {
      let inside = false;
      for (let i = 0, j = zone.poly.length - 1; i < zone.poly.length; j = i++) {
        const [xi, yi] = zone.poly[i], [xj, yj] = zone.poly[j];
        if ((yi > y) !== (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
      }
      return inside;
    }
    return null;
  }
  function levelAt(x, y, height) {
    const zoned = levels.find((level) =>
      level.zones?.some((zone) => inZone(zone, x, y) === true && inHeight(zone.height, height)));
    if (zoned) return zoned.id;
    const ranged = levels.find((level) =>
      level.zones?.some((zone) => inZone(zone, x, y) === null && inHeight(zone.height, height))
      || inHeight(level.height, height));
    return ranged?.id ?? null;
  }

  if (levels.length > 1) {
    for (const level of levels) {
      const row = document.createElement('div');
      row.className = 'no-wrap';
      const label = document.createElement('label');
      label.className = 'pointer';
      const input = document.createElement('input');
      input.type = 'radio';
      input.name = 'layers';
      input.value = level.id;
      input.addEventListener('change', () => select(level.id));
      label.append(input, ` ${level.label}`);
      row.append(label);
      elements.levelsList.append(row);
    }
    elements.levelsPanel.hidden = false;
  }

  return {
    select, step, terrainBounds, levelAt,
    get selected() { return selected; },
    number: (levelId) => levels.length > 1 ? levels.find((level) => level.id === levelId)?.sourceLevel ?? null : null,
  };
}

function showPosition(filename) {
  const position = parseScreenshot(filename);
  if (!position) return false;
  const { meta, levels, camera, player } = state.map;
  // 사이트처럼 위치의 높이로 층을 고른다. 층이 바뀌면 지형 범위도 바뀌므로 화면을 옮기기 전에 정한다.
  const level = levels.levelAt(position.x, position.y, position.z);
  if (level && level !== levels.selected) levels.select(level);
  const point = gamePositionToMapPosition(position.x, position.y, meta.transform);
  player.show(point, gameDirectionToMapDirection(position.look, meta.transform));

  // 화면 안쪽에 있으면 사용자가 맞춘 화면을 그대로 둔다
  const screen = camera.toScreen(point);
  const { width, height } = camera.viewSize();
  if (screen.x < width * REVEAL_INSET || screen.x > width * (1 - REVEAL_INSET)
    || screen.y < height * REVEAL_INSET || screen.y > height * (1 - REVEAL_INSET)) camera.centerOn(point);
  state.renderedScreenshot = filename;
  return player.isVisible();
}

window.tanukiViewer = Object.freeze({
  status: () => state.status,
  showScreenshot(filename) {
    if (state.status !== 'ready') {
      state.pendingScreenshot = filename;
      return false;
    }
    return showPosition(filename);
  },
  isRendered: (filename) => state.status === 'ready' && state.renderedScreenshot === filename && state.map.player.isVisible(),
  setFaction(isPmc) {
    state.faction = isPmc ? 'pmc' : 'scav';
    state.map?.extractions.setFaction(state.faction);
    return true;
  },
  setControlsVisible(visible) {
    elements.container.classList.toggle('controls-hidden', !visible);
    return true;
  },
});

async function initialize() {
  const mapId = new URLSearchParams(location.search).get('map');
  const manifest = await fetchResource('manifest.json', 'json');
  if (!manifest.maps.includes(mapId)) throw new Error(`리소스에 ${mapId} 맵이 없습니다`);
  const [meta, markerData, svgText] = await Promise.all([
    fetchResource(`maps/${mapId}/meta.json`, 'json'),
    fetchResource(`maps/${mapId}/markers.json`, 'json'),
    fetchResource(`maps/${mapId}/map.svg`, 'text'),
  ]);
  document.title = `${meta.title} - Tanuki Local Map`;
  const svg = insertTerrain(svgText);

  let camera = null, extractions = null, player = null;
  const levels = createLevels({
    meta, svg,
    onChange(levelId) { extractions?.setLevel(levelId); camera?.render(); },
  });
  camera = createCamera({
    container: elements.container,
    wrap: elements.wrap,
    minZoom: meta.minZoom,
    maxZoom: meta.maxZoom,
    terrainBounds: levels.terrainBounds,
    onChange() { extractions?.redraw(); player?.place(); },
    onAltWheel: levels.step,
  });
  extractions = createExtractionLayer({
    canvas: elements.canvas, markerData, baseZoom: meta.zoom, levelNumber: levels.number, camera,
  });
  player = createPlayerMarker({ element: elements.marker, camera });
  levels.select(levels.selected);
  extractions.setFaction(state.faction);
  camera.fit();

  state.map = { meta, levels, camera, extractions, player };
  state.status = 'ready';
  elements.status.hidden = true;
  if (state.pendingScreenshot) showPosition(state.pendingScreenshot);
}

initialize().catch((error) => {
  console.error(error);
  state.status = `error: ${error.message}`;
  elements.status.classList.add('is-error');
  elements.status.textContent = `지도를 열지 못했습니다. ${error.message}`;
});
