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
const defaultVisibleLevels = new Map();
for (const mapId of requestedMaps) {
  const markerPath = path.join(repositoryRoot, 'resources', 'maps', mapId, 'markers.json');
  const metaPath = path.join(repositoryRoot, 'resources', 'maps', mapId, 'meta.json');
  const markerResource = JSON.parse(await readFile(markerPath, 'utf8'));
  const meta = JSON.parse(await readFile(metaPath, 'utf8'));
  if (markerResource.schemaVersion !== 1 || markerResource.mapId !== mapId
    || !Array.isArray(markerResource.categories) || !Array.isArray(markerResource.markers)) {
    throw new Error(`${mapId}: markers.json의 스키마가 올바르지 않습니다.`);
  }
  markerResources.set(mapId, markerResource);
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
      return `${subtype.label} ${count}`;
    }).join(', ');
    return `${category.label} ${categoryMarkers.length} (${subtypes})`;
  }).join(' / ');
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
        return {
          viewBox: svg?.getAttribute('viewBox'),
          groups: svg?.querySelectorAll(':scope > g').length || 0,
          zoom: Number(viewport.dataset.zoom),
          matrix: getComputedStyle(stage).transform,
          center: { x: bounds.left + bounds.width / 2, y: bounds.top + bounds.height / 2 },
          basementDisplay: svg?.querySelector('#basement')?.style.display,
          markerCount: markers.length,
          visibleMarkerCount: markers.filter((marker) => !marker.hidden).length,
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
          emptyMarkerLabelCount: markers.filter((marker) => !marker.dataset.markerName).length,
          basementMarkerCount: markers.filter(
            (marker) => marker.dataset.markerLevel === 'basement'
          ).length,
          visibleBasementMarkerCount: markers.filter(
            (marker) => marker.dataset.markerLevel === 'basement' && !marker.hidden
          ).length,
        };
      })())`);
      const initialState = JSON.parse(initial);
      if (!initialState.viewBox || initialState.groups === 0) throw new Error('지형 SVG가 비어 있습니다.');
      if (initialState.basementDisplay !== 'none') throw new Error('지하 레벨의 초기 상태가 꺼짐이 아닙니다.');
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
      const expectedVisibleMarkers = markerResource.markers.filter((marker) => {
        const category = markerResource.categories.find((entry) => entry.id === marker.category);
        return category?.defaultVisible !== false
          && defaultVisibleLevels.get(mapId).has(marker.levelId);
      }).length;
      if (initialState.visibleMarkerCount !== expectedVisibleMarkers) {
        throw new Error(
          `초기 표시 마커 수량이 다릅니다: ` +
          `${initialState.visibleMarkerCount}/${expectedVisibleMarkers}`
        );
      }
      if (initialState.visibleBasementMarkerCount !== 0) {
        throw new Error('지하 레벨이 꺼졌는데 지하 마커가 보입니다.');
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

      const levelState = JSON.parse(await evaluate(cdp, `JSON.stringify((() => {
        const input = document.querySelector('[data-level-id="basement"]');
        input.click();
        const basementMarkers = [...document.querySelectorAll(
          '.map-marker[data-marker-level="basement"]'
        )];
        return {
          terrainVisible: document.querySelector('svg.map-svg #basement').style.display !== 'none',
          visibleMarkers: basementMarkers.filter((marker) => !marker.hidden).length,
        };
      })())`));
      if (!levelState.terrainVisible) throw new Error('레벨 토글이 지하 그룹을 켜지 못했습니다.');
      if (levelState.visibleMarkers !== initialState.basementMarkerCount) {
        throw new Error(
          `지하 마커가 모두 켜지지 않았습니다: ` +
          `${levelState.visibleMarkers}/${initialState.basementMarkerCount}`
        );
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
          maxXDifference: Math.max(...positions.map((position) => position.xDifference)),
          maxYDifference: Math.max(...positions.map((position) => position.yDifference)),
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

      await evaluate(cdp, `document.querySelector('#resetView').click()`);
      await delay(100);
      const hoverTarget = JSON.parse(await evaluate(cdp, `JSON.stringify((() => {
        const viewport = document.querySelector('#mapViewport').getBoundingClientRect();
        for (const marker of document.querySelectorAll('.map-marker:not([hidden])')) {
          const bounds = marker.getBoundingClientRect();
          const x = bounds.left + bounds.width / 2;
          const y = bounds.top + bounds.height / 2;
          if (x <= viewport.left || x >= viewport.right || y <= viewport.top || y >= viewport.bottom) {
            continue;
          }
          const topMarker = document.elementFromPoint(x, y)?.closest('.map-marker');
          if (topMarker) return { x, y };
        }
        return null;
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

      const coordinate = JSON.parse(await evaluate(cdp, `JSON.stringify((() => {
        document.querySelector('#gameX').value = '100';
        document.querySelector('#gameY').value = '200';
        document.querySelector('#positionForm').requestSubmit();
        const result = document.querySelector('#coordinateResult');
        const marker = document.querySelector('.position-marker');
        return {
          mapX: Number(result.dataset.mapX),
          mapY: Number(result.dataset.mapY),
          markerX: Number(marker.dataset.mapX),
          markerY: Number(marker.dataset.mapY),
          hidden: marker.hidden,
        };
      })())`));

      // 0ffa064b...의 shoreline 식에 게임 좌표 (100, 200)을 넣은 독립 기준값이다.
      if (mapId === 'shoreline'
        && (coordinate.mapX !== 1370 || coordinate.mapY !== 1550)) {
        throw new Error(`좌표 변환값이 다릅니다: X ${coordinate.mapX}, Y ${coordinate.mapY}`);
      }
      if (coordinate.hidden || coordinate.markerX !== coordinate.mapX
        || coordinate.markerY !== coordinate.mapY) {
        throw new Error('변환한 지도 좌표와 마커 위치가 다릅니다.');
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
        `좌표 ${coordinate.mapX}/${coordinate.mapY}\n` +
        `     마커 ${formatMarkerSummary(markerResource)}, ` +
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
