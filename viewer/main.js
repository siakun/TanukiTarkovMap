import {
  gameDirectionToMapDirection,
  gamePositionToMapPosition,
  parseScreenshotPosition,
} from './coords.js';
import { createMapView } from './map-view.js';
import { createMapMarkers, createPositionMarker } from './markers.js';

/**
 * 진입점은 URL의 맵 ID를 resources/manifest.json에서 확인하고 해당 리소스만 조립한다.
 * 맵 목록과 설정을 코드에 복제하지 않아 새 리소스를 넣을 때 뷰어를 고칠 필요가 없다.
 */
const RESOURCE_SCHEMA_VERSION = 1;
const resourceRoot = new URL('../resources/', import.meta.url);
const mapTitle = document.querySelector('#mapTitle');
const mapCode = document.querySelector('#mapCode');
const zoomReadout = document.querySelector('#zoomReadout');
const levelControls = document.querySelector('#levelControls');
const markerControls = document.querySelector('#markerControls');
const factionControls = document.querySelector('#factionControls');
const positionForm = document.querySelector('#positionForm');
const coordinateResult = document.querySelector('#coordinateResult');
const viewport = document.querySelector('#mapViewport');
const loadStatus = document.querySelector('#loadStatus');
let showPositionOnMap = null;
let pendingPosition = null;

function normalizePosition(position) {
  if (!position || typeof position !== 'object') {
    throw new TypeError('위치 정보가 없습니다.');
  }

  const normalized = {
    x: Number(position.x),
    y: Number(position.y),
    z: position.z === undefined ? 0 : Number(position.z),
    look: position.look === null || position.look === undefined ? null : Number(position.look),
  };
  for (const name of ['x', 'y', 'z']) {
    if (!Number.isFinite(normalized[name])) {
      throw new TypeError(`${name} 좌표는 유한한 숫자여야 합니다.`);
    }
  }
  if (normalized.look !== null && !Number.isFinite(normalized.look)) {
    throw new TypeError('look은 유한한 숫자이거나 null이어야 합니다.');
  }
  return normalized;
}

function requestPosition(position) {
  const normalized = normalizePosition(position);
  if (!showPositionOnMap) {
    // 리소스를 읽는 동안 들어온 위치는 버리지 않고 가장 최근 값 하나만 준비 뒤 표시한다.
    pendingPosition = normalized;
    return { queued: true };
  }
  return showPositionOnMap(normalized);
}

window.tanukiViewer = Object.freeze({
  get ready() {
    return showPositionOnMap !== null;
  },
  showPosition(position) {
    return requestPosition(position);
  },
  showPositionFromScreenshot(filename) {
    return requestPosition(parseScreenshotPosition(filename));
  },
});

async function loadJson(url, description) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${description} 응답이 ${response.status}입니다.`);
  try {
    return await response.json();
  } catch {
    throw new Error(`${description}의 JSON 문법이 올바르지 않습니다.`);
  }
}

function assertSchema(data, description) {
  if (data.schemaVersion !== RESOURCE_SCHEMA_VERSION) {
    throw new Error(`${description} 스키마 ${data.schemaVersion}은 이 뷰어가 읽을 수 없습니다.`);
  }
}

function createVisibilityControl({ checked, text, codeText, data, onChange }) {
  const label = document.createElement('label');
  label.className = 'visibility-control';
  const input = document.createElement('input');
  input.type = 'checkbox';
  input.checked = checked;
  Object.assign(input.dataset, data);
  const description = document.createElement('span');
  description.textContent = text;
  const code = document.createElement('code');
  code.textContent = codeText;
  label.append(input, description, code);
  input.addEventListener('change', () => onChange(input.checked));
  return label;
}

function buildLevelControls(meta, mapView, mapMarkers) {
  const fragment = document.createDocumentFragment();
  for (const level of meta.levels) {
    fragment.append(createVisibilityControl({
      checked: level.defaultVisible,
      text: level.label,
      codeText: level.terrainGroupId ? `#${level.terrainGroupId}` : '단층',
      data: { levelId: level.id },
      onChange: (visible) => {
        mapView.setLevelVisibility(level.id, visible);
        mapMarkers.setLevelVisibility(level.id, visible);
      },
    }));
  }
  levelControls.replaceChildren(fragment);
}

function buildMarkerControls(markerData, mapMarkers) {
  const fragment = document.createDocumentFragment();
  for (const category of markerData.categories) {
    const count = markerData.markers.filter((marker) => marker.category === category.id).length;
    const control = createVisibilityControl({
      checked: category.defaultVisible !== false,
      text: category.label,
      codeText: String(count),
      data: { markerCategoryId: category.id },
      onChange: (visible) => mapMarkers.setCategoryVisibility(category.id, visible),
    });
    control.dataset.markerCategory = category.id;
    fragment.append(control);
  }
  markerControls.replaceChildren(fragment);
}

function buildFactionControls(markerData, mapMarkers) {
  const subtypeFactions = new Map(markerData.categories.flatMap((category) =>
    category.subtypes.map((subtype) => [`${category.id}\n${subtype.id}`, subtype.factions])
  ));
  const fragment = document.createDocumentFragment();

  for (const faction of markerData.factions) {
    const label = document.createElement('label');
    label.className = 'faction-control';
    label.dataset.markerFaction = faction.id;
    const input = document.createElement('input');
    input.type = 'radio';
    input.name = 'markerFaction';
    input.checked = faction.defaultSelected === true;
    input.dataset.markerFactionId = faction.id;
    const text = document.createElement('span');
    text.textContent = faction.label;
    const count = document.createElement('code');
    count.textContent = String(markerData.markers.filter((marker) =>
      subtypeFactions.get(`${marker.category}\n${marker.subtype}`)?.includes(faction.id)
    ).length);
    label.append(input, text, count);
    input.addEventListener('change', () => {
      if (input.checked) mapMarkers.setFaction(faction.id);
    });
    fragment.append(label);
  }

  factionControls.replaceChildren(fragment);
}

