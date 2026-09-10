#!/usr/bin/env node
/**
 * verify-archive.mjs - 사본만으로 맵이 뜨는지 검사한다
 *
 * 왜 필요한가: 사본이 불완전해도 화면은 그럴듯하게 뜬다. 실제로 맵의 지형 조각이 빈 채로
 * 담긴 적이 있는데(2026-08-18), 앱은 그것을 200에 빈 본문으로 돌려주고 사이트는 마커만 있고
 * 지형이 없는 화면을 그렸다. 사본 파일이 있는지 세는 것만으로는 이런 상태를 잡지 못한다.
 * 실제로 열어 봐야 안다.
 *
 * 무엇을 하는가: 앱의 로컬 모드와 같은 규칙을 브라우저 밖에서 흉내 낸다. 모든 요청을 가로채
 * 사본에 있으면 그 본문으로 응답하고, 없으면 실패시킨다. 네트워크로 나가는 요청이 하나도
 * 없으므로 인터넷이 끊긴 상태와 같다. 그 상태에서 맵마다 바닥 맵, 마커 층, window.pilot이
 * 있는지 본다.
 *
 * 사용법:
 *   node tools/verify-archive.mjs                      모든 맵
 *   node tools/verify-archive.mjs --maps lab,customs   일부만
 *   node tools/verify-archive.mjs --archive D:\archive  사본 위치 지정
 *   node tools/verify-archive.mjs --local-core --maps shoreline
 *       Local 전용 UI, 마커 축소, PMC/SCAV, 층 전환과 Online 복원을 검사
 *
 * 결과: 맵 하나라도 실패하면 종료 코드 1로 끝난다. 사본을 다시 만든 뒤에는 이 검사를 통과해야
 * 배포에 넣는다.
 *
 * 주의: 일반 사본 검사는 디버깅 포트 9225, Local 코어 검사는 9242를 쓴다. 같은 포트를 쓰면
 * 명령이 엉뚱한 브라우저로 흘러간다. VERIFY_PORT로 9242 이후의 빈 포트를 고를 수 있다.
 *
 * 수집 도구(archive-maps.mjs)와 브라우저 실행 부분이 겹치지만 각자 둔다. 사본을 만드는 도구와
 * 검사하는 도구가 같은 코드를 공유하면, 그 코드가 틀렸을 때 두 쪽이 같이 틀린 채로 통과한다.
 */
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { spawn } from 'node:child_process';
import { rm, mkdtemp } from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';

const args = process.argv.slice(2);
const LOCAL_CORE = args.includes('--local-core');
const PORT = Number(process.env.VERIFY_PORT || (LOCAL_CORE ? 9242 : 9225));
if (!Number.isInteger(PORT) || PORT < (LOCAL_CORE ? 9242 : 9225) || PORT > 65_535) {
  throw new Error(
    `VERIFY_PORT는 ${LOCAL_CORE ? '9242' : '9225'}~65535 사이 정수여야 합니다.`
  );
}

// MapConfiguration.cs의 맵 ID와 같은 순서로 둔다
const ALL_MAPS = [
  'ground-zero', 'factory', 'customs', 'interchange', 'woods', 'shoreline',
  'reserve', 'lighthouse', 'streets', 'lab', 'labyrinth', 'icebreaker',
];

const argValue = (name, fallback) => {
  const index = args.indexOf(name);
  return index >= 0 && args[index + 1] ? args[index + 1] : fallback;
};

const ARCHIVE = path.resolve(argValue('--archive', path.join(process.cwd(), 'archive')));
const SCREENSHOT_DIRECTORY = argValue('--screenshots', '').trim();
const MAPS = argValue('--maps', '').trim()
  ? argValue('--maps', '').split(',').map((m) => m.trim()).filter(Boolean)
  : ALL_MAPS;

if (LOCAL_CORE && MAPS.length !== 1) {
  throw new Error('--local-core 검사는 --maps로 맵 하나를 지정해야 합니다.');
}

// 페이지가 뜨기를 기다리는 시간 (ms). 사본은 디스크에서 오므로 온라인보다 짧아도 된다
const SETTLE = 12000;

const CHROME_CANDIDATES = [
  'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
  'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
  process.env.CHROME_PATH,
].filter(Boolean);

