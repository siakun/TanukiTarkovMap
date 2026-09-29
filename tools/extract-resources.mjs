#!/usr/bin/env node
/**
 * extract-resources.mjs - 저장된 사이트 사본에서 읽을 수 있는 맵 리소스를 추출한다
 *
 * 목적: archive의 최소화된 사이트 번들을 사람이 고칠 수 있는 map.svg, meta.json,
 * markers.json으로 바꾼다. 실측값을 손으로 옮기지 않고 원본 blob과 함께 기록해야 사이트가
 * 바뀌었을 때 차이를 추적할 수 있다.
 *
 * 대상 맵은 MapConfiguration.cs에서 읽는다. 맵 목록을 도구에 다시 적지 않아 앱에 맵이
 * 늘거나 빠졌는데 리소스만 조용히 뒤처지는 일을 막는다.
 *
 * 사용법:
 *   node tools/extract-resources.mjs
 *   node tools/extract-resources.mjs --maps shoreline
 *   node tools/extract-resources.mjs --archive D:\archive --out D:\resources
 *
 * 입력은 archive-maps.mjs가 만든 색인과 blob뿐이다. 지형과 설정은 blob에서 직접 읽고, 마커는
 * 별도 headless Chrome에 사본만 응답해 페이지가 복원한 Nuxt 상태에서 읽는다. 외부 네트워크와
 * 실행 중인 앱은 사용하지 않는다.
 */
