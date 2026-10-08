#!/usr/bin/env node
/**
 * INTENT
 * 앱과 게임 창을 열지 않고 실제 DOM에서 Online의 내 위치 표시를 검사한다.
 * Online은 사이트의 위치 입력 경로와 내 위치 그리기를 쓰지 않고, 앱이 파일명을 읽어 사이트 지도 위에 원과
 * 방향 삼각형을 직접 그린다. 사이트에서 빌리는 것은 지도 컨테이너와 좌표 변환뿐이므로, 재현 페이지도 그것만
 * 가진다. 지금 판의 사이트처럼 지도 상태를 렌더 트리에 두지 않고 Vue처럼 원본과 프록시를 WeakMap에 등록해
 * 만들며, 앱이 로드 시작에 넣는 map-state-capture.js가 그 등록으로 지도 상태를 기록하므로 기록 스크립트는
 * 지도 상태보다 먼저 넣는다. 지금 온라인 사이트와 맞는지는 tools/verify-online.mjs가 따로 확인한다.
 * Online과 Local이 같은 파일명을 같은 위치와 방향으로 읽는지도 여기서 viewer/coords.js와 대조한다.
 * 층은 사이트가 고른다. 재현 페이지는 사이트처럼 층 데이터 표를 등록하고 지도 문서가 늦게 도착하는 것을
 * 흉내 내며, playerPos의 칸이 바뀌면 층 데이터가 있을 때만 높이로 층을 고른다.
 * Chromium은 임시 프로필과 자동 할당 포트로 실행해 사용 중인 앱의 CDP에 연결하지 않는다.
 * 실행: node tools/verify-map-recovery.mjs [--scripts <폴더>]
 * --scripts는 주입 스크립트 폴더를 바꾼다. 수정 전 판으로 돌려 같은 사례가 실패하는지 비교할 때 쓴다.
 * Node 22+, CHROME_PATH로 브라우저 지정 가능.
 */
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { findChrome } from './headless-chrome.mjs';

const executable = findChrome();
const scriptsOption = process.argv.indexOf('--scripts');
const scripts = scriptsOption < 0
  ? new URL('../src/TanukiTarkovMap/Models/JavaScript/Scripts/', import.meta.url)
  : pathToFileURL(path.resolve(process.argv[scriptsOption + 1]) + path.sep);
const hostSources = new URL('../src/TanukiTarkovMap/Models/JavaScript/', import.meta.url);
const markers = await readFile(new URL('map-markers.js', scripts), 'utf8');
const bridge = await readFile(new URL('pilot-bridge.js', scripts), 'utf8');
const capture = await readFile(new URL('map-state-capture.js', scripts), 'utf8').catch(() => '');
const localCoords = await readFile(new URL('../viewer/coords.js', import.meta.url), 'utf8');
// 회전값은 y축 90도다. 재현 지도의 좌표 변환에서 화면 오른쪽(90도)을 향한다.
const filename = '2026-09-09[14-14]_179.10, 3.29, -717.65_0, 0.70710678, 0, 0.70710678_15.67 (0).png';
// 같은 방향으로 평면 y만 1 옮긴 다음 스크린샷이다. 재현 지도에서 화면 오른쪽으로 1px이다.
const nextFilename = filename.replace('179.10', '180.10');
// 같은 평면 위치의 지하층(높이 -10) 스크린샷이다. 재현 페이지의 층 데이터에서 basement(-1)에 든다.
const basementFilename = filename.replace('3.29', '-10.00');
const origin = 'https://tarkov-market.com';
const profile = await mkdtemp(path.join(os.tmpdir(), 'tanuki-recovery-'));
const browser = spawn(executable, ['--headless=new', '--disable-gpu', '--no-first-run',
  '--no-default-browser-check', '--remote-debugging-address=127.0.0.1',
  '--remote-debugging-port=0', `--user-data-dir=${profile}`, 'about:blank'],
{ stdio: 'ignore', windowsHide: true });
let launchError;
browser.on('error', error => { launchError = error; });
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
const sockets = [];
let passed = 0, failed = 0;

