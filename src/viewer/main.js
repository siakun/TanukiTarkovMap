import { createCamera } from './camera.js';
import { gameDirectionToMapDirection, gamePositionToMapPosition, levelAtPosition, parseScreenshot } from './coords.js';
import { message, resolveLanguage } from './i18n.js';
import { createExtractionLayer, createPlayerMarker, createTeammateLayer } from './markers.js';

const RESOURCE_ROOT = new URL('../resources/', import.meta.url);

const REVEAL_INSET = 0.15;

const DEFAULT_TEAMMATE_COLOR = '#e53935';

const requestedRotation = Number(new URLSearchParams(location.search).get('rotation'));
const rotation = Number.isFinite(requestedRotation) && requestedRotation % 90 === 0
  ? ((requestedRotation % 360) + 360) % 360 : 0;

const elements = {
  container: document.getElementById('mapCont'),
  wrap: document.getElementById('mapWrap'),
  canvas: document.getElementById('markersCanvas'),
  marker: document.getElementById('playerMarker'),
  teammates: document.getElementById('teammates'),
  levelsPanel: document.getElementById('levelsPanel'),
  levelsList: document.getElementById('levelsList'),
  status: document.getElementById('loadStatus'),
};

const state = {
  status: 'loading', faction: 'pmc', pendingScreenshot: null, renderedScreenshot: null, map: null, teammates: [],
  language: resolveLanguage(new URLSearchParams(location.search).get('lang')), error: null,
};

class ViewerError extends Error {
  constructor(key, params = {}) {
    super(message('en', key, params));
    this.key = key;
    this.params = params;
  }
}

function renderStatus() {
  document.documentElement.lang = state.language;
  if (state.status === 'loading') {
    elements.status.textContent = message(state.language, 'loading');
  } else if (state.error) {
    const reason = state.error instanceof ViewerError
      ? message(state.language, state.error.key, state.error.params)
      : state.error.message;
    elements.status.textContent = message(state.language, 'failed', { reason });
  }
}

async function fetchResource(path, type) {
  const response = await fetch(new URL(path, RESOURCE_ROOT));
  if (!response.ok) throw new ViewerError('resourceUnreadable', { path, status: response.status });
  return type === 'json' ? response.json() : response.text();
}

function insertTerrain(svgText) {
  const parsed = new DOMParser().parseFromString(svgText, 'image/svg+xml');
  const root = parsed.documentElement;
  if (root.localName !== 'svg' || parsed.querySelector('parsererror')) throw new ViewerError('terrainInvalid');
  const svg = document.importNode(root, true);
  if (rotation) {
    for (const group of new Set([...svg.querySelectorAll('text')].map(text => text.parentElement))) {
      group.setAttribute('transform', `${group.getAttribute('transform') ?? ''} rotate(${-rotation})`);
    }
  }
  elements.wrap.replaceChildren(svg);
  return svg;
}

function createLevels({ meta, svg, onChange }) {
  const levels = [...meta.levels].sort((a, b) => b.sourceLevel - a.sourceLevel);
  const bounds = new Map();
  let selected = (levels.find((level) => level.defaultVisible) ?? levels[0]).id;

  function group(level) {
    return level.terrainGroupId ? svg.querySelector(`[id="${CSS.escape(level.terrainGroupId)}"]`) : null;
  }

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

  function step(up) {
    const index = levels.findIndex((level) => level.id === selected);
    const next = levels[Math.min(levels.length - 1, Math.max(0, index + (up ? -1 : 1)))];
    if (next.id !== selected) select(next.id);
  }

  const levelAt = (x, y, height) => levelAtPosition(levels, x, y, height)?.id ?? null;

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
  const level = levels.levelAt(position.x, position.y, position.z);
  if (level && level !== levels.selected) levels.select(level);
  const point = gamePositionToMapPosition(position.x, position.y, meta.transform);
  player.show(point, (gameDirectionToMapDirection(position.look, meta.transform) + rotation) % 360);

  const screen = camera.toScreen(point);
  const { width, height } = camera.viewSize();
  if (screen.x < width * REVEAL_INSET || screen.x > width * (1 - REVEAL_INSET)
    || screen.y < height * REVEAL_INSET || screen.y > height * (1 - REVEAL_INSET)) camera.centerOn(point);
  state.renderedScreenshot = filename;
  return player.isVisible();
}

function renderTeammates() {
  try {
    const { meta, levels, teammates } = state.map;
    teammates.set(state.teammates.flatMap((teammate) => {
      const { id, name, color, x, y, z, look } = teammate ?? {};
      if (typeof id !== 'string' || ![x, y, z, look].every(Number.isFinite)) return [];
      const level = levels.levelAt(x, y, z);
      return [{
        id, name: String(name ?? ''),
        color: /^#[0-9a-fA-F]{6}$/.test(color) ? color : DEFAULT_TEAMMATE_COLOR,
        position: gamePositionToMapPosition(x, y, meta.transform),
        direction: (gameDirectionToMapDirection(look, meta.transform) + rotation) % 360, otherLevel: level !== null && level !== levels.selected,
      }];
    }));
  } catch (error) {
    console.error(error);
  }
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
  setLanguage(code) {
    state.language = resolveLanguage(code);
    renderStatus();
    return true;
  },
  showTeammates(teammates) {
    state.teammates = Array.isArray(teammates) ? teammates : [];
    if (state.status === 'ready') renderTeammates();
    return true;
  },
});

renderStatus();

async function initialize() {
  const mapId = new URLSearchParams(location.search).get('map');
  const manifest = await fetchResource('manifest.json', 'json');
  if (!manifest.maps.includes(mapId)) throw new ViewerError('mapMissing', { map: mapId });
  const [meta, markerData, svgText] = await Promise.all([
    fetchResource(`maps/${mapId}/meta.json`, 'json'),
    fetchResource(`maps/${mapId}/markers.json`, 'json'),
    fetchResource(`maps/${mapId}/map.svg`, 'text'),
  ]);
  document.title = `${meta.title} - Tanuki Local Map`;
  const svg = insertTerrain(svgText);

  let camera = null, extractions = null, player = null, teammates = null;
  const levels = createLevels({
    meta, svg,
    onChange(levelId) { extractions?.setLevel(levelId); camera?.render(); if (state.map) renderTeammates(); },
  });
  camera = createCamera({
    container: elements.container,
    wrap: elements.wrap,
    minZoom: meta.minZoom,
    maxZoom: meta.maxZoom,
    rotation, mapSize: meta.size,
    terrainBounds: levels.terrainBounds,
    onChange() { extractions?.redraw(); player?.place(); teammates?.place(); },
    onAltWheel: levels.step,
  });
  extractions = createExtractionLayer({
    canvas: elements.canvas, markerData, baseZoom: meta.zoom, levelNumber: levels.number, camera,
  });
  player = createPlayerMarker({ element: elements.marker, camera });
  teammates = createTeammateLayer({ element: elements.teammates, camera });
  levels.select(levels.selected);
  extractions.setFaction(state.faction);
  camera.fit();

  state.map = { meta, levels, camera, extractions, player, teammates };
  state.status = 'ready';
  elements.status.hidden = true;
  if (state.pendingScreenshot) showPosition(state.pendingScreenshot);
  renderTeammates();
}

initialize().catch((error) => {
  console.error(error);
  state.status = `error: ${error.message}`;
  state.error = error;
  elements.status.classList.add('is-error');
  renderStatus();
});