function formatCoordinate(value) {
  return value.toFixed(4);
}

async function initialize() {
  const manifest = await loadJson(new URL('manifest.json', resourceRoot), '리소스 manifest');
  assertSchema(manifest, '리소스 manifest');
  if (!Array.isArray(manifest.maps) || manifest.maps.length === 0) {
    throw new Error('리소스 manifest에 맵이 없습니다.');
  }

  const queryMap = new URLSearchParams(location.search).get('map');
  const mapId = queryMap || manifest.maps[0];
  if (!/^[a-z0-9-]+$/.test(mapId) || !manifest.maps.includes(mapId)) {
    throw new Error(`${mapId}: resources에 없는 맵입니다.`);
  }

  const mapRoot = new URL(`maps/${mapId}/`, resourceRoot);
  const meta = await loadJson(new URL('meta.json', mapRoot), `${mapId} meta.json`);
  assertSchema(meta, `${mapId} meta.json`);
  if (meta.mapId !== mapId) throw new Error('요청한 맵과 meta.json의 mapId가 다릅니다.');
  const markerData = await loadJson(new URL('markers.json', mapRoot), `${mapId} markers.json`);
  assertSchema(markerData, `${mapId} markers.json`);
  if (markerData.mapId !== mapId) {
    throw new Error('요청한 맵과 markers.json의 mapId가 다릅니다.');
  }
  if (!Array.isArray(markerData.markers)) {
    throw new Error('markers.json에 markers 배열이 없습니다.');
  }
  const knownLevels = new Set(meta.levels.map((level) => level.id));
  const unknownLevelMarker = markerData.markers.find(
    (marker) => !knownLevels.has(marker.levelId)
  );
  if (unknownLevelMarker) {
    throw new Error(`${unknownLevelMarker.id}: meta.json에 없는 마커 레벨입니다.`);
  }

  document.title = `${meta.title} / Tanuki Local Map`;
  mapTitle.textContent = meta.title;
  mapCode.textContent = mapId.toUpperCase();
  const mapView = await createMapView({
    viewport,
    meta,
    mapUrl: new URL('map.svg', mapRoot),
    onViewChange: ({ zoom }) => {
      zoomReadout.textContent = `${Math.round(zoom * 100).toString().padStart(3, '0')}%`;
    },
  });
  const initiallyVisibleLevels = meta.levels
    .filter((level) => level.defaultVisible)
    .map((level) => level.id);
  const mapMarkers = createMapMarkers(
    mapView.markerLayer,
    markerData,
    initiallyVisibleLevels
  );
  const positionMarker = createPositionMarker(mapView.markerLayer);

  buildLevelControls(meta, mapView, mapMarkers);
  buildMarkerControls(markerData, mapMarkers);
  buildFactionControls(markerData, mapMarkers);
  document.querySelector('#zoomIn').addEventListener('click', mapView.zoomIn);
  document.querySelector('#zoomOut').addEventListener('click', mapView.zoomOut);
  document.querySelector('#resetView').addEventListener('click', mapView.reset);

  showPositionOnMap = ({ x, y, z, look }) => {
    const mapPosition = gamePositionToMapPosition(x, y, meta.transform);
    const mapDirection = look === null
      ? null
      : gameDirectionToMapDirection(look, meta.transform);
    positionMarker.show(mapPosition, mapDirection);
    mapView.centerOn(mapPosition);
    coordinateResult.dataset.mapX = String(mapPosition.x);
    coordinateResult.dataset.mapY = String(mapPosition.y);
    coordinateResult.dataset.gameX = String(x);
    coordinateResult.dataset.gameY = String(y);
    coordinateResult.dataset.gameZ = String(z);
    if (mapDirection === null) {
      delete coordinateResult.dataset.gameDirection;
      delete coordinateResult.dataset.mapDirection;
    } else {
      coordinateResult.dataset.gameDirection = String(look);
      coordinateResult.dataset.mapDirection = String(mapDirection);
    }
    coordinateResult.textContent = `지도 좌표 X ${formatCoordinate(mapPosition.x)} / ` +
      `Y ${formatCoordinate(mapPosition.y)}` +
      (mapDirection === null ? '' : ` / 방향 ${formatCoordinate(mapDirection)}°`);
    document.documentElement.dataset.positionState = 'shown';
    return { mapPosition, gameDirection: look, mapDirection };
  };

  if (pendingPosition) {
    const position = pendingPosition;
    pendingPosition = null;
    showPositionOnMap(position);
  }

  positionForm.addEventListener('submit', (event) => {
    event.preventDefault();
    const formData = new FormData(positionForm);
    try {
      requestPosition({ x: formData.get('gameX'), y: formData.get('gameY') });
    } catch (error) {
      coordinateResult.textContent = error.message;
    }
  });

  loadStatus.hidden = true;
  document.documentElement.dataset.viewerState = 'ready';
  viewport.focus({ preventScroll: true });
}

initialize().catch((error) => {
  console.error(error);
  document.documentElement.dataset.viewerState = 'error';
  mapTitle.textContent = '지도를 열지 못했습니다';
  loadStatus.classList.add('is-error');
  loadStatus.textContent = `${error.message} 로컬 HTTP 서버에서 viewer/index.html을 열었는지 확인해 주세요.`;
});