async function connect(url) {
  const socket = new WebSocket(url);
  sockets.push(socket);
  let nextId = 0;
  const pending = new Map();
  const handlers = new Map();
  socket.addEventListener('message', ({ data }) => {
    const message = JSON.parse(data);
    if (message.id) {
      const request = pending.get(message.id);
      if (!request) return;
      pending.delete(message.id);
      clearTimeout(request.timeout);
      message.error ? request.reject(new Error(message.error.message)) : request.resolve(message.result);
    } else handlers.get(message.method)?.(message.params);
  });
  await new Promise((resolve, reject) => {
    socket.addEventListener('open', resolve, { once: true });
    socket.addEventListener('error', reject, { once: true });
  });
  return {
    on: (event, handler) => handlers.set(event, handler),
    send(method, params = {}) {
      return new Promise((resolve, reject) => {
        const id = ++nextId;
        const timeout = setTimeout(() => { pending.delete(id); reject(new Error(`CDP timeout: ${method}`)); }, 10000);
        pending.set(id, { resolve, reject, timeout });
        socket.send(JSON.stringify({ id, method, params }));
      });
    },
  };
}

async function check(name, run) {
  try { await run(); passed++; console.log(`PASS ${name}`); }
  catch (error) { failed++; console.error(`FAIL ${name}: ${error.stack}`); }
}