import { spawn } from 'node:child_process';
import { mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { createServer as createNetServer } from 'node:net';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';

const toolDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(toolDirectory, '..');
const MARKER_FACTIONS = [
  { id: 'pmc', label: 'PMC', defaultSelected: true },
  { id: 'scav', label: 'SCAV', defaultSelected: false },
];
// 2026-08-19 archive의 지도 blob 0ffa064b...는 Transition을 PMC Extraction과 같은
// #70a800 규칙으로 그린다. Co-Op은 두 진영이 함께 써야 하므로 어느 진영에서도 표시한다.
const MARKER_CATEGORIES = [
  {
    id: 'extraction',
    label: '추출구',
    sourceCategory: 'Extractions',
    defaultVisible: true,
    subtypes: [
      { id: 'transit', label: 'Transit', sourceSubtype: 'Transition', factions: ['pmc'] },
      { id: 'pmc', label: 'PMC', sourceSubtype: 'PMC Extraction', factions: ['pmc'] },
      { id: 'scav', label: 'SCAV', sourceSubtype: 'Scav Extraction', factions: ['scav'] },
      { id: 'co-op', label: 'Co-Op', sourceSubtype: 'Co-Op Extraction', factions: ['pmc', 'scav'] },
    ],
  },
];

const CDP_PORT = Number(process.env.EXTRACT_RESOURCES_PORT || 9230);
if (!Number.isInteger(CDP_PORT) || CDP_PORT < 9230 || CDP_PORT > 65_535) {
  throw new Error('EXTRACT_RESOURCES_PORT는 9230~65535 사이 정수여야 합니다.');
}

const args = process.argv.slice(2);

function argValue(name, fallback) {
  const index = args.indexOf(name);
  if (index < 0) return fallback;
  if (!args[index + 1] || args[index + 1].startsWith('--')) {
    throw new Error(`${name} 뒤에 값을 지정해야 합니다.`);
  }
  return args[index + 1];
}

const archivePath = path.resolve(argValue('--archive', path.join(repositoryRoot, 'archive')));
const outputPath = path.resolve(argValue('--out', path.join(repositoryRoot, 'resources')));
const archiveManifest = await readJson(path.join(archivePath, 'manifest.json'), 'archive manifest');
const configuredMaps = await readConfiguredMapIds();
const requestedMapArgument = argValue('--maps', '').trim();
const requestedMaps = requestedMapArgument
  ? requestedMapArgument.split(',').map((mapId) => mapId.trim()).filter(Boolean)
  : configuredMaps;

if (requestedMaps.length === 0) throw new Error('추출할 맵을 하나 이상 지정해야 합니다.');
if (new Set(requestedMaps).size !== requestedMaps.length) {
  throw new Error('추출할 맵 ID가 중복됩니다.');
}

const configuredMapSet = new Set(configuredMaps);
for (const mapId of requestedMaps) {
  if (!configuredMapSet.has(mapId)) {
    throw new Error(`${mapId}: MapConfiguration.cs에 없는 맵입니다.`);
  }
  if (!archiveManifest.maps?.[mapId]) {
    throw new Error(`${mapId}: archive manifest에 없는 맵입니다.`);
  }
}

const blobCache = new Map();

async function readJson(filePath, description) {
  try {
    return JSON.parse(await readFile(filePath, 'utf8'));
  } catch (error) {
    throw new Error(`${description}를 읽지 못했습니다: ${filePath}\n${error.message}`);
  }
}

async function readConfiguredMapIds() {
  const configurationPath = path.join(
    repositoryRoot,
    'src',
    'TanukiTarkovMap',
    'Models',
    'Data',
    'MapConfiguration.cs'
  );
  const source = await readFile(configurationPath, 'utf8');
  const mapIds = [...source.matchAll(/new MapInfo\("([a-z0-9-]+)"/g)]
    .map((match) => match[1]);
  if (mapIds.length === 0 || new Set(mapIds).size !== mapIds.length) {
    throw new Error('MapConfiguration.cs에서 고유한 맵 ID 목록을 읽지 못했습니다.');
  }
  return mapIds;
}

async function readBlob(blobHash) {
  if (!blobCache.has(blobHash)) {
    const blobPath = path.join(archivePath, 'blobs', blobHash);
    blobCache.set(blobHash, await readFile(blobPath));
  }
  return blobCache.get(blobHash).toString('utf8');
}

async function readBlobBody(blobHash) {
  if (!blobCache.has(blobHash)) {
    const blobPath = path.join(archivePath, 'blobs', blobHash);
    blobCache.set(blobHash, await readFile(blobPath));
  }
  return blobCache.get(blobHash);
}

async function readBrowserArchiveIndex() {
  // 새 프로필용 전역 캐시 응답은 첫 맵 색인에만 있으므로 verify-archive와 같은 manifest 순서로
  // 합친다. 같은 URL은 내용이 같으며, 먼저 수집한 최초 응답을 유지해야 해시 없는 요청에 답한다.
  const combinedIndex = {};
  for (const mapId of Object.keys(archiveManifest.maps || {})) {
    const mapIndex = await readJson(
      path.join(archivePath, 'maps', `${mapId}.json`),
      `${mapId} archive index`
    );
    for (const [url, entry] of Object.entries(mapIndex)) {
      if (!(url in combinedIndex)) combinedIndex[url] = entry;
    }
  }
  return combinedIndex;
}

const browserArchiveIndex = await readBrowserArchiveIndex();

function escapePattern(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

const NUMBER_PATTERN = '-?(?:\\d+(?:\\.\\d*)?|\\.\\d+)(?:e[+-]?\\d+)?';

function parseBoolean(value) {
  if (value === 'true' || value === '!0') return true;
  if (value === 'false' || value === '!1') return false;
  throw new Error(`알 수 없는 boolean 표기입니다: ${value}`);
}

function parseMapSettings(bundle, mapId) {
  const mapKey = `(?:["']${escapePattern(mapId)}["']|${escapePattern(mapId)})`;
  const propertyGap = '\\s*:\\s*';
  const separator = '\\s*,\\s*';
  const booleanPattern = '(?:true|false|![01])';
  const pattern = new RegExp(
    `(?:^|[,{])\\s*${mapKey}${propertyGap}\\{` +
      `size${propertyGap}\\{width${propertyGap}(${NUMBER_PATTERN})${separator}` +
      `height${propertyGap}(${NUMBER_PATTERN})\\}${separator}` +
      `zoom${propertyGap}(${NUMBER_PATTERN})${separator}` +
      `minZoom${propertyGap}(${NUMBER_PATTERN})${separator}` +
      `maxZoom${propertyGap}(${NUMBER_PATTERN})${separator}` +
      `transform${propertyGap}\\{rotate${propertyGap}(${NUMBER_PATTERN})${separator}` +
      `xOffset${propertyGap}(${NUMBER_PATTERN})${separator}` +
      `yOffset${propertyGap}(${NUMBER_PATTERN})${separator}` +
      `invertX${propertyGap}(${booleanPattern})${separator}` +
      `invertY${propertyGap}(${booleanPattern})${separator}` +
      `ratio${propertyGap}(${NUMBER_PATTERN})\\}\\}`,
    'i'
  );
  const match = bundle.match(pattern);
  if (!match) return null;

  const [
    width, height, zoom, minZoom, maxZoom, rotate, xOffset, yOffset,
    invertX, invertY, ratio,
  ] = match.slice(1);

  return {
    size: { width: Number(width), height: Number(height) },
    zoom: Number(zoom),
    minZoom: Number(minZoom),
    maxZoom: Number(maxZoom),
    transform: {
      rotate: Number(rotate),
      xOffset: Number(xOffset),
      yOffset: Number(yOffset),
      invertX: parseBoolean(invertX),
      invertY: parseBoolean(invertY),
      ratio: Number(ratio),
    },
  };
}

function hasCoordinateFormula(bundle) {
  const name = '[A-Za-z_$][\\w$]*';
  const xFormula = new RegExp(`\\.${'xOffset'}-${name}\\*${name}\\.ratio`);
  const yFormula = new RegExp(`\\.${'yOffset'}-${name}\\*${name}\\.ratio`);
  const rounding = new RegExp(`Math\\.round\\(${name}\\*1e4\\)\\/1e4`);
  const clockwiseRotation = new RegExp(`${name}=-${name}\\*\\(Math\\.PI\\/180\\)`);
  return xFormula.test(bundle) && yFormula.test(bundle) && rounding.test(bundle)
    && clockwiseRotation.test(bundle);
}

function extractSvgMarkup(moduleText) {
  const markupIndex = moduleText.indexOf('<defs>') >= 0
    ? moduleText.indexOf('<defs>')
    : moduleText.indexOf('<g id="wrapper">');
  if (markupIndex < 0) throw new Error('지형 모듈에서 SVG 내용의 시작을 찾지 못했습니다.');

  const quoteIndex = moduleText.lastIndexOf("'", markupIndex);
  if (quoteIndex < 0) throw new Error('지형 SVG 문자열의 시작을 찾지 못했습니다.');

  let escaped = false;
  let markup = '';
  for (let index = quoteIndex + 1; index < moduleText.length; index++) {
    const character = moduleText[index];
    if (!escaped && character === "'") return decodeJavaScriptString(markup);
    markup += character;
    escaped = !escaped && character === '\\';
    if (character !== '\\') escaped = false;
  }

  throw new Error('지형 SVG 문자열의 끝을 찾지 못했습니다.');
}

function decodeJavaScriptString(value) {
  let decoded = '';

  for (let index = 0; index < value.length; index++) {
    const character = value[index];
    if (character !== '\\') {
      decoded += character;
      continue;
    }

    const escape = value[++index];
    const simpleEscapes = {
      "'": "'", '"': '"', '\\': '\\', n: '\n', r: '\r', t: '\t',
      b: '\b', f: '\f', v: '\v', 0: '\0',
    };
    if (escape in simpleEscapes) {
      decoded += simpleEscapes[escape];
      continue;
    }

    if (escape === 'x') {
      decoded += String.fromCharCode(Number.parseInt(value.slice(index + 1, index + 3), 16));
      index += 2;
      continue;
    }

    if (escape === 'u') {
      decoded += String.fromCharCode(Number.parseInt(value.slice(index + 1, index + 5), 16));
      index += 4;
      continue;
    }

    if (escape === '\n') continue;
    if (escape === '\r' && value[index + 1] === '\n') {
      index++;
      continue;
    }

    throw new Error(`지원하지 않는 JavaScript 문자열 이스케이프입니다: \\${escape}`);
  }

  return decoded;
}

function assertPassiveSvg(markup) {
  const forbidden = [
    { pattern: /<script\b/i, name: '<script>' },
    { pattern: /<foreignObject\b/i, name: '<foreignObject>' },
    { pattern: /\son[a-z]+\s*=/i, name: '이벤트 속성' },
    { pattern: /(?:href|xlink:href)\s*=\s*["'](?:https?:|javascript:|data:)/i, name: '외부 또는 실행 URL' },
  ];

  for (const entry of forbidden) {
    if (entry.pattern.test(markup)) {
      throw new Error(`지형 SVG에 데이터가 아닌 ${entry.name}이 들어 있습니다.`);
    }
  }
}

function formatSvg(mapId, settings, innerMarkup, source) {
  const emptyElements = innerMarkup.replace(
    /<([A-Za-z][\w:.-]*)([^>]*)><\/\1>/g,
    '<$1$2 />'
  );
  const lines = emptyElements.replace(/></g, '>\n<').split('\n');
  const formatted = [];
  let depth = 1;

  for (const line of lines) {
    const trimmed = line.trim();
    if (trimmed.startsWith('</')) depth--;
    formatted.push(`${'  '.repeat(Math.max(1, depth))}${trimmed}`);

    const opensElement = /^<[^!?/][^>]*>$/.test(trimmed);
    const closesOnSameLine = /<\/[^>]+>$/.test(trimmed);
    const selfClosing = /\/>$/.test(trimmed);
    if (opensElement && !closesOnSameLine && !selfClosing) depth++;
  }

  const { width, height } = settings.size;
  return [
    '<?xml version="1.0" encoding="UTF-8"?>',
    `<!-- tarkov-market ${mapId} terrain; archive ${source.archiveCreatedAt}; ` +
      `source blob ${source.terrainBlob}. Generated by tools/extract-resources.mjs. -->`,
    `<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" ` +
      `width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" ` +
      `class="svg-map map_${mapId}">`,
    ...formatted,
    '</svg>',
    '',
  ].join('\n');
}

function findLevels(mapId, markup, renderedLevels, sourceMarkers) {
  const sourceLevelNumbers = new Set(sourceMarkers.map((marker) => marker.siteLevel));
  const seenIds = new Set();
  const seenNumbers = new Set();
  const levels = renderedLevels.map((level) => {
    if (!level.id || seenIds.has(level.id) || !Number.isFinite(level.sourceLevel)
      || seenNumbers.has(level.sourceLevel) || typeof level.label !== 'string') {
      throw new Error(
        `${mapId}: 사이트 레벨 목록이 없거나 중복됩니다: ${JSON.stringify(level)}`
      );
    }
    seenIds.add(level.id);
    seenNumbers.add(level.sourceLevel);
    const hasTerrainGroup = new RegExp(
      `<g\\s+[^>]*\\bid=["']${escapePattern(level.id)}["']`
    ).test(markup);
    return {
      id: level.id,
      label: level.label,
      sourceLevel: level.sourceLevel,
      terrainGroupId: hasTerrainGroup ? level.id : null,
      defaultVisible: level.defaultVisible,
    };
  });

  if (levels.length === 0) {
    if (sourceLevelNumbers.size > 1) {
      throw new Error(`${mapId}: 여러 마커 레벨이 있지만 사이트 레벨 목록을 찾지 못했습니다.`);
    }
    const sourceLevel = sourceLevelNumbers.size === 1 ? [...sourceLevelNumbers][0] : 1;
    const hasMainGroup = /<g\s+[^>]*\bid=["']main["']/.test(markup);
    levels.push({
      id: 'main',
      label: '지상',
      sourceLevel,
      terrainGroupId: hasMainGroup ? 'main' : null,
      defaultVisible: true,
    });
    seenNumbers.add(sourceLevel);
  }

  const missingSourceLevel = [...sourceLevelNumbers]
    .find((sourceLevel) => !seenNumbers.has(sourceLevel));
  if (missingSourceLevel !== undefined) {
    throw new Error(`${mapId}: 사이트 목록에 없는 마커 레벨입니다: ${missingSourceLevel}`);
  }
  if (levels.filter((level) => level.defaultVisible).length !== 1) {
    throw new Error(`${mapId}: 기본 표시 레벨을 하나로 특정하지 못했습니다.`);
  }
  if (levels.length > 1 && levels.some((level) => level.terrainGroupId === null)) {
    throw new Error(`${mapId}: 여러 레벨 중 지형 그룹이 없는 항목이 있습니다.`);
  }

  return levels;
}

async function findSource(index, mapId) {
  const javascriptEntries = Object.entries(index)
    .filter(([, entry]) => String(entry.mime || '').includes('javascript'));
  const settingsSources = [];

  for (const [url, entry] of javascriptEntries) {
    const bundle = await readBlob(entry.blob);
    if (!bundle.includes('xOffset')) continue;

    const settings = parseMapSettings(bundle, mapId);
    if (settings) settingsSources.push({ url, entry, bundle, settings });
  }

  if (settingsSources.length !== 1) {
    throw new Error(`${mapId}: 설정 bundle을 하나로 특정하지 못했습니다 (${settingsSources.length}개).`);
  }

  const settingsSource = settingsSources[0];
  if (!hasCoordinateFormula(settingsSource.bundle)) {
    throw new Error(`${mapId}: 확인한 좌표 변환식이 bundle에서 바뀌었습니다.`);
  }

  const { width, height } = settingsSource.settings.size;
  const terrainSources = [];

  for (const [url, entry] of javascriptEntries) {
    const moduleText = await readBlob(entry.blob);
    if (!moduleText.includes(`viewBox:"0 0 ${width} ${height}"`)) continue;
    if (!moduleText.includes('<g id="wrapper">')) continue;
    terrainSources.push({ url, entry, moduleText });
  }

  if (terrainSources.length !== 1) {
    throw new Error(
      `${mapId}: ${width}x${height} 지형 module을 하나로 특정하지 못했습니다 ` +
      `(${terrainSources.length}개).`
    );
  }

  return { settingsSource, terrainSource: terrainSources[0] };
}

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

async function connectChrome() {
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

function createSession(page, handleEvent) {
  const socket = new WebSocket(page.webSocketDebuggerUrl);
  const pending = new Map();
  let nextId = 1;

  function send(method, params = {}) {
    return new Promise((resolve, reject) => {
      const id = nextId++;
      pending.set(id, { resolve, reject });
      socket.send(JSON.stringify({ id, method, params }));
    });
  }

  socket.addEventListener('message', (message) => {
    const data = JSON.parse(message.data);
    if (data.id && pending.has(data.id)) {
      const { resolve, reject } = pending.get(data.id);
      pending.delete(data.id);
      data.error ? reject(new Error(data.error.message)) : resolve(data.result);
      return;
    }
    handleEvent?.(data, send);
  });

  return {
    ready: new Promise((resolve) => socket.addEventListener('open', resolve)),
    send,
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

async function waitUntil(cdp, expression, timeout = 30_000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    try {
      if (await evaluate(cdp, expression)) return;
    } catch {}
    await delay(100);
  }
  throw new Error(`사본 페이지 준비를 ${timeout}ms 안에 확인하지 못했습니다.`);
}

function findArchiveEntry(index, url) {
  return index[url] ?? (url.includes('?') ? index[url.split('?')[0]] : undefined);
}

async function extractRenderedMarkerState(index, mapId, pageUrl) {
  const pageEntry = index[pageUrl];
  if (!pageEntry || !String(pageEntry.mime || '').includes('html')) {
    throw new Error(`${mapId}: 사본에서 서버 렌더링 HTML을 찾지 못했습니다: ${pageUrl}`);
  }

  const chromeCandidates = [
    'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
    process.env.CHROME_PATH,
  ].filter(Boolean);
  const chromePath = chromeCandidates.find((candidate) => existsSync(candidate));
  if (!chromePath) {
    throw new Error('Chrome을 찾지 못했습니다. CHROME_PATH 환경변수로 경로를 지정하세요.');
  }
  if (!await portAvailable(CDP_PORT)) {
    throw new Error(`CDP 포트 ${CDP_PORT}을 이미 다른 프로세스가 사용 중입니다.`);
  }

  const profilePath = await mkdtemp(path.join(os.tmpdir(), 'tanuki-resource-extract-'));
  const chrome = spawn(chromePath, [
    '--headless=new',
    '--disable-gpu',
    '--disable-background-networking',
    '--disable-default-apps',
    '--disable-extensions',
    '--no-first-run',
    '--remote-debugging-address=127.0.0.1',
    `--remote-debugging-port=${CDP_PORT}`,
    `--user-data-dir=${profilePath}`,
    '--window-size=1440,1000',
    'about:blank',
  ], { stdio: 'ignore' });
  const chromeExited = new Promise((resolve) => chrome.once('exit', resolve));
  const killChrome = () => chrome.kill();
  process.once('exit', killChrome);

  const blockedUrls = new Set();
  let markerResponse = null;
  let cdp = null;

  try {
    const page = await connectChrome();
    cdp = createSession(page, (event, send) => {
      if (event.method !== 'Fetch.requestPaused') return;
      const { requestId, request } = event.params;
      const entry = findArchiveEntry(index, request.url);

      if (!entry) {
        blockedUrls.add(request.url);
        send('Fetch.failRequest', { requestId, errorReason: 'ConnectionRefused' }).catch(() => {});
        return;
      }

      if (!markerResponse && new URL(request.url).pathname === '/api/be/markers/list') {
        markerResponse = { url: request.url, blob: entry.blob };
      }

      readBlobBody(entry.blob)
        .then((body) => send('Fetch.fulfillRequest', {
          requestId,
          responseCode: entry.status || 200,
          responseHeaders: [
            { name: 'content-type', value: entry.mime || 'application/octet-stream' },
            { name: 'cache-control', value: 'no-store' },
          ],
          body: body.toString('base64'),
        }))
        .catch(() => send('Fetch.failRequest', {
          requestId,
          errorReason: 'Failed',
        }).catch(() => {}));
    });
    await cdp.ready;
    await cdp.send('Page.enable');
    await cdp.send('Network.enable');
    await cdp.send('Network.setCacheDisabled', { cacheDisabled: true });
    await cdp.send('Fetch.enable', { patterns: [{ urlPattern: '*' }] });
    await cdp.send('Page.navigate', { url: pageUrl });
    try {
      await waitUntil(
        cdp,
        `(() => {
          const root = document.querySelector('#__nuxt')?._vnode?.component?.proxy?.$nuxt;
          const state = root?.payload?.state?.['$squestsState'];
          return state?.map === ${JSON.stringify(mapId)} && Array.isArray(state.markers)
            && state.markers.length > 0 && !!document.querySelector('.map-wrap.inited');
        })()`
      );
    } catch (error) {
      const diagnostic = await evaluate(cdp, `JSON.stringify((() => {
        const root = document.querySelector('#__nuxt')?._vnode?.component?.proxy?.$nuxt;
        const state = root?.payload?.state?.['$squestsState'];
        return {
          url: location.href,
          title: document.title,
          readyState: document.readyState,
          nuxtRoot: !!document.querySelector('#__nuxt'),
          nuxtRuntime: !!root,
          markerMap: state?.map,
          markerCount: state?.markers?.length,
          mapInited: !!document.querySelector('.map-wrap.inited'),
        };
      })())`).catch(() => '{}');
      const blocked = [...blockedUrls].slice(0, 5).join(', ') || '없음';
      throw new Error(`${error.message} 상태 ${diagnostic}, 막은 요청: ${blocked}`);
    }

    const sourceCategories = MARKER_CATEGORIES.map((category) => category.sourceCategory);
    const renderedJson = await evaluate(cdp, `JSON.stringify((() => {
      const rootVNode = document.querySelector('#__nuxt')._vnode;
      const nuxt = rootVNode.component.proxy.$nuxt;
      const state = nuxt.payload.state['$squestsState'];
      const wantedCategories = new Set(${JSON.stringify(sourceCategories)});
      const visited = new Set();
      const pending = [rootVNode];
      let leftPanel = null;

      while (pending.length > 0 && !leftPanel) {
        const vnode = pending.pop();
        if (!vnode || typeof vnode !== 'object' || visited.has(vnode)) continue;
        visited.add(vnode);
        if (vnode.component) {
          const component = vnode.component;
          const componentName = component.type?.__name || component.type?.name;
          if (componentName === 'MapLeftPanel') leftPanel = component;
          pending.push(component.subTree);
        }
        if (Array.isArray(vnode.children)) pending.push(...vnode.children);
        if (vnode.suspense?.activeBranch) pending.push(vnode.suspense.activeBranch);
      }

      if (!leftPanel || typeof leftPanel.props.map?.gamePosToMapPos !== 'function') {
        throw new Error('MapLeftPanel에서 좌표 변환 함수를 찾지 못했습니다.');
      }

      const siteMap = leftPanel.props.map;
      const markers = state.markers
        .filter((marker) => wantedCategories.has(marker.category))
        .map((marker) => ({
          id: marker.uid,
          sourceCategory: marker.category,
          sourceSubtype: marker.subCategory,
          name: marker.name,
          siteLevel: marker.level,
          position: siteMap.gamePosToMapPos(marker.geometry.x, marker.geometry.y),
        }));

      const levelById = new Map();
      const layerSettings = leftPanel.props.visibleLayers || {};
      for (const element of document.querySelectorAll('[data-layer]')) {
        const input = element.querySelector('input[name="layers"]');
        const numberText = element.querySelector('.level-num')?.textContent?.trim();
        const layerId = element.dataset.layer;
        if (!input || !layerId || levelById.has(layerId)) continue;
        const fallbackLevel = layerId === 'basement' || layerId === 'bunker'
          ? -1
          : layerId === 'main'
            ? 1
            : Number(layerId.match(/^level(\\d+)$/)?.[1]);
        levelById.set(element.dataset.layer, {
          id: layerId,
          label: numberText
            ? element.textContent.replace(numberText, '').trim()
            : element.textContent.trim(),
          sourceLevel: Number(
            layerSettings[layerId]?.num
              ?? (numberText ? numberText.replace(/[()]/g, '') : fallbackLevel)
          ),
          defaultVisible: input.checked,
        });
      }

      const titlePrefix = 'Map - ';
      const titleSuffix = ' - Tarkov Market';
      const title = document.title.startsWith(titlePrefix) && document.title.endsWith(titleSuffix)
        ? document.title.slice(titlePrefix.length, -titleSuffix.length)
        : ${JSON.stringify(mapId)};

      return {
        mapId: state.map,
        title,
        levels: [...levelById.values()],
        categories: leftPanel.props.categories,
        markers,
      };
    })())`);

    return {
      ...JSON.parse(renderedJson),
      pageEntry,
      markerResponse,
      blockedRequestCount: blockedUrls.size,
    };
  } finally {
    await cdp?.send('Fetch.disable').catch(() => {});
    cdp?.close();
    if (chrome.exitCode === null) {
      chrome.kill();
      await Promise.race([chromeExited, delay(3_000)]);
    }
    process.removeListener('exit', killChrome);
    await rm(profilePath, { recursive: true, force: true }).catch(() => {});
  }
}

function buildMarkerResource(mapId, levels, pageUrl, renderedState) {
  if (renderedState.mapId !== mapId) {
    throw new Error(`${mapId}: 페이지의 현재 맵이 ${renderedState.mapId}입니다.`);
  }
  if (!renderedState.markerResponse) {
    throw new Error(`${mapId}: 페이지가 사용한 마커 응답을 특정하지 못했습니다.`);
  }

  const categoryBySource = new Map(
    MARKER_CATEGORIES.map((category) => [category.sourceCategory, category])
  );
  const levelBySourceNumber = new Map(
    levels.map((level) => [level.sourceLevel, level.id])
  );
  const seenIds = new Set();
  const sourceCounts = new Map();
  const markers = renderedState.markers.map((sourceMarker) => {
    const category = categoryBySource.get(sourceMarker.sourceCategory);
    const subtype = category?.subtypes.find(
      (entry) => entry.sourceSubtype === sourceMarker.sourceSubtype
    );
    if (!category || !subtype) {
      throw new Error(
        `${mapId}: 지원하지 않는 마커 구분입니다: ` +
        `${sourceMarker.sourceCategory} / ${sourceMarker.sourceSubtype}`
      );
    }
    if (!sourceMarker.id || seenIds.has(sourceMarker.id)) {
      throw new Error(`${mapId}: 마커 UID가 없거나 중복됩니다: ${sourceMarker.id}`);
    }
    seenIds.add(sourceMarker.id);

    const levelId = levelBySourceNumber.get(sourceMarker.siteLevel);
    if (!levelId) {
      throw new Error(`${mapId}: 알 수 없는 마커 레벨입니다: ${sourceMarker.siteLevel}`);
    }
    if (!Number.isFinite(sourceMarker.position?.x) || !Number.isFinite(sourceMarker.position?.y)) {
      throw new Error(`${mapId}: ${sourceMarker.id}의 지도 좌표가 올바르지 않습니다.`);
    }

    const sourceCountKey = `${category.sourceCategory}\n${subtype.sourceSubtype}`;
    sourceCounts.set(sourceCountKey, (sourceCounts.get(sourceCountKey) || 0) + 1);
    return {
      id: sourceMarker.id,
      category: category.id,
      subtype: subtype.id,
      name: typeof sourceMarker.name === 'string' ? sourceMarker.name : '',
      levelId,
      position: {
        x: sourceMarker.position.x,
        y: sourceMarker.position.y,
      },
    };
  });

  const listedCounts = {};
  for (const category of MARKER_CATEGORIES) {
    const sourceCategoryCounts = renderedState.categories?.[category.sourceCategory];
    if (!sourceCategoryCounts || typeof sourceCategoryCounts !== 'object') {
      throw new Error(`${mapId}: 좌측 목록에서 ${category.sourceCategory} 수량을 찾지 못했습니다.`);
    }
    listedCounts[category.id] = {};
    for (const subtype of category.subtypes) {
      const sourceCountKey = `${category.sourceCategory}\n${subtype.sourceSubtype}`;
      const extractedCount = sourceCounts.get(sourceCountKey) || 0;
      const listedCount = sourceCategoryCounts[subtype.sourceSubtype] ?? 0;
      if (listedCount !== extractedCount) {
        throw new Error(
          `${mapId}: 좌측 목록의 ${category.sourceCategory} / ${subtype.sourceSubtype} ` +
          `수량 ${listedCount}과 추출 수량 ${extractedCount}이 다릅니다.`
        );
      }
      listedCounts[category.id][subtype.id] = listedCount;
    }
  }

  const categoryOrder = new Map(MARKER_CATEGORIES.map((category, index) => [category.id, index]));
  const subtypeOrder = new Map(MARKER_CATEGORIES.flatMap((category) =>
    category.subtypes.map((subtype, index) => [`${category.id}\n${subtype.id}`, index])
  ));
  markers.sort((left, right) =>
    categoryOrder.get(left.category) - categoryOrder.get(right.category)
    || subtypeOrder.get(`${left.category}\n${left.subtype}`)
      - subtypeOrder.get(`${right.category}\n${right.subtype}`)
    || left.id.localeCompare(right.id, 'en')
  );

  return {
    schemaVersion: 1,
    mapId,
    coordinateSpace: 'map',
    factions: MARKER_FACTIONS,
    categories: MARKER_CATEGORIES,
    markers,
    source: {
      page: pageUrl,
      archiveCreatedAt: archiveManifest.createdAt,
      htmlBlob: renderedState.pageEntry.blob,
      markerResponse: renderedState.markerResponse,
      markerState: '$nuxt.payload.state.$squestsState.markers',
      categoryState: 'MapLeftPanel.props.categories',
      listedCounts,
      mapPosition: 'MapLeftPanel.props.map.gamePosToMapPos(geometry.x, geometry.y)',
    },
  };
}

const extractedMaps = [];

for (const mapId of requestedMaps) {
  const indexPath = path.join(archivePath, 'maps', `${mapId}.json`);
  const index = await readJson(indexPath, `${mapId} archive index`);
  const { settingsSource, terrainSource } = await findSource(index, mapId);
  const innerMarkup = extractSvgMarkup(terrainSource.moduleText);
  assertPassiveSvg(innerMarkup);
  const pageUrl = archiveManifest.maps?.[mapId]?.url || `${archiveManifest.site}/maps/${mapId}`;
  const renderedState = await extractRenderedMarkerState(browserArchiveIndex, mapId, pageUrl);
  const levels = findLevels(mapId, innerMarkup, renderedState.levels, renderedState.markers);
  const markerResource = buildMarkerResource(mapId, levels, pageUrl, renderedState);
  const mapPath = path.join(outputPath, 'maps', mapId);
  const source = {
    site: archiveManifest.site,
    page: pageUrl,
    archiveCreatedAt: archiveManifest.createdAt,
    settingsUrl: settingsSource.url,
    settingsBlob: settingsSource.entry.blob,
    terrainUrl: terrainSource.url,
    terrainBlob: terrainSource.entry.blob,
    coordinateFormula: {
      rotation: '-transform.rotate degrees',
      mapX: 'xOffset - rotatedX * ratio',
      mapY: 'yOffset - rotatedY * ratio',
      rounding: '4 decimal places',
    },
    directionFormula: {
      directionX: '2 * (quaternion.x * quaternion.z + quaternion.w * quaternion.y)',
      directionZ: '1 - 2 * (quaternion.x^2 + quaternion.y^2)',
      screenDegrees: 'normalize(gameDegrees + 270 - transform.rotate)',
    },
  };
  const meta = {
    schemaVersion: 1,
    mapId,
    title: renderedState.title,
    ...settingsSource.settings,
    levels,
    source,
  };

  await mkdir(mapPath, { recursive: true });
  await writeFile(
    path.join(mapPath, 'map.svg'),
    formatSvg(mapId, settingsSource.settings, innerMarkup, source),
    'utf8'
  );
  await writeFile(path.join(mapPath, 'meta.json'), `${JSON.stringify(meta, null, 2)}\n`, 'utf8');
  await writeFile(
    path.join(mapPath, 'markers.json'),
    `${JSON.stringify(markerResource, null, 2)}\n`,
    'utf8'
  );
  extractedMaps.push(mapId);

  const markerSummary = markerResource.categories.map((category) => {
    const count = markerResource.markers.filter((marker) => marker.category === category.id).length;
    return `${category.label} ${count}개`;
  }).join(', ');
  console.log(
    `${mapId}: ${settingsSource.settings.size.width}x${settingsSource.settings.size.height}, ` +
    `레벨 ${levels.length}개, ${markerSummary}, 지형 blob ${terrainSource.entry.blob}, ` +
    `막은 비필수 요청 ${renderedState.blockedRequestCount}개`
  );
}

const existingManifestPath = path.join(outputPath, 'manifest.json');
let existingMaps = [];
if (existsSync(existingManifestPath)) {
  const existingManifest = await readJson(existingManifestPath, 'resource manifest');
  if (Array.isArray(existingManifest.maps)) existingMaps = existingManifest.maps;
}

const retainedMaps = existingMaps.filter((mapId) => configuredMapSet.has(mapId));
const presentMaps = new Set([...retainedMaps, ...extractedMaps]);
const maps = configuredMaps.filter((mapId) => presentMaps.has(mapId));
const resourceManifest = {
  schemaVersion: 1,
  source: archiveManifest.site,
  collectedAt: archiveManifest.createdAt,
  maps,
};
await mkdir(outputPath, { recursive: true });
await writeFile(existingManifestPath, `${JSON.stringify(resourceManifest, null, 2)}\n`, 'utf8');

console.log(`리소스 저장 위치: ${outputPath}`);