const chromePath = CHROME_CANDIDATES.find((p) => existsSync(p));
if (!chromePath) {
  console.error('Chrome을 찾지 못했습니다. CHROME_PATH 환경변수로 경로를 지정하세요.');
  process.exit(1);
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// 앱과 같은 방식으로 색인을 합친다. 같은 주소가 맵마다 나오지만 내용이 같으므로 처음 것만 쓴다
const index = new Map();
for (const mapId of ALL_MAPS) {
  const indexPath = path.join(ARCHIVE, 'maps', `${mapId}.json`);
  if (!existsSync(indexPath)) continue;

  for (const [url, entry] of Object.entries(JSON.parse(await readFile(indexPath, 'utf8')))) {
    if (!index.has(url)) index.set(url, entry);
  }
}

if (index.size === 0) {
  console.error(`사본이 비어 있습니다: ${ARCHIVE}`);
  process.exit(1);
}

console.log(`사본 ${ARCHIVE}, 항목 ${index.size}개`);

const bodies = new Map();
const readBlob = async (blob) => {
  if (!bodies.has(blob)) bodies.set(blob, await readFile(path.join(ARCHIVE, 'blobs', blob)));
  return bodies.get(blob);
};

// 앱의 MapArchive.Find와 같은 규칙: 정확히 일치, 없으면 질의를 뗀 주소로 한 번 더
const find = (url) => index.get(url) ?? (url.includes('?') ? index.get(url.split('?')[0]) : undefined);

const profileDir = await mkdtemp(path.join(os.tmpdir(), 'verify-archive-'));
const chrome = spawn(chromePath, [
  ...(LOCAL_CORE ? ['--headless=new', '--disable-gpu', '--no-first-run'] : []),
  '--remote-debugging-address=127.0.0.1',
  `--remote-debugging-port=${PORT}`,
  `--user-data-dir=${profileDir}`,
  '--window-size=1280,1000',
  'about:blank',
], { stdio: 'ignore' });

process.on('exit', () => chrome.kill());

async function connect() {
  for (let attempt = 0; attempt < 40; attempt++) {
    try {
      const targets = await (await fetch(`http://127.0.0.1:${PORT}/json/list`)).json();
      const page = targets.find((t) => t.type === 'page');
      if (page) return page;
    } catch {}
    await sleep(500);
  }
  throw new Error('브라우저에 붙지 못했습니다');
}

const page = await connect();
const ws = new WebSocket(page.webSocketDebuggerUrl);
let nextId = 1;
const pending = new Map();
const blocked = new Set();

const send = (method, params = {}) =>
  new Promise((resolve, reject) => {
    const id = nextId++;
    pending.set(id, { resolve, reject });
    ws.send(JSON.stringify({ id, method, params }));
  });

ws.addEventListener('message', async (message) => {
  const data = JSON.parse(message.data);

  if (data.id && pending.has(data.id)) {
    const { resolve, reject } = pending.get(data.id);
    pending.delete(data.id);
    data.error ? reject(new Error(data.error.message)) : resolve(data.result);
    return;
  }

  if (data.method !== 'Fetch.requestPaused') return;

  const { requestId, request } = data.params;
  const entry = find(request.url);

  if (!entry) {
    blocked.add(request.url);
    send('Fetch.failRequest', { requestId, errorReason: 'ConnectionRefused' }).catch(() => {});
    return;
  }

  const body = await readBlob(entry.blob);
  send('Fetch.fulfillRequest', {
    requestId,
    responseCode: entry.status || 200,
    responseHeaders: [{ name: 'content-type', value: entry.mime || 'application/octet-stream' }],
    body: body.toString('base64'),
  }).catch(() => {});
});

await new Promise((r) => ws.addEventListener('open', r));
await send('Page.enable');
await send('Network.enable');

// 캐시가 대신 답하면 사본이 빠져도 화면이 뜬다. 그 상태로는 검사가 되지 않는다
await send('Network.setCacheDisabled', { cacheDisabled: true });
await send('Fetch.enable', { patterns: [{ urlPattern: '*' }] });

const evaluate = async (expression) => {
  const result = await send('Runtime.evaluate', {
    expression,
    returnByValue: true,
    awaitPromise: true,
  });
  if (result.exceptionDetails) {
    throw new Error(result.exceptionDetails.exception?.description || '페이지 평가가 실패했습니다.');
  }
  return result.result.value;
};

const captureScreenshot = async (name) => {
  if (!SCREENSHOT_DIRECTORY) return null;
  const outputDirectory = path.resolve(SCREENSHOT_DIRECTORY);
  await mkdir(outputDirectory, { recursive: true });
  const outputPath = path.join(outputDirectory, `${name}.png`);
  const capture = await send('Page.captureScreenshot', { format: 'png', fromSurface: true });
  await writeFile(outputPath, Buffer.from(capture.data, 'base64'));
  return outputPath;
};

const CHECK = `JSON.stringify((() => {
  const svg = document.querySelector('svg.svg-map');
  const layer = document.querySelector('svg.map-layer');
  const canvas = document.querySelector('canvas.doc-map-canvas');
  return {
    baseMap: svg ? svg.getAttribute('class') : null,
    groups: svg ? svg.children.length : 0,
    markerLayer: !!layer,
    canvas: canvas ? Math.round(canvas.getBoundingClientRect().width) : 0,
    pilot: typeof window.pilot,
  };
})())`;

let failed = 0;

if (LOCAL_CORE) {
  const mapId = MAPS[0];
  const webElementsScript = await readFile(new URL(
    '../src/TanukiTarkovMap/Models/JavaScript/Scripts/web-elements-control.js',
    import.meta.url
  ), 'utf8');
  const mapMarkersScript = await readFile(new URL(
    '../src/TanukiTarkovMap/Models/JavaScript/Scripts/map-markers.js',
    import.meta.url
  ), 'utf8');
  const onlineSelection = {
    'Extractions_PMC Extraction': true,
    'Spawns_PMC Spawn': true,
    'Quests_Quest': true,
    'Keys_Unlock': true,
    'Keys_Key Spawn': true,
    'Keys_Keycard Spawn': true,
    'Miscellaneous_Lever': true,
  };
  const localPmcSelection = {
    'Extractions_Co-Op Extraction': true,
    'Extractions_Transition': true,
    'Extractions_PMC Extraction': true,
  };
  const localScavSelection = {
    'Extractions_Co-Op Extraction': true,
    'Extractions_Scav Extraction': true,
  };
  let initializationScriptId = null;

  const sameStringSet = (left, right) =>
    JSON.stringify([...left].sort()) === JSON.stringify([...right].sort());
  const truthyKeys = (value) => Object.entries(value || {})
    .filter(([, selected]) => selected)
    .map(([key]) => key);
  const assert = (condition, message) => {
    if (!condition) throw new Error(message);
  };

  const installInitialization = async ({ local, isPmc, seedOnlineSelection = false }) => {
    if (initializationScriptId) {
      await send('Page.removeScriptToEvaluateOnNewDocument', {
        identifier: initializationScriptId,
      });
    }
    const seed = seedOnlineSelection
      ? `try {
          if (!sessionStorage.getItem('tanuki-local-core-verify-seeded')) {
            localStorage.setItem('sel_cats_map', ${JSON.stringify(JSON.stringify(onlineSelection))});
            sessionStorage.setItem('tanuki-local-core-verify-seeded', 'true');
          }
        } catch (error) { console.error(error); }`
      : '';
    const modeCall = `window.setLocalMapMode(${local}, ${isPmc});`;
    const result = await send('Page.addScriptToEvaluateOnNewDocument', {
      source: `${seed}\n${webElementsScript}\n${modeCall}`,
    });
    initializationScriptId = result.identifier;
  };

  const navigate = async () => {
    blocked.clear();
    await send('Page.navigate', { url: `https://tarkov-market.com/maps/${mapId}` });
    await sleep(SETTLE);
  };

  const readLocalState = async () => JSON.parse(await evaluate(`JSON.stringify((() => {
    const visible = (selector) => {
      const element = document.querySelector(selector);
      if (!element) return false;
      const style = getComputedStyle(element);
      const bounds = element.getBoundingClientRect();
      return style.display !== 'none' && style.visibility !== 'hidden'
        && bounds.width > 0 && bounds.height > 0;
    };
    const selected = JSON.parse(localStorage.getItem('sel_cats_map') || '{}');
    const activeCategories = [];
    for (const group of document.querySelectorAll('.panel_left .two-columns > div')) {
      const category = group.firstElementChild?.querySelector('.bold')?.textContent?.trim();
      const items = group.querySelector(':scope > .items');
      if (!category || !items) continue;
      for (const row of items.children) {
        const subtype = row.firstElementChild?.textContent?.trim();
        if (subtype && !row.classList.contains('inactive')) {
          activeCategories.push(category + '_' + subtype);
        }
      }
    }
    const markerCanvas = document.querySelector('canvas.markers-canvas');
    let opaqueMarkerPixels = -1;
    if (markerCanvas) {
      const pixels = markerCanvas.getContext('2d')
        .getImageData(0, 0, markerCanvas.width, markerCanvas.height).data;
      opaqueMarkerPixels = 0;
      for (let index = 3; index < pixels.length; index += 4) {
        if (pixels[index] !== 0) opaqueMarkerPixels++;
      }
    }
    return {
      localClass: document.documentElement.classList.contains('tanuki-local-core'),
      hiddenClass: document.documentElement.classList.contains('tanuki-panels-hidden'),
      selected,
      backup: localStorage.getItem('tanuki-online-map-categories'),
      activeCategories,
      opaqueMarkerPixels,
      baseMap: !!document.querySelector('svg.svg-map'),
      markerCanvas: !!markerCanvas,
      panelLeft: visible('.panel_left'),
      panelTop: visible('.panel_top'),
      panelRight: visible('.panel_right'),
      levels: visible('.panel_right .layers'),
      squad: visible('.panel_right .squad-panel'),
      userLayers: visible('.panel_right .user-layers-panel'),
      quests: visible('.panel_right .tools_quests'),
      pilotBanner: visible('.maps-site-chrome > .head-pilot'),
      alertBox: visible('.maps-site-chrome > .alert-box'),
      levelCount: document.querySelectorAll('.panel_right .layers [data-layer] input').length,
      positionMarker: !!document.querySelector('.marker'),
      directionMarker: !!document.querySelector('.marker .triangle-indicator'),
    };
  })())`));

  const testLevelSwitch = async () => JSON.parse(await evaluate(`(async () => {
    const inputs = [...document.querySelectorAll('.panel_right .layers [data-layer] input')];
    const current = inputs.find((input) => input.checked);
    const target = inputs.find((input) => !input.checked
      && document.querySelector('svg.svg-map [id="' + CSS.escape(
        input.closest('[data-layer]').dataset.layer
      ) + '"]'));
    if (!current || !target) return JSON.stringify({ available: false });
    const targetLayer = target.closest('[data-layer]').dataset.layer;
    const group = document.querySelector('svg.svg-map [id="' + CSS.escape(targetLayer) + '"]');
    const beforeDisplay = getComputedStyle(group).display;
    target.click();
    await new Promise((resolve) => setTimeout(resolve, 500));
    const result = {
      available: true,
      targetLayer,
      beforeDisplay,
      afterDisplay: getComputedStyle(group).display,
      targetChecked: target.checked,
    };
    current.click();
    return JSON.stringify(result);
  })()`));

  try {
    await installInitialization({ local: false, isPmc: true, seedOnlineSelection: true });
    await navigate();
    const before = await readLocalState();
    assert(before.baseMap && before.markerCanvas, '정리 전 지도나 마커 캔버스가 없습니다.');
    assert(before.panelLeft && before.panelTop && before.panelRight,
      '정리 전 패널 상태를 확인하지 못했습니다.');
    assert(before.pilotBanner && before.alertBox,
      '정리 전 Pilot 배너나 안내 줄 상태를 확인하지 못했습니다.');
    assert(sameStringSet(truthyKeys(before.selected), truthyKeys(onlineSelection)),
      '정리 전 온라인 마커 선택을 준비하지 못했습니다.');
    const beforeScreenshot = await captureScreenshot(`${mapId}-before`);

    await installInitialization({ local: true, isPmc: true });
    await navigate();
    await evaluate('window.hidePanelLeft(); window.clickPmcExtraction();');
    await sleep(500);
    let localPmc = await readLocalState();
    assert(localPmc.localClass && localPmc.hiddenClass, 'Local 전용 클래스가 적용되지 않았습니다.');
    // 숨김 규칙은 두 층이다. Local 모드는 코어 밖 UI를 늘 감추고, "UI 요소 숨기기" 체크는 그
    // 위에서 남은 Levels까지 감춘다. 체크를 켠 상태에서 무언가 남으면 사용자에게는 체크박스가
    // 고장난 것으로 보인다
    assert(!localPmc.panelLeft && !localPmc.panelTop && !localPmc.panelRight && !localPmc.levels,
      'UI 요소 숨기기를 켠 Local 화면에 패널이 남았습니다.');
    assert(!localPmc.squad && !localPmc.userLayers && !localPmc.quests,
      'Local 화면에 Squad, Layers 또는 Quests 패널이 남아 있습니다.');
    assert(!localPmc.pilotBanner && !localPmc.alertBox,
      'Local 화면에 Pilot 배너나 안내 줄이 남아 있습니다.');
    assert(sameStringSet(truthyKeys(localPmc.selected), truthyKeys(localPmcSelection)),
      'Local PMC 마커 선택에 추출구가 아닌 종류가 남았습니다.');
    assert(localPmc.activeCategories.every((category) => category.startsWith('Extractions_')),
      'Local PMC 지도에 추출구가 아닌 마커 종류가 활성화됐습니다.');
    assert(localPmc.opaqueMarkerPixels >= 0
      && localPmc.opaqueMarkerPixels < before.opaqueMarkerPixels,
    'Local PMC 마커 캔버스의 불투명 픽셀이 줄지 않았습니다.');

    // 체크를 풀면 코어인 Levels만 돌아오고 나머지는 Local 전용 규칙으로 계속 가려져야 한다.
    // 층마다 탈출구가 다르므로 층 전환은 코어에 속한다. 그래서 이 상태에서 전환을 확인한다.
    await evaluate('window.restorePanels()');
    await sleep(100);
    const localWithPanelsRestored = await readLocalState();
    assert(!localWithPanelsRestored.hiddenClass
      && !localWithPanelsRestored.panelLeft && !localWithPanelsRestored.panelTop
      && localWithPanelsRestored.panelRight && localWithPanelsRestored.levels
      && !localWithPanelsRestored.squad && !localWithPanelsRestored.userLayers
      && !localWithPanelsRestored.quests && !localWithPanelsRestored.pilotBanner
      && !localWithPanelsRestored.alertBox,
    'UI 요소 숨기기를 해제하자 Local 코어 밖의 패널이 다시 나타났습니다.');

    const levelSwitch = await testLevelSwitch();
    assert(levelSwitch.available && levelSwitch.beforeDisplay === 'none'
      && levelSwitch.afterDisplay !== 'none' && levelSwitch.targetChecked,
    'Local 화면에서 층을 전환해도 대상 지형이 나타나지 않습니다.');

    await evaluate('window.hidePanelLeft()');

    await evaluate(mapMarkersScript);
    for (let attempt = 0; attempt < 20; attempt++) {
      await evaluate('window.pilot.position(100, 200, 0, 45)');
      await sleep(150);
      localPmc = await readLocalState();
      if (localPmc.positionMarker && localPmc.directionMarker) break;
    }
    assert(localPmc.positionMarker && localPmc.directionMarker,
      'Local 화면에 현재 위치와 방향 마커가 남지 않았습니다.');

    const localScreenshot = await captureScreenshot(`${mapId}-local-core`);

    await evaluate('window.clickScavExtraction()');
    await sleep(500);
    const localScav = await readLocalState();
    const localScavKeys = truthyKeys(localScav.selected);
    assert(sameStringSet(localScavKeys, truthyKeys(localScavSelection)),
    'Local SCAV 선택이 PMC 추출구를 끄고 SCAV 추출구를 켜지 못했습니다.');
    assert(localScav.activeCategories.every((category) => category.startsWith('Extractions_'))
      && !localScav.activeCategories.includes('Extractions_Transit')
      && !localScav.activeCategories.includes('Extractions_PMC Extraction'),
      'Local SCAV 지도에 추출구가 아닌 마커 종류가 활성화됐습니다.');

    await installInitialization({ local: false, isPmc: false });
    await navigate();
    const restored = await readLocalState();
    assert(!restored.localClass && restored.backup === null,
      'Online 복귀 뒤 Local 클래스나 백업이 남았습니다.');
    assert(sameStringSet(truthyKeys(restored.selected), truthyKeys(onlineSelection)),
      'Online 복귀 뒤 사용자의 마커 선택이 원래대로 돌아오지 않았습니다.');
    assert(restored.panelLeft && restored.panelTop && restored.panelRight,
      'Online 복귀 뒤 기존 패널이 나타나지 않았습니다.');
    assert(restored.pilotBanner && restored.alertBox,
      'Online 복귀 뒤 Pilot 배너나 안내 줄이 나타나지 않았습니다.');
    assert(restored.opaqueMarkerPixels === before.opaqueMarkerPixels,
      'Online 복귀 뒤 마커 캔버스가 정리 전 상태와 다릅니다.');

    await evaluate('window.clickScavExtraction()');
    await sleep(500);
    const onlineScav = await readLocalState();
    const expectedOnlineScav = truthyKeys(onlineSelection)
      .filter((category) => category !== 'Extractions_PMC Extraction')
      .concat('Extractions_Scav Extraction');
    assert(sameStringSet(truthyKeys(onlineScav.selected), expectedOnlineScav)
      && onlineScav.activeCategories.includes('Extractions_Scav Extraction')
      && !onlineScav.activeCategories.includes('Extractions_PMC Extraction'),
    'Online 복귀 뒤 기존 PMC/SCAV 전환이 달라졌습니다.');
    await evaluate('window.clickPmcExtraction()');
    await sleep(500);
    const onlinePmc = await readLocalState();
    assert(sameStringSet(truthyKeys(onlinePmc.selected), truthyKeys(onlineSelection)),
      'Online PMC 전환 뒤 원래 마커 선택을 유지하지 못했습니다.');

    const removedPixels = before.opaqueMarkerPixels - localPmc.opaqueMarkerPixels;
    const removedPercent = before.opaqueMarkerPixels === 0
      ? 0
      : removedPixels / before.opaqueMarkerPixels * 100;
    console.log(
      `OK   ${mapId} Local 코어: 마커 불투명 픽셀 ` +
      `${before.opaqueMarkerPixels} -> ${localPmc.opaqueMarkerPixels} ` +
      `(-${removedPixels}, ${removedPercent.toFixed(1)}%)`
    );
    console.log(
      `     PMC ${localPmc.activeCategories.join(', ') || '표시 항목 없음'} / ` +
      `SCAV ${localScav.activeCategories.join(', ') || '표시 항목 없음'}`
    );
    console.log(
      `     Levels ${localPmc.levelCount}개, ${levelSwitch.targetLayer} 전환 ` +
      `${levelSwitch.beforeDisplay}->${levelSwitch.afterDisplay}, 위치/방향 유지, ` +
      'Online 선택/진영 전환 복원'
    );
    if (beforeScreenshot && localScreenshot) {
      console.log(`     화면 캡처: ${beforeScreenshot}`);
      console.log(`                 ${localScreenshot}`);
    }
  } catch (error) {
    failed++;
    console.error(`실패 ${mapId} Local 코어: ${error.message}`);
  }
} else {
  for (const mapId of MAPS) {
    blocked.clear();

    await send('Page.navigate', { url: `https://tarkov-market.com/maps/${mapId}` });
    await sleep(SETTLE);

    const state = JSON.parse(await evaluate(CHECK));
    const ok = !!state.baseMap && state.groups > 0 && state.markerLayer && state.canvas > 0;
    if (!ok) failed++;

    console.log(
      `${ok ? 'OK  ' : '실패'} ${mapId.padEnd(12)} 바닥맵 ${state.baseMap || '없음'} ` +
      `(그림 ${state.groups}겹), 마커층 ${state.markerLayer ? '있음' : '없음'}, ` +
      `pilot ${state.pilot}, 막은 요청 ${blocked.size}개`
    );

    if (!ok) for (const url of [...blocked].slice(0, 10)) console.log(`       막힘: ${url.slice(0, 110)}`);
  }
}

await send('Fetch.disable').catch(() => {});
ws.close();
chrome.kill();
await rm(profileDir, { recursive: true, force: true }).catch(() => {});

if (failed > 0) {
  console.error(LOCAL_CORE
    ? '\nLocal 코어 화면 검사를 통과하지 못했습니다.'
    : `\n맵 ${failed}개가 사본만으로 뜨지 않습니다.`);
  process.exitCode = 1;
} else if (LOCAL_CORE) {
  console.log('\nLocal 코어 화면 검사를 통과했습니다.');
} else {
  console.log(`\n맵 ${MAPS.length}개 모두 사본만으로 떴습니다.`);
}