try {
  let port;
  for (let attempt = 0; attempt < 100; attempt++) {
    if (launchError) throw launchError;
    if (browser.exitCode !== null) throw new Error(`Browser exited: ${browser.exitCode}`);
    try { port = Number((await readFile(path.join(profile, 'DevToolsActivePort'), 'utf8')).split('\n')[0]); break; }
    catch { await delay(100); }
  }
  assert.ok(port, 'Headless browser did not start');
  const targets = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
  const client = await connect(targets.find(target => target.type === 'page').webSocketDebuggerUrl);
  const evaluate = async expression => {
    const result = await client.send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true });
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.exception?.description ?? result.exceptionDetails.text);
    return result.result.value;
  };
  client.on('Fetch.requestPaused', ({ requestId }) => {
    client.send('Fetch.fulfillRequest', { requestId, responseCode: 200,
      responseHeaders: [{ name: 'content-type', value: 'text/html' }],
      body: Buffer.from('<!doctype html><html><head></head><body><div id="__nuxt"></div></body></html>').toString('base64'),
    }).catch(error => { console.error(error); failed++; });
  });
  await client.send('Page.enable');
  await client.send('Fetch.enable', { patterns: [{ urlPattern: '*' }] });

  async function load(map = 'lighthouse') {
    await client.send('Page.navigate', { url: `${origin}/maps/${map}` });
    for (let attempt = 0; attempt < 100; attempt++) {
      try { if (await evaluate(`location.pathname === '/maps/${map}' && document.readyState === 'complete'`)) return; }
      catch { /* 탐색 중에는 이전 실행 컨텍스트가 사라진다. */ }
      await delay(20);
    }
    throw new Error('Fixture navigation timed out');
  }
  const show = (name = filename) => evaluate(`window.tanukiPilot.showScreenshot(${JSON.stringify(name)})`);
  const rendered = (name = filename) => evaluate(`window.tanukiPilot.isRendered(${JSON.stringify(name)})`);
  const frames = (count = 3) => evaluate(`new Promise(resolve => { let left = ${count}; const step = () => --left ? requestAnimationFrame(step) : resolve(); requestAnimationFrame(step); })`);

  // 지금 판의 사이트 구조에서 앱이 빌리는 것만 재현한다. 지도 상태는 렌더 트리에 없고 Vue처럼 WeakMap에 원본과
  // 프록시를 등록해 만든다. 좌표 변환은 단순한 선형식이고, 화면 변환은 사이트처럼 panzoom 변환(x, y, zoom)과
  // 회전을 쓴다. 층 선택은 사이트처럼 playerPos의 칸이 바뀐 다음 마이크로태스크에 한 번, 층 데이터 표에 높이가
  // 채워져 있을 때만 높이로 고른다. 지도 문서가 늦게 도착하는 것은 fillLevels()로 흉내 낸다.
  // 핑은 내 위치 원의 애니메이션이므로 원에서 시작한 animate 호출을 센다.
  const site = `(() => {
    const container = document.createElement('div');
    container.className = 'pan map-cont';
    container.style.cssText = 'position:relative;width:400px;height:300px;overflow:hidden';
    document.body.append(container);
    const pickLevel = (raw) => {
      const levels = Object.values(window.levels ?? {});
      if (!levels.some(level => level.height?.length === 2)) return;
      const z = raw.playerPos.z, found = levels.find(level => level.height?.length === 2 && z >= level.height[0] && z < level.height[1]);
      if (found) raw.selectedLevel = found.num;
    };
    window.makeMap = (cont = container) => {
      let queued = false;
      const raw = { cont, x: 0, y: 0, zoom: 1, viewRotation: 0, selectedLevel: 1,
        panzoom: { moveBy() {} },
        gamePosToMapPos: (x, y) => ({ x: y, y: -x }),
        mapPosToScreenPos(x, y) {
          const r = raw.viewRotation * Math.PI / 180, rx = x * Math.cos(r) - y * Math.sin(r), ry = x * Math.sin(r) + y * Math.cos(r);
          return { x: raw.x + rx * raw.zoom, y: raw.y + ry * raw.zoom };
        },
        centerOnPosition(x, y) { const p = raw.mapPosToScreenPos(x, y); raw.x += 200 - p.x; raw.y += 150 - p.y; } };
      raw.playerPos = new Proxy({ x: 0, y: 0, z: 0, look: null }, { set(target, key, value) {
        if (target[key] !== value) {
          target[key] = value;
          if (!queued) { queued = true; queueMicrotask(() => { queued = false; pickLevel(raw); }); }
        }
        return true;
      } });
      const proxy = new Proxy(raw, {});
      new WeakMap().set(raw, proxy);
      return proxy;
    };
    // 사이트는 층 데이터 표의 프록시를 붙들고 있다. 재현 페이지도 붙들어야 약한 참조로 둔 기록이 사라지지 않는다
    window.levelProxies = [];
    window.makeLevels = (names = { level2: 2, main: 1, basement: -1 }) => {
      const raw = Object.fromEntries(Object.entries(names).map(([name, num]) => [name, { num, visible: [] }]));
      const proxy = new Proxy(raw, {});
      levelProxies.push(proxy);
      new WeakMap().set(raw, proxy);
      return raw;
    };
    window.fillLevels = () => {
      for (const [name, height] of Object.entries({ level2: [4, 20], main: [-0.5, 4], basement: [-20, -0.5] })) {
        if (window.levels[name]) window.levels[name].height = height;
      }
    };
    window.map = makeMap();
    window.pings = 0;
    const animate = Element.prototype.animate;
    Element.prototype.animate = function (...args) {
      if (this.classList.contains('tanuki-position')) pings++;
      return animate.apply(this, args);
    };
  })()`;
  // levels: 'filled'는 지도 문서가 이미 도착한 상태, 'empty'는 층 데이터 표만 있고 높이가 아직 없는 상태,
  // 'single'은 층이 하나인 맵, 'none'은 층 데이터 표를 기록하지 못한 판이다.
  async function setup({ levels = 'filled' } = {}) {
    await load();
    await evaluate(capture);
    await evaluate(site);
    if (levels !== 'none') await evaluate(levels === 'single' ? 'window.levels = makeLevels({ main: 1 })' : 'window.levels = makeLevels()');
    if (levels === 'filled') await evaluate('fillLevels()');
    await evaluate(markers);
    await evaluate(bridge);
  }

  // 표시 판정은 표시가 보고하는 값 대신 요소의 화면 위치로 한다. 핑이 원을 키우는 중이면 크기를 잴 수 없으므로
  // 애니메이션을 끝낸 뒤 잰다. 원 중심에서 삼각형 중심까지 17.25px, 원은 지름 20px에 2px 테두리다(viewer/style.css).
  const placement = `(() => {
    const circle = map.cont.querySelector('.tanuki-position');
    if (!circle) return null;
    circle.getAnimations().forEach(animation => animation.finish());
    const view = map.cont.getBoundingClientRect(), box = circle.getBoundingClientRect();
    const triangle = circle.querySelector('.tanuki-direction').getBoundingClientRect(), style = getComputedStyle(circle);
    const cx = box.left + box.width / 2, cy = box.top + box.height / 2;
    const dx = triangle.left + triangle.width / 2 - cx, dy = triangle.top + triangle.height / 2 - cy;
    const round = (value, scale = 100) => Math.round(value * scale) / scale;
    return { visible: circle.checkVisibility(), center: { x: round(cx - view.left), y: round(cy - view.top) },
      size: [box.width, box.height], fill: style.backgroundColor, border: style.borderTopWidth + ' ' + style.borderTopColor,
      angle: round(Math.atan2(dx, -dy) * 180 / Math.PI, 10), distance: round(Math.hypot(dx, dy)) };
  })()`;
  const markerVisible = 'document.querySelector(".tanuki-position")?.checkVisibility() === true';
  // 재현 지도의 이동은 소수 덧셈이라 끝자리 오차가 남는다. 좌표는 0.01px 단위로 비교한다
  const markerPoint = `(() => { const point = tanukiPilot.markerPoint();
    return point && { x: Math.round(point.x * 100) / 100, y: Math.round(point.y * 100) / 100 }; })()`;

  await check('position and direction are drawn from the filename without the site input', async () => {
    await setup();
    assert.equal(await show(), true);
    // 사이트의 내 위치 상태에는 층 선택에 쓸 좌표만 넣고 방향은 넣지 않는다
    assert.deepEqual(await evaluate('({ ...map.playerPos })'), { x: -717.65, y: 179.1, z: 3.29, look: null });
    await frames();
    // 파일명의 위치는 재현 지도의 화면 밖이므로 가운데로 옮겨 보인다
    assert.deepEqual(await evaluate(placement), { visible: true, center: { x: 200, y: 150 }, size: [20, 20],
      fill: 'rgb(138, 43, 226)', border: '2px rgb(112, 168, 0)', angle: 90, distance: 17.25 });
    assert.deepEqual(await evaluate(markerPoint), { x: 200, y: 150 });
  });
  await check('Online reads screenshots exactly like the Local minimap', async () => {
    await setup();
    const names = [filename,
      '2026-09-09[14-14]_-125.67, 5.64, -364.04_0.1, -0.2, 0.3, 0.9_1.00 (1).png',
      '2026-09-09[14-14]_1e2,2E-1,-3.5_0,1,0,0_2.png',
      '2026-09-09[14-14]_.5, 1., -0.25_0, 0, 0, 1_ (0).png',
      '2026-09-09[14-14]_1, 2, 3_1, 0, 0, 0_1.png',
      // 수평 시선이 없는 회전값
      '2026-09-09[14-14]_1, 2, 3_0.5, 0.5, 0.5, -0.5_1.png',
      // 메뉴와 은신처 스크린샷, 날짜 없는 이름, 잘린 이름
      '2026-09-09[14-14]_9.43 (0).png',
      '_179.10, 3.29, -717.65_0, 0.70710678, 0, 0.70710678_15.67 (0).png',
      '2026-09-09[14-14]_179.10, 3.29, -717.65_0, 0.70710678, 0, 0.70710678'];
    const pairs = await evaluate(`(async () => {
      const local = await import('data:text/javascript;base64,${Buffer.from(localCoords).toString('base64')}');
      return ${JSON.stringify(names)}.map(name => [tanukiPilot.parseScreenshot(name), local.parseScreenshot(name)]);
    })()`);
    for (const [index, [online, local]] of pairs.entries()) assert.deepEqual(online, local, names[index]);
    const first = pairs[0][0];
    assert.deepEqual([first.x, first.y, first.z, Math.round(first.look * 1000) / 1000], [-717.65, 179.1, 3.29, 90]);
    assert.equal(pairs.filter(([online]) => online === null).length, 4);
  });
  await check('marker follows panning and map rotation', async () => {
    await setup(); assert.equal(await show(), true);
    await frames();
    const before = await evaluate('tanukiMarker.pose()');
    await evaluate('map.x += 40; map.y -= 25');
    await frames();
    const panned = await evaluate('tanukiMarker.pose()');
    assert.deepEqual([panned.x - before.x, panned.y - before.y], [40, -25]);
    assert.equal((await evaluate(placement)).angle, 90);
    await evaluate('map.viewRotation = 90');
    await frames();
    assert.equal((await evaluate(placement)).angle, 180);
  });
  await check('resending the same screenshot does not ping, a new screenshot does', async () => {
    await setup();
    assert.equal(await show(), true);
    assert.equal(await evaluate('pings'), 1);
    assert.equal(await show(), true);
    assert.equal(await show(), true);
    assert.equal(await evaluate('pings'), 1);
    assert.equal(await show(nextFilename), true);
    assert.equal(await evaluate('pings'), 2);
  });
  await check('the periodic check confirms the shown position without moving the view', async () => {
    await setup(); assert.equal(await show(), true);
    assert.equal(await rendered(), true);
    assert.equal(await rendered(nextFilename), false);
    // 사용자가 지도를 끌어 마커가 화면 밖에 있어도 표시된 것이다. 점검이 화면을 되돌리지 않는다
    await evaluate('map.x += 1000');
    await frames();
    assert.equal(await rendered(), true);
    assert.deepEqual(await evaluate(markerPoint), { x: 1200, y: 150 });
    // 맞춤 스크립트가 부르는 revealPosition은 화면 밖 마커를 가운데로 옮긴다
    assert.equal(await evaluate('tanukiPilot.revealPosition()'), true);
    assert.deepEqual(await evaluate(markerPoint), { x: 200, y: 150 });
    // 새 위치가 화면 안쪽이면 사용자가 맞춘 화면을 그대로 둔다
    const camera = await evaluate('({ x: map.x, y: map.y })');
    assert.equal(await show(nextFilename), true);
    assert.deepEqual(await evaluate('({ x: map.x, y: map.y })'), camera);
  });
  await check('the site picks the level of the shown position once its level data arrives', async () => {
    // 앱은 맵을 열자마자 위치를 맡긴다. 그때 사이트의 층 데이터가 아직 없으면 사이트는 층을 고르지 않고,
    // 데이터가 나중에 와도 다시 고르지 않으므로 브리지가 데이터가 채워질 때까지 기다렸다가 좌표를 넘긴다.
    await setup({ levels: 'empty' });
    assert.equal(await show(basementFilename), true);
    await frames();
    assert.deepEqual(await evaluate('[map.selectedLevel, tanukiPilot.levelSync().state, map.playerPos.z]'), [1, 'waiting', 0]);
    await evaluate('fillLevels()');
    await delay(300);
    assert.deepEqual(await evaluate('[map.selectedLevel, tanukiPilot.levelSync().state, map.playerPos.z]'), [-1, 'synced', -10]);
  });
  await check('a resent screenshot keeps the level the user chose', async () => {
    await setup();
    assert.equal(await show(basementFilename), true);
    await frames();
    assert.equal(await evaluate('map.selectedLevel'), -1);
    // 사용자가 Levels 패널에서 다른 층을 고른 뒤 앱이 같은 스크린샷을 다시 맡긴다
    await evaluate('map.selectedLevel = 2');
    assert.equal(await show(basementFilename), true);
    await frames();
    assert.equal(await evaluate('map.selectedLevel'), 2);
    // 다른 높이의 새 스크린샷이면 사이트가 다시 고른다
    assert.equal(await show(), true);
    await frames();
    assert.equal(await evaluate('map.selectedLevel'), 1);
  });
  await check('single-level maps are left alone and missing level data does not hold the position', async () => {
    await setup({ levels: 'single' });
    assert.equal(await show(), true);
    assert.deepEqual(await evaluate('[tanukiPilot.levelSync().state, map.playerPos.x]'), ['single-level', 0]);
    await setup({ levels: 'none' });
    assert.equal(await show(), true);
    assert.deepEqual(await evaluate('[tanukiPilot.levelSync().state, map.playerPos.x]'), ['no-level-data', -717.65]);
  });
  await check('rebuilt site map hides the old position until it is handed over again', async () => {
    await setup(); assert.equal(await show(), true);
    await evaluate(`(() => { const next = map.cont.cloneNode(false); map.cont.replaceWith(next); window.map = makeMap(next); })()`);
    await frames();
    assert.equal(await rendered(), false);
    assert.equal(await show(), true);
    assert.equal(await evaluate('map.cont.querySelectorAll(".tanuki-position").length'), 1);
    assert.equal(await evaluate('document.querySelectorAll(".tanuki-position").length'), 1);
    // 같은 스크린샷을 다시 맡긴 것이므로 핑을 켜지 않는다
    assert.equal(await evaluate('pings'), 1);
  });
  await check('removed marker and replaced container element recover without a resend', async () => {
    await setup(); assert.equal(await show(), true);
    await evaluate(`document.querySelector('.tanuki-position').remove()`);
    await frames();
    assert.equal(await evaluate(markerVisible), true);
    // 사이트가 컨테이너 요소만 다시 만들면 지도 상태의 cont가 새 요소를 가리킨다
    await evaluate(`(() => { const next = map.cont.cloneNode(false); map.cont.replaceWith(next); map.cont = next; })()`);
    await frames();
    assert.equal(await evaluate('map.cont.querySelectorAll(".tanuki-position").length'), 1);
    assert.equal(await evaluate('document.querySelectorAll(".tanuki-position").length'), 1);
    assert.equal(await rendered(), true);
  });
  await check('leaving the map ends the shown position even if the same map comes back', async () => {
    await setup(); assert.equal(await show(), true);
    await evaluate(`history.pushState({}, '', '/maps/woods')`);
    await frames();
    assert.equal(await evaluate(markerVisible), false);
    assert.equal(await rendered(), false);
    await evaluate(`history.pushState({}, '', '/maps/lighthouse')`);
    await frames();
    assert.equal(await evaluate(markerVisible), false);
    assert.equal(await evaluate('tanukiMarker.ensure()'), true);
    assert.equal(await evaluate(markerVisible), false);
  });
  await check('a throwing site transform hides the marker and is reported by name', async () => {
    await setup(); assert.equal(await show(), true);
    await evaluate(`map.mapPosToScreenPos = () => { throw new Error('transform failed'); }`);
    await frames();
    assert.equal(await evaluate(markerVisible), false);
    assert.equal(await show(nextFilename), false);
    assert.equal(await evaluate('tanukiPilot.status()'), 'site-error: transform failed');
  });
  await check('capture record keeps WeakMap behavior and records only map states', async () => {
    await setup();
    assert.equal(await evaluate(`(() => {
      const table = new WeakMap(), key = {}, value = { gamePosToMapPos: 1 };
      return table.set(key, value) === table && table.get(key) === value && table.has(key);
    })()`), true);
    // 앱이 쓰는 칸(컨테이너와 두 좌표 변환)만으로 판정한다. 등록 순간에는 칸이 null이다
    assert.equal(await evaluate(`(() => {
      const states = () => window[Symbol.for('TanukiTarkovMap.mapStates')].states().length, before = states();
      for (const raw of [{ cont: null, gamePosToMapPos: null, mapPosToScreenPos: null },
        { playerPos: {}, gamePosToMapPos: null, mapPosToScreenPos: null }, { cont: null, gamePosToMapPos: null }]) {
        new WeakMap().set(raw, new Proxy(raw, {}));
      }
      return states() - before;
    })()`), 1);
    // 층 데이터 표는 층마다 번호(num)와 보일 층 목록(visible)을 가진 표의 프록시만 기록한다.
    // 같은 원본을 키로 쓰는 의존성 표(Map)와 모양이 다른 표는 기록하지 않는다
    assert.equal(await evaluate(`(() => {
      const sets = () => window[Symbol.for('TanukiTarkovMap.mapStates')].levelSets().length, before = sets();
      const levels = { level2: { num: 2, visible: [] }, main: { num: 1, visible: [] } };
      new WeakMap().set(levels, new Map());
      new WeakMap().set(levels, new Proxy(levels, {}));
      for (const raw of [{ main: { num: 1 } }, { main: { num: '1', visible: [] } }, {}, [{ num: 1, visible: [] }]]) {
        new WeakMap().set(raw, new Proxy(raw, {}));
      }
      return sets() - before;
    })()`), 1);
  });
  await check('missing or late capture is reported by name', async () => {
    await load(); await evaluate(site); await evaluate(markers); await evaluate(bridge);
    assert.equal(await show(), false);
    assert.equal(await evaluate('tanukiPilot.status()'), 'map-unavailable (capture-missing)');
    await evaluate(capture);
    assert.equal(await show(), false);
    assert.equal(await evaluate('tanukiPilot.status()'), 'map-unavailable (not-captured)');
  });
  await check('invalid filename and missing marker script are reported by name', async () => {
    await setup();
    assert.equal(await show('menu.png'), false);
    assert.equal(await evaluate('tanukiPilot.status()'), 'invalid-screenshot');
    await evaluate('tanukiMarker.dispose()');
    assert.equal(await show(), false);
    assert.equal(await evaluate('tanukiPilot.status()'), 'marker-unavailable');
  });
  await check('C# health probes accept the shipped JavaScript APIs', async () => {
    await setup();
    for (const [file, constant] of [['MapMarkers.js.cs', 'ENSURE_READY_SCRIPT'], ['PilotBridge.js.cs', 'IS_INSTALLED_SCRIPT']]) {
      const source = await readFile(new URL(file, hostSources), 'utf8');
      const declaration = source.match(new RegExp(`public const string ${constant} =([\\s\\S]*?);\\s*\\r?\\n`))?.[1];
      assert.ok(declaration, `Missing host probe: ${constant}`);
      const expression = [...declaration.matchAll(/"(?:[^"\\]|\\.)*"/g)].map(match => JSON.parse(match[0])).join('');
      assert.equal(await evaluate(expression), true, constant);
    }
  });
  await check('repeat injection and deleted public API keep one marker', async () => {
    await setup(); assert.equal(await show(), true);
    await evaluate('window.originalMarker = tanukiMarker; delete window.tanukiMarker');
    for (let i = 0; i < 5; i++) await evaluate(markers);
    assert.equal(await evaluate('originalMarker === tanukiMarker'), true);
    for (let i = 0; i < 3; i++) await evaluate(bridge);
    await frames();
    assert.equal(await evaluate('document.querySelectorAll(".tanuki-position").length'), 1);
    assert.equal(await rendered(), true);
  });
  await check('reload and history navigation accept reinjection', async () => {
    await setup();
    await load('woods'); await load('reserve');
    await evaluate('history.back()');
    for (let i = 0; i < 100 && await evaluate('location.pathname') !== '/maps/woods'; i++) await delay(20);
    assert.equal(await evaluate('location.pathname'), '/maps/woods');
    await evaluate(markers); await evaluate(bridge);
    assert.equal(await evaluate('tanukiMarker.ensure()'), true);
    await evaluate('history.forward()');
    for (let i = 0; i < 100 && await evaluate('location.pathname') !== '/maps/reserve'; i++) await delay(20);
    assert.equal(await evaluate('location.pathname'), '/maps/reserve');
    await evaluate(markers); await evaluate(bridge);
    assert.equal(await evaluate('tanukiMarker.ensure()'), true);
  });

} finally {
  for (const socket of sockets) socket.close();
  // 브라우저가 뜨지 못해 실패한 경우에도 종료를 끝없이 기다리지 않는다. 끝내 종료되지 않으면 프로필을 남긴다.
  if (browser.exitCode === null) browser.kill();
  for (let attempt = 0; browser.exitCode === null && attempt < 50; attempt++) await delay(100);
  const resolved = path.resolve(profile);
  if (browser.exitCode !== null && path.dirname(resolved) === path.resolve(os.tmpdir())
      && path.basename(resolved).startsWith('tanuki-recovery-')) {
    await rm(resolved, { recursive: true, force: true, maxRetries: 20, retryDelay: 100 });
  }
}
console.log(`${passed} passed, ${failed} failed`);
process.exitCode = failed ? 1 : 0;
