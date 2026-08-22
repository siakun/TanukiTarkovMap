#!/usr/bin/env node
/**
 * verify-resources.mjs - 새 리소스와 뷰어가 실제 브라우저에서 함께 동작하는지 검사한다
 *
 * 파일 존재 여부만 보면 SVG 파싱 오류, 모듈 로드 실패, 포인터 조작 오류, 마커 누락을 놓친다.
 * 이 도구는 loopback 정적 서버와 별도 headless Chrome을 띄우고 완성 화면의 마커 수량, 종류와
 * 레벨 필터, 이름 표시까지 직접 조작해 확인한다. 앱은 실행하거나 연결하지 않으며 네트워크로
 * 나가는 자원도 없다.
 *
 * 사용법:
 *   node tools/verify-resources.mjs
 *   node tools/verify-resources.mjs --maps shoreline
 *   node tools/verify-resources.mjs --screenshot D:\shoreline.png
 *
 * 디버깅 포트는 9231을 쓴다. 9230은 리소스 추출 자리다. 포트가 이미 사용 중이면 다른
 * 프로세스에 붙지 않고 즉시 실패한다. VERIFY_RESOURCES_PORT로 9230 이후 포트를 고를 수 있다.
 */
import { spawn } from 'node:child_process';
import { createServer as createHttpServer } from 'node:http';
import { createServer as createNetServer } from 'node:net';
import { mkdtemp, readFile, rm, stat, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {
  gameDirectionToMapDirection,
  gamePositionToMapPosition,
  parseScreenshotPosition,
} from '../viewer/coords.js';

const CDP_PORT = Number(process.env.VERIFY_RESOURCES_PORT || 9231);
if (!Number.isInteger(CDP_PORT) || CDP_PORT < 9230 || CDP_PORT > 65_535) {
  throw new Error('VERIFY_RESOURCES_PORT는 9230~65535 사이 정수여야 합니다.');
}

const args = process.argv.slice(2);
const argValue = (name, fallback) => {
  const index = args.indexOf(name);
  if (index < 0) return fallback;
  if (!args[index + 1] || args[index + 1].startsWith('--')) {
    throw new Error(`${name} 뒤에 값을 지정해야 합니다.`);
  }
  return args[index + 1];
};

const repositoryRoot = path.resolve(argValue('--root', process.cwd()));
const screenshotPath = argValue('--screenshot', '').trim();
const manifestPath = path.join(repositoryRoot, 'resources', 'manifest.json');
const manifest = JSON.parse(await readFile(manifestPath, 'utf8'));
const mapConfigurationSource = await readFile(path.join(
  repositoryRoot,
  'src',
  'TanukiTarkovMap',
  'Models',
  'Data',
  'MapConfiguration.cs'
), 'utf8');
const configuredMaps = [...mapConfigurationSource.matchAll(/new MapInfo\("([a-z0-9-]+)"/g)]
  .map((match) => match[1]);
if (configuredMaps.length === 0 || new Set(configuredMaps).size !== configuredMaps.length) {
  throw new Error('MapConfiguration.cs에서 고유한 맵 ID 목록을 읽지 못했습니다.');
}
if (manifest.schemaVersion !== 1 || !Array.isArray(manifest.maps)
  || JSON.stringify(manifest.maps) !== JSON.stringify(configuredMaps)) {
  throw new Error('resource manifest의 맵 목록이 MapConfiguration.cs와 다릅니다.');
}
const requestedMaps = argValue('--maps', '').trim()
  ? argValue('--maps', '').split(',').map((mapId) => mapId.trim()).filter(Boolean)
  : manifest.maps;

if (!Array.isArray(requestedMaps) || requestedMaps.length === 0) {
  throw new Error('검사할 맵이 없습니다.');
}

for (const mapId of requestedMaps) {
  if (!manifest.maps.includes(mapId)) throw new Error(`${mapId}: manifest에 없는 맵입니다.`);
}

const markerResources = new Map();
const metas = new Map();
const defaultVisibleLevels = new Map();
for (const mapId of requestedMaps) {
  const markerPath = path.join(repositoryRoot, 'resources', 'maps', mapId, 'markers.json');
  const metaPath = path.join(repositoryRoot, 'resources', 'maps', mapId, 'meta.json');
  const markerResource = JSON.parse(await readFile(markerPath, 'utf8'));
  const meta = JSON.parse(await readFile(metaPath, 'utf8'));
  if (meta.schemaVersion !== 1 || meta.mapId !== mapId || !Array.isArray(meta.levels)
    || meta.levels.length === 0) {
    throw new Error(`${mapId}: meta.json의 스키마가 올바르지 않습니다.`);
  }
  if (markerResource.schemaVersion !== 1 || markerResource.mapId !== mapId
    || !Array.isArray(markerResource.factions) || !Array.isArray(markerResource.categories)
    || !Array.isArray(markerResource.markers)) {
    throw new Error(`${mapId}: markers.json의 스키마가 올바르지 않습니다.`);
  }
  if (markerResource.categories.length !== 1
    || markerResource.categories[0].id !== 'extraction'
    || markerResource.markers.some((marker) => marker.category !== 'extraction')) {
    throw new Error(`${mapId}: markers.json에는 추출구만 있어야 합니다.`);
  }
  if (markerResource.factions.filter((faction) => faction.defaultSelected).length !== 1) {
    throw new Error(`${mapId}: 기본 진영을 하나로 특정하지 못했습니다.`);
  }

  for (const category of markerResource.categories) {
    for (const subtype of category.subtypes) {
      const resourceCount = markerResource.markers.filter(
        (marker) => marker.category === category.id && marker.subtype === subtype.id
      ).length;
      const listedCount = markerResource.source?.listedCounts?.[category.id]?.[subtype.id];
      if (listedCount !== resourceCount) {
        throw new Error(
          `${mapId}: 사이트 좌측 목록의 ${subtype.label} 수량 ${listedCount}과 ` +
          `리소스 수량 ${resourceCount}이 다릅니다.`
        );
      }
    }
  }

  markerResources.set(mapId, markerResource);
  metas.set(mapId, meta);
  defaultVisibleLevels.set(
    mapId,
    new Set(meta.levels.filter((level) => level.defaultVisible).map((level) => level.id))
  );
}

const CHROME_CANDIDATES = [
  'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
  'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
  process.env.CHROME_PATH,
].filter(Boolean);
const chromePath = CHROME_CANDIDATES.find((candidate) => existsSync(candidate));
if (!chromePath) {
  console.error('Chrome을 찾지 못했습니다. CHROME_PATH 환경변수로 경로를 지정하세요.');
  process.exit(1);
}

const MIME_TYPES = {
  '.css': 'text/css; charset=utf-8',
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml; charset=utf-8',
};

function delay(milliseconds) {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

function portAvailable(port) {
  return new Promise((resolve) => {
    const probe = createNetServer();
    probe.once('error', () => resolve(false));
    probe.listen(port, '127.0.0.1', () => probe.close(() => resolve(true)));
  });
}

if (!await portAvailable(CDP_PORT)) {
  throw new Error(`CDP 포트 ${CDP_PORT}을 이미 다른 프로세스가 사용 중입니다.`);
}

const server = createHttpServer(async (request, response) => {
  try {
    const requestUrl = new URL(request.url, 'http://127.0.0.1');
    const relativePath = decodeURIComponent(requestUrl.pathname).replace(/^[/\\]+/, '');
    const filePath = path.resolve(repositoryRoot, relativePath || 'viewer/index.html');
    const rootPrefix = `${repositoryRoot}${path.sep}`;
    if (filePath !== repositoryRoot && !filePath.startsWith(rootPrefix)) {
      response.writeHead(403).end('Forbidden');
      return;
    }

    const fileStat = await stat(filePath);
    if (!fileStat.isFile()) throw new Error('Not a file');
    const body = await readFile(filePath);
    response.writeHead(200, {
      'content-type': MIME_TYPES[path.extname(filePath).toLowerCase()] || 'application/octet-stream',
      'cache-control': 'no-store',
    });
    response.end(body);
  } catch {
    response.writeHead(404).end('Not found');
  }
});

await new Promise((resolve, reject) => {
  server.once('error', reject);
  server.listen(0, '127.0.0.1', resolve);
});
const serverPort = server.address().port;

const profilePath = await mkdtemp(path.join(os.tmpdir(), 'tanuki-resource-verify-'));
const chrome = spawn(chromePath, [
  '--headless=new',
  '--disable-gpu',
  '--no-first-run',
  '--disable-default-apps',
  '--remote-debugging-address=127.0.0.1',
  `--remote-debugging-port=${CDP_PORT}`,
  `--user-data-dir=${profilePath}`,
  '--window-size=1440,1000',
  'about:blank',
], { stdio: 'ignore' });
const chromeExited = new Promise((resolve) => chrome.once('exit', resolve));

process.on('exit', () => chrome.kill());

async function connect() {
  for (let attempt = 0; attempt < 40; attempt++) {
    try {
      const response = await fetch(`http://127.0.0.1:${CDP_PORT}/json/list`);
      const targets = await response.json();
      const page = targets.find((target) => target.type === 'page');
      if (page) return page;
    } catch {}
    await delay(250);
  }
  throw new Error(`Chrome CDP 포트 ${CDP_PORT}에 연결하지 못했습니다.`);
}

function createSession(page) {
  const socket = new WebSocket(page.webSocketDebuggerUrl);
  const pending = new Map();
  let nextId = 1;

  socket.addEventListener('message', (message) => {
    const data = JSON.parse(message.data);
    if (!data.id || !pending.has(data.id)) return;
    const { resolve, reject } = pending.get(data.id);
    pending.delete(data.id);
    data.error ? reject(new Error(data.error.message)) : resolve(data.result);
  });

  return {
    ready: new Promise((resolve) => socket.addEventListener('open', resolve)),
    send(method, params = {}) {
      return new Promise((resolve, reject) => {
        const id = nextId++;
        pending.set(id, { resolve, reject });
        socket.send(JSON.stringify({ id, method, params }));
      });
    },
    close: () => socket.close(),
  };
}

async function evaluate(cdp, expression) {
  const result = await cdp.send('Runtime.evaluate', {
    expression,
    returnByValue: true,
    awaitPromise: true,
  });
  if (result.exceptionDetails) {
    throw new Error(result.exceptionDetails.exception?.description || '페이지 평가가 실패했습니다.');
  }
  return result.result.value;
}

async function waitUntil(cdp, expression, timeout = 10_000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    if (await evaluate(cdp, expression)) return;
    await delay(100);
  }
  throw new Error(`화면 준비를 ${timeout}ms 안에 확인하지 못했습니다.`);
}

function matrixState(matrixText) {
  const values = matrixText === 'none'
    ? [1, 0, 0, 1, 0, 0]
    : matrixText.match(/matrix\(([^)]+)\)/)?.[1].split(',').map(Number);
  if (!values || values.length !== 6) throw new Error(`알 수 없는 transform입니다: ${matrixText}`);
  return { scale: values[0], x: values[4], y: values[5] };
}

function formatMarkerSummary(markerResource) {
  return markerResource.categories.map((category) => {
    const categoryMarkers = markerResource.markers.filter(
      (marker) => marker.category === category.id
    );
    const subtypes = category.subtypes.map((subtype) => {
      const count = categoryMarkers.filter((marker) => marker.subtype === subtype.id).length;
      const listedCount = markerResource.source.listedCounts[category.id][subtype.id];
      return `${subtype.label} ${count}/${listedCount}`;
    }).join(', ');
    return `${category.label} ${categoryMarkers.length} (리소스/사이트 ${subtypes})`;
  }).join(' / ');
}

function factionsForMarker(markerResource, marker) {
  const category = markerResource.categories.find((entry) => entry.id === marker.category);
  return category?.subtypes.find((entry) => entry.id === marker.subtype)?.factions || [];
}

function sameStringSet(actual, expected) {
  return JSON.stringify([...actual].sort()) === JSON.stringify([...expected].sort());
}

let cdp = null;
let failed = 0;

try {
  const page = await connect();
  cdp = createSession(page);
  await cdp.ready;
  await cdp.send('Page.enable');

  for (const mapId of requestedMaps) {
    try {
      const markerResource = markerResources.get(mapId);
      const meta = metas.get(mapId);
      const defaultFaction = markerResource.factions.find((faction) => faction.defaultSelected).id;
      const terrainGroupByLevel = Object.fromEntries(
        meta.levels.map((level) => [level.id, level.terrainGroupId])
      );
      const url = `http://127.0.0.1:${serverPort}/viewer/index.html?map=${encodeURIComponent(mapId)}`;
      await cdp.send('Page.navigate', { url });
      await waitUntil(
        cdp,
        `['ready', 'error'].includes(document.documentElement.dataset.viewerState)`
      );

      const viewerState = await evaluate(cdp, `document.documentElement.dataset.viewerState`);
      if (viewerState !== 'ready') {
        const message = await evaluate(cdp, `document.querySelector('#loadStatus').textContent`);
        throw new Error(message);
      }

      const initial = await evaluate(cdp, `JSON.stringify((() => {
        const viewport = document.querySelector('#mapViewport');
        const stage = document.querySelector('.map-stage');
        const svg = document.querySelector('svg.map-svg');
        const markers = [...document.querySelectorAll('.map-marker')];
        const bounds = viewport.getBoundingClientRect();
        const terrainGroupByLevel = ${JSON.stringify(terrainGroupByLevel)};
        return {
          viewBox: svg?.getAttribute('viewBox'),
          groups: svg?.querySelectorAll(':scope > g').length || 0,
          zoom: Number(viewport.dataset.zoom),
          matrix: getComputedStyle(stage).transform,
          center: { x: bounds.left + bounds.width / 2, y: bounds.top + bounds.height / 2 },
          levelDisplays: Object.fromEntries(Object.entries(terrainGroupByLevel)
            .filter(([, groupId]) => groupId !== null)
            .map(([levelId, groupId]) => [
              levelId,
              svg?.querySelector('[id="' + CSS.escape(groupId) + '"]')?.style.display,
            ])),
          markerCount: markers.length,
          visibleMarkerCount: markers.filter((marker) => !marker.hidden).length,
          markerRecords: markers.map((marker) => ({
            id: marker.dataset.markerId,
            category: marker.dataset.markerCategory,
            levelId: marker.dataset.markerLevel,
            factions: marker.dataset.markerFactions,
          })),
          markerCategoryCounts: Object.fromEntries(
            [...new Set(markers.map((marker) => marker.dataset.markerCategory))]
              .map((category) => [
                category,
                markers.filter((marker) => marker.dataset.markerCategory === category).length,
              ])
          ),
          markerControlCounts: Object.fromEntries(
            [...document.querySelectorAll('[data-marker-category-id]')].map((control) => [
              control.dataset.markerCategoryId,
              Number(control.closest('.visibility-control')?.querySelector('code')?.textContent),
            ])
          ),
          factionControlCounts: Object.fromEntries(
            [...document.querySelectorAll('[data-marker-faction-id]')].map((control) => [
              control.dataset.markerFactionId,
              Number(control.closest('.faction-control')?.querySelector('code')?.textContent),
            ])
          ),
          selectedFaction: document.querySelector('[data-marker-faction-id]:checked')
            ?.dataset.markerFactionId,
          emptyMarkerLabelCount: markers.filter((marker) => !marker.dataset.markerName).length,
        };
      })())`);
      const initialState = JSON.parse(initial);
      const expectedViewBox = `0 0 ${meta.size.width} ${meta.size.height}`;
      if (initialState.viewBox !== expectedViewBox || initialState.groups === 0) {
        throw new Error(`지형 SVG가 비었거나 크기가 다릅니다: ${initialState.viewBox}`);
      }
      for (const level of meta.levels) {
        if (level.terrainGroupId === null) continue;
        const displayed = initialState.levelDisplays[level.id] !== 'none';
        if (displayed !== level.defaultVisible) {
          throw new Error(`${level.id}: 지형 레벨의 초기 표시 상태가 다릅니다.`);
        }
      }
      if (initialState.markerCount !== markerResource.markers.length) {
        throw new Error(
          `마커 DOM 수량이 다릅니다: ${initialState.markerCount}/${markerResource.markers.length}`
        );
      }
      for (const category of markerResource.categories) {
        const expectedCount = markerResource.markers.filter(
          (marker) => marker.category === category.id
        ).length;
        if (initialState.markerCategoryCounts[category.id] !== expectedCount) {
          throw new Error(
            `${category.label} 마커 수량이 다릅니다: ` +
            `${initialState.markerCategoryCounts[category.id]}/${expectedCount}`
          );
        }
        if (initialState.markerControlCounts[category.id] !== expectedCount) {
          throw new Error(
            `${category.label} 필터 수량이 다릅니다: ` +
            `${initialState.markerControlCounts[category.id]}/${expectedCount}`
          );
        }
      }
      if (initialState.emptyMarkerLabelCount !== 0) {
        throw new Error('이름 또는 세부 종류 이름이 없는 마커가 있습니다.');
      }
      if (initialState.selectedFaction !== defaultFaction) {
        throw new Error(`초기 진영이 ${defaultFaction}이 아닙니다.`);
      }
      for (const faction of markerResource.factions) {
        const expectedCount = markerResource.markers.filter(
          (marker) => factionsForMarker(markerResource, marker).includes(faction.id)
        ).length;
        if (initialState.factionControlCounts[faction.id] !== expectedCount) {
          throw new Error(
            `${faction.label} 진영 필터 수량이 다릅니다: ` +
            `${initialState.factionControlCounts[faction.id]}/${expectedCount}`
          );
        }
      }
      for (const marker of markerResource.markers) {
        const rendered = initialState.markerRecords.find((entry) => entry.id === marker.id);
        const expectedFactions = factionsForMarker(markerResource, marker).join(' ');
        if (!rendered || rendered.category !== marker.category || rendered.levelId !== marker.levelId
          || rendered.factions !== expectedFactions) {
          throw new Error(`${marker.id}: DOM의 종류, 레벨 또는 진영이 리소스와 다릅니다.`);
        }
      }
      const expectedVisibleMarkers = markerResource.markers.filter((marker) => {
        const category = markerResource.categories.find((entry) => entry.id === marker.category);
        return category?.defaultVisible !== false
          && defaultVisibleLevels.get(mapId).has(marker.levelId)
          && factionsForMarker(markerResource, marker).includes(defaultFaction);
      }).length;
      if (initialState.visibleMarkerCount !== expectedVisibleMarkers) {
        throw new Error(
          `초기 표시 마커 수량이 다릅니다: ` +
          `${initialState.visibleMarkerCount}/${expectedVisibleMarkers}`
        );
      }

      await cdp.send('Input.dispatchMouseEvent', {
        type: 'mouseMoved',
        x: initialState.center.x,
        y: initialState.center.y,
      });
      await cdp.send('Input.dispatchMouseEvent', {
        type: 'mouseWheel',
        x: initialState.center.x,
        y: initialState.center.y,
        deltaX: 0,
        deltaY: -120,
      });
      await delay(100);
      const wheelZoom = await evaluate(cdp, `Number(document.querySelector('#mapViewport').dataset.zoom)`);
      if (!(wheelZoom > initialState.zoom)) throw new Error('휠 확대가 배율을 높이지 않았습니다.');

      const beforeDrag = matrixState(await evaluate(
        cdp,
        `getComputedStyle(document.querySelector('.map-stage')).transform`
      ));
      await cdp.send('Input.dispatchMouseEvent', {
        type: 'mousePressed',
        x: initialState.center.x,
        y: initialState.center.y,
        button: 'left',
        buttons: 1,
        clickCount: 1,
      });
      await cdp.send('Input.dispatchMouseEvent', {
        type: 'mouseMoved',
        x: initialState.center.x + 80,
        y: initialState.center.y + 45,
        button: 'left',
        buttons: 1,
      });
      await cdp.send('Input.dispatchMouseEvent', {
        type: 'mouseReleased',
        x: initialState.center.x + 80,
        y: initialState.center.y + 45,
        button: 'left',
        buttons: 0,
        clickCount: 1,
      });
      const afterDrag = matrixState(await evaluate(
        cdp,
        `getComputedStyle(document.querySelector('.map-stage')).transform`
      ));
      const dragX = Math.round(afterDrag.x - beforeDrag.x);
      const dragY = Math.round(afterDrag.y - beforeDrag.y);
      if (dragX !== 80 || dragY !== 45) {
        throw new Error(`끌기 이동값이 다릅니다: X ${dragX}, Y ${dragY}`);
      }

      const levelFilters = JSON.parse(await evaluate(cdp, `JSON.stringify((() => {
        const allMarkers = [...document.querySelectorAll('.map-marker')];
        const terrainGroupByLevel = ${JSON.stringify(terrainGroupByLevel)};
        const defaultFaction = ${JSON.stringify(defaultFaction)};
        return [...document.querySelectorAll('[data-level-id]')].map((input) => {
          const levelId = input.dataset.levelId;
          const selected = allMarkers.filter((marker) => marker.dataset.markerLevel === levelId);
          const other = allMarkers.filter((marker) => marker.dataset.markerLevel !== levelId);
          const selectedBefore = selected.map((marker) => marker.hidden);
          const otherBefore = other.map((marker) => marker.hidden);
          input.click();
          const selectedCorrect = selected.every((marker) => {
            const inFaction = marker.dataset.markerFactions.split(' ').includes(defaultFaction);
            return marker.hidden === (!input.checked || !inFaction);
          });
          const otherUnchanged = other.every(
            (marker, index) => marker.hidden === otherBefore[index]
          );
          const terrainGroupId = terrainGroupByLevel[levelId];
          const terrainCorrect = terrainGroupId === null
            || (document.querySelector('svg.map-svg [id="' + CSS.escape(terrainGroupId) + '"]')
              .style.display !== 'none') === input.checked;
          input.click();
          const selectedRestored = selected.every(
            (marker, index) => marker.hidden === selectedBefore[index]
          );
          return { levelId, selectedCorrect, otherUnchanged, terrainCorrect, selectedRestored };
        });
      })())`));
      if (levelFilters.length !== meta.levels.length
        || levelFilters.some((result) => !result.selectedCorrect || !result.otherUnchanged
          || !result.terrainCorrect || !result.selectedRestored)) {
        throw new Error('레벨 필터가 해당 지형과 마커만 바꾸고 복원하지 못했습니다.');
      }

      const factionFilters = JSON.parse(await evaluate(cdp, `JSON.stringify((() => {
        const markers = [...document.querySelectorAll('.map-marker')];
        const results = [...document.querySelectorAll('[data-marker-faction-id]')].map((input) => {
          input.click();
          return {
            factionId: input.dataset.markerFactionId,
            checked: input.checked,
            visibleIds: markers.filter((marker) => !marker.hidden)
              .map((marker) => marker.dataset.markerId),
          };
        });
        document.querySelector(
          '[data-marker-faction-id=${JSON.stringify(defaultFaction)}]'
        ).click();
        return results;
      })())`));
      if (factionFilters.length !== markerResource.factions.length) {
        throw new Error('진영 필터 수량이 markers.json과 다릅니다.');
      }
      for (const result of factionFilters) {
        const expectedIds = markerResource.markers.filter((marker) =>
          defaultVisibleLevels.get(mapId).has(marker.levelId)
          && factionsForMarker(markerResource, marker).includes(result.factionId)
        ).map((marker) => marker.id);
        if (!result.checked || !sameStringSet(result.visibleIds, expectedIds)) {
          throw new Error(`${result.factionId}: 진영 필터가 다른 추출구를 표시했습니다.`);
        }
      }

      const categoryFilter = JSON.parse(await evaluate(cdp, `JSON.stringify((() => {
        const allMarkers = [...document.querySelectorAll('.map-marker')];
        return [...document.querySelectorAll('[data-marker-category-id]')].map((input) => {
          const categoryId = input.dataset.markerCategoryId;
          const selected = allMarkers.filter(
            (marker) => marker.dataset.markerCategory === categoryId
          );
          const other = allMarkers.filter(
            (marker) => marker.dataset.markerCategory !== categoryId
          );
          const selectedBefore = selected.map((marker) => marker.hidden);
          const otherBefore = other.map((marker) => marker.hidden);
          input.click();
          const selectedHidden = selected.every((marker) => marker.hidden);
          const otherUnchanged = other.every(
            (marker, index) => marker.hidden === otherBefore[index]
          );
          input.click();
          const selectedRestored = selected.every(
            (marker, index) => marker.hidden === selectedBefore[index]
          );
          return { categoryId, selectedHidden, otherUnchanged, selectedRestored };
        });
      })())`));
      if (categoryFilter.length !== markerResource.categories.length
        || categoryFilter.some((result) => !result.selectedHidden
          || !result.otherUnchanged || !result.selectedRestored)) {
        throw new Error('종류 필터가 선택한 마커만 숨기고 복원하지 못했습니다.');
      }

      const markerPositions = JSON.parse(await evaluate(cdp, `JSON.stringify((() => {
        const markers = [...document.querySelectorAll('.map-marker')];
        const positions = markers.map((marker) => ({
          xDifference: Math.abs(Number.parseFloat(marker.style.left) - Number(marker.dataset.mapX)),
          yDifference: Math.abs(Number.parseFloat(marker.style.top) - Number(marker.dataset.mapY)),
        }));
        return {
          maxXDifference: Math.max(0, ...positions.map((position) => position.xDifference)),
          maxYDifference: Math.max(0, ...positions.map((position) => position.yDifference)),
        };
      })())`));
      // Chromium은 left/top을 내부 레이아웃 단위로 양자화하므로 4자리 좌표가 최대 0.01px 안에서
      // 반올림된다. 이 합격선은 좌표가 다른 마커를 통과시키지 않으면서 렌더러 반올림만 허용한다.
      if (markerPositions.maxXDifference > 0.01 || markerPositions.maxYDifference > 0.01) {
        throw new Error(
          `markers.json과 DOM 위치가 다릅니다: ` +
          `X ${markerPositions.maxXDifference}, Y ${markerPositions.maxYDifference}`
        );
      }

      const hoverTarget = JSON.parse(await evaluate(cdp, `JSON.stringify((() => {
        const markers = [...document.querySelectorAll('.map-marker')];
        const defaultFaction = ${JSON.stringify(defaultFaction)};
        const marker = markers.find((entry) =>
          entry.dataset.markerFactions.split(' ').includes(defaultFaction)
        ) || markers[0];
        if (!marker) return null;
        const factionId = marker.dataset.markerFactions.split(' ')[0];
        document.querySelector('[data-marker-faction-id="' + factionId + '"]').click();
        const levelInput = document.querySelector(
          '[data-level-id="' + CSS.escape(marker.dataset.markerLevel) + '"]'
        );
        if (levelInput && !levelInput.checked) levelInput.click();
        const viewport = document.querySelector('#mapViewport');
        const stage = document.querySelector('.map-stage');
        const zoom = Number(viewport.dataset.zoom);
        const mapX = Number(marker.dataset.mapX);
        const mapY = Number(marker.dataset.mapY);
        stage.style.transform = 'translate3d(' +
          (viewport.clientWidth / 2 - mapX * zoom) + 'px, ' +
          (viewport.clientHeight / 2 - mapY * zoom) + 'px, 0) scale(' + zoom + ')';
        const bounds = marker.getBoundingClientRect();
        return {
          x: bounds.left + bounds.width / 2,
          y: bounds.top + bounds.height / 2,
          markerId: marker.dataset.markerId,
        };
      })())`));
      if (!hoverTarget) throw new Error('이름 표시를 검사할 화면 안 마커를 찾지 못했습니다.');
      await cdp.send('Input.dispatchMouseEvent', {
        type: 'mouseMoved',
        x: hoverTarget.x,
        y: hoverTarget.y,
      });
      await delay(150);
      const hoverState = JSON.parse(await evaluate(cdp, `JSON.stringify((() => {
        const marker = document.querySelector('.map-marker:hover');
        const label = marker?.querySelector('.map-marker__label');
        const style = label ? getComputedStyle(label) : null;
        return {
          name: marker?.dataset.markerName,
          text: label?.textContent,
          opacity: style ? Number(style.opacity) : 0,
          visibility: style?.visibility,
        };
      })())`));
      if (!hoverState.name || hoverState.text !== hoverState.name
        || hoverState.opacity < 0.9 || hoverState.visibility !== 'visible') {
        throw new Error('마커에 올렸을 때 이름이 표시되지 않습니다.');
      }

      const expectedMapPosition = gamePositionToMapPosition(100, 200, meta.transform);
      const expectedMapDirection = gameDirectionToMapDirection(90, meta.transform);
      const position = JSON.parse(await evaluate(cdp, `JSON.stringify((() => {
        const result = window.tanukiViewer.showPosition({ x: 100, y: 200, z: 0, look: 90 });
        const marker = document.querySelector('.position-marker');
        const direction = marker.querySelector('.position-marker__direction');
        return {
          ready: window.tanukiViewer.ready,
          result,
          markerX: Number(marker.dataset.mapX),
          markerY: Number(marker.dataset.mapY),
          markerDirection: Number(marker.dataset.mapDirection),
          cssDirection: Number.parseFloat(marker.style.getPropertyValue('--direction-angle')),
          directionHidden: direction.hidden,
          hidden: marker.hidden,
        };
      })())`));
      if (!position.ready || position.hidden || position.directionHidden
        || position.markerX !== expectedMapPosition.x
        || position.markerY !== expectedMapPosition.y
        || position.result.mapPosition.x !== expectedMapPosition.x
        || position.result.mapPosition.y !== expectedMapPosition.y
        || Math.abs(position.markerDirection - expectedMapDirection) > 1e-10
        || Math.abs(position.cssDirection - expectedMapDirection) > 1e-10) {
        throw new Error('외부 위치 진입점이 좌표와 방향을 같은 마커에 표시하지 못했습니다.');
      }

      const screenshotFilename = '2026-08-22[21-49]_-147.10, 5.52, -386.99_' +
        '-0.05008, -0.56703, 0.03485, -0.82144_15.61 (0).png';
      const screenshotPosition = parseScreenshotPosition(screenshotFilename);
      const expectedScreenshotMapPosition = gamePositionToMapPosition(
        screenshotPosition.x,
        screenshotPosition.y,
        meta.transform
      );
      const expectedScreenshotDirection = gameDirectionToMapDirection(
        screenshotPosition.look,
        meta.transform
      );
      const screenshotResult = JSON.parse(await evaluate(cdp, `JSON.stringify((() => {
        const result = window.tanukiViewer.showPositionFromScreenshot(
          ${JSON.stringify(screenshotFilename)}
        );
        const marker = document.querySelector('.position-marker');
        return {
          result,
          markerX: Number(marker.dataset.mapX),
          markerY: Number(marker.dataset.mapY),
          markerDirection: Number(marker.dataset.mapDirection),
        };
      })())`));
      if (screenshotResult.markerX !== expectedScreenshotMapPosition.x
        || screenshotResult.markerY !== expectedScreenshotMapPosition.y
        || screenshotResult.result.mapPosition.x !== expectedScreenshotMapPosition.x
        || screenshotResult.result.mapPosition.y !== expectedScreenshotMapPosition.y
        || Math.abs(screenshotResult.markerDirection - expectedScreenshotDirection) > 1e-10) {
        throw new Error('스크린샷 파일명 진입점이 좌표와 방향을 올바르게 해석하지 못했습니다.');
      }

      if (screenshotPath) {
        const capture = await cdp.send('Page.captureScreenshot', { format: 'png', fromSurface: true });
        const outputPath = path.resolve(screenshotPath);
        await writeFile(outputPath, Buffer.from(capture.data, 'base64'));
        console.log(`화면 캡처: ${outputPath}`);
      }

      console.log(
        `OK   ${mapId.padEnd(12)} SVG ${initialState.viewBox}, 그림 ${initialState.groups}겹, ` +
        `휠 ${initialState.zoom}->${wheelZoom}, 끌기 ${dragX}/${dragY}, ` +
        `좌표 ${position.markerX}/${position.markerY}, 방향 ${position.markerDirection}°\n` +
        `     마커 ${formatMarkerSummary(markerResource)}, ` +
        `진영 ${factionFilters.map((entry) => `${entry.factionId} ${entry.visibleIds.length}`).join(', ')}, ` +
        `위치 최대 오차 ${markerPositions.maxXDifference}/${markerPositions.maxYDifference}px, ` +
        `hover ${hoverState.name}`
      );
    } catch (error) {
      failed++;
      console.error(`실패 ${mapId}: ${error.message}`);
    }
  }
} finally {
  cdp?.close();
  if (chrome.exitCode === null) {
    chrome.kill();
    await Promise.race([chromeExited, delay(3_000)]);
  }
  await new Promise((resolve) => server.close(resolve));
  await rm(profilePath, { recursive: true, force: true }).catch(() => {});
}

if (failed > 0) {
  console.error(`맵 ${failed}개가 리소스 검사를 통과하지 못했습니다.`);
  process.exitCode = 1;
} else {
  console.log(`맵 ${requestedMaps.length}개가 리소스 검사를 통과했습니다.`);
}
