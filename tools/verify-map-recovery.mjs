#!/usr/bin/env node
/**
 * INTENT
 * 앱과 게임 창을 열지 않고 실제 DOM에서 위치 전달과 방향 복구를 검사한다.
 * 사이트의 이전/새 Pilot 계약은 작은 페이지로 재현해 사이트 접속 상태와 무관하게 검사한다.
 * Chromium은 임시 프로필과 자동 할당 포트로 실행해 사용 중인 앱의 CDP에 연결하지 않는다.
 * 실행: node tools/verify-map-recovery.mjs --archive archive
 * Node 22+, CHROME_PATH로 브라우저 지정 가능. --archive 생략 시 재현 페이지만 검사한다.
 */
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { mkdtemp, readFile, readdir, rm } from 'node:fs/promises';
import { once } from 'node:events';
import os from 'node:os';
import path from 'node:path';

const executable = [process.env.CHROME_PATH,
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  '/usr/bin/chromium', '/usr/bin/chromium-browser', '/usr/bin/google-chrome',
].filter(Boolean).find(existsSync);
assert.ok(executable, 'CHROME_PATH에 Chromium 브라우저 경로를 지정해야 합니다.');
const scripts = new URL('../src/TanukiTarkovMap/Models/JavaScript/Scripts/', import.meta.url);
const direction = await readFile(new URL('map-markers.js', scripts), 'utf8');
const bridge = await readFile(new URL('pilot-bridge.js', scripts), 'utf8');
const filename = '2026-09-09[14-14]_179.10, 3.29, -717.65_0, 0.70710678, 0, 0.70710678_15.67 (0).png';
const archiveArgument = process.argv.indexOf('--archive');
const archive = archiveArgument < 0 ? null : path.resolve(process.argv[archiveArgument + 1] ?? 'archive');
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
  const send = (name = filename) => evaluate(`window.tanukiPilot.sendScreenshot(${JSON.stringify(name)})`);
  async function setup() {
    await load();
    await evaluate(`(() => {
      const marker = document.createElement('div');
      marker.className = 'marker';
      marker.style.cssText = 'position:relative;width:20px;height:20px;transform:rotate(35deg)';
      document.body.append(marker);
      window.makeMap = () => ({wrap:document.body, playerPos:{x:0,y:0,z:0},
        gamePosToMapPos:(z,x)=>({x,y:-z}), mapPosToScreenPos:(x,y)=>({x,y})});
      window.map = makeMap();
      const root = document.getElementById('__nuxt');
      root._vnode = {component:{subTree:{props:{map}}}};
      window.update = (z,x,y) => {map.playerPos = {x:z,y:x,z:y};};
      root.__vue_app__ = {config:{globalProperties:{$nuxt:{$pilot:{positionUpdate:(...args)=>window.update(...args)}}}}};
    })()`);
    await evaluate(direction);
    await evaluate(bridge);
  }

  await check('late service and map mount, ignored position, retry', async () => {
    await load(); await evaluate(direction); await evaluate(bridge);
    assert.equal(await send(), false);
    assert.equal(await evaluate('tanukiPilot.status()'), 'pilot-unavailable');
    await setup();
    await evaluate('window.originalUpdate = update; window.update = () => {}');
    assert.equal(await send(), false);
    assert.equal(await evaluate('tanukiPilot.status()'), 'position-not-rendered');
    await evaluate('window.update = originalUpdate');
    assert.equal(await send(), true);
    assert.deepEqual(await evaluate('map.playerPos'), { x: -717.65, y: 179.1, z: 3.29 });
  });
  await check('C# health probes accept the shipped JavaScript APIs', async () => {
    await setup();
    for (const [file, constant] of [['MapMarkers.js.cs', 'ENSURE_READY_SCRIPT'], ['PilotBridge.js.cs', 'IS_INSTALLED_SCRIPT']]) {
      const source = await readFile(new URL(`../${file}`, scripts), 'utf8');
      const declaration = source.match(new RegExp(`public const string ${constant} =([\\s\\S]*?);\\s*\\r?\\n`))?.[1];
      assert.ok(declaration, `Missing host probe: ${constant}`);
      const expression = [...declaration.matchAll(/"(?:[^"\\]|\\.)*"/g)].map(match => JSON.parse(match[0])).join('');
      assert.equal(await evaluate(expression), true, constant);
    }
  });
  await check('legacy Pilot contract and invalid filename', async () => {
    await setup();
    await evaluate(`delete document.getElementById('__nuxt').__vue_app__;
      window.pilot = {positionFromScreenshot: () => {update(-717.65,179.1,3.29)}};`);
    assert.equal(await send(), true);
    assert.equal(await send('menu.png'), false);
    assert.equal(await evaluate('tanukiPilot.status()'), 'invalid-screenshot');
  });
  await check('repeat injection and deleted public API keep one observer', async () => {
    await setup(); assert.equal(await send(), true);
    await evaluate('window.originalDirection = tanukiDirection; delete window.tanukiDirection');
    for (let i = 0; i < 5; i++) await evaluate(direction);
    assert.equal(await evaluate('originalDirection === tanukiDirection'), true);
    assert.equal(await evaluate('document.querySelectorAll(".triangle-indicator").length'), 1);
    assert.equal(await evaluate('document.querySelectorAll("#triangle-indicator-style").length'), 1);
  });
  await check('deleted arrow, stylesheet and replaced body recover', async () => {
    await setup(); assert.equal(await send(), true);
    await evaluate(`document.querySelector('.triangle-indicator').remove(); document.getElementById('triangle-indicator-style').remove();`);
    assert.equal(await evaluate('!!document.querySelector(".triangle-indicator") && !!document.getElementById("triangle-indicator-style")'), true);
    await evaluate(`document.body.replaceWith(Object.assign(document.createElement('body'), {innerHTML:'<div class="marker"></div>'}));`);
    assert.equal(await evaluate('document.querySelectorAll(".triangle-indicator").length'), 1);
  });
  await check('emptied or disabled stylesheet recovers', async () => {
    await setup(); assert.equal(await send(), true);
    await evaluate(`document.getElementById('triangle-indicator-style').textContent = ''; tanukiDirection.ensure();`);
    assert.equal(await evaluate(`getComputedStyle(document.querySelector('.triangle-indicator')).width`), '25px');
    await evaluate(`document.getElementById('triangle-indicator-style').sheet.deleteRule(0); tanukiDirection.ensure();`);
    assert.equal(await evaluate(`getComputedStyle(document.querySelector('.triangle-indicator')).width`), '25px');
    await evaluate(`document.getElementById('triangle-indicator-style').disabled = true; tanukiDirection.ensure();`);
    assert.equal(await evaluate(`getComputedStyle(document.querySelector('.triangle-indicator')).width`), '25px');
  });
  await check('same route map replacement uses active Vue tree', async () => {
    await setup(); assert.equal(await send(), true);
    assert.equal(await evaluate(`tanukiPilot.isRendered(${JSON.stringify(filename)})`), true);
    await evaluate(`window.map = makeMap(); document.getElementById('__nuxt')._vnode.component.subTree = {props:{map}};`);
    assert.equal(await evaluate(`tanukiPilot.isRendered(${JSON.stringify(filename)})`), false);
    assert.equal(await send(filename.replace('179.10', '180.10')), true);
    assert.equal(await evaluate('map.playerPos.y'), 180.1);
  });
  await check('response after SPA navigation cannot set new map heading', async () => {
    await setup();
    await evaluate(`window.headingCalls = 0; tanukiDirection.setHeading = () => {headingCalls++;return true;};
      window.update = (...args) => new Promise(resolve => {window.finish = () => {originalUpdate(...args);resolve()};});
      window.originalUpdate = (z,x,y) => {map.playerPos = {x:z,y:x,z:y}};
      window.result = tanukiPilot.sendScreenshot(${JSON.stringify(filename)});
      history.pushState({}, '', '/maps/woods'); finish();`);
    assert.equal(await evaluate('window.result'), false);
    assert.equal(await evaluate('headingCalls'), 0);
  });
  await check('out-of-order screenshot responses cannot replace latest heading', async () => {
    await setup();
    await evaluate(`window.headingCalls = 0; tanukiDirection.setHeading = () => {headingCalls++;return true;};
      window.resolvers = []; window.update = (z,x,y) => new Promise(resolve => {
        resolvers.push(() => {map.playerPos={x:z,y:x,z:y};resolve();});
      }); window.first = tanukiPilot.sendScreenshot(${JSON.stringify(filename)});
      window.second = tanukiPilot.sendScreenshot(${JSON.stringify(filename.replace('179.10', '180.10'))});
      resolvers[1]();`);
    assert.equal(await evaluate('window.second'), true);
    await evaluate('resolvers[0]()');
    assert.equal(await evaluate('window.first'), false);
    assert.equal(await evaluate('headingCalls'), 1);
    // 외부 서비스가 좌표를 뒤늦게 덮어쓰더라도 다음 상태 확인에서 재전달 대상으로 잡는다.
    assert.equal(await evaluate(`tanukiPilot.isRendered(${JSON.stringify(filename.replace('179.10', '180.10'))})`), false);
  });
  await check('parent transform and compact arrow spacing are preserved', async () => {
    await setup(); assert.equal(await send(), true);
    const pose = await evaluate(`(() => {const arrow=document.querySelector('.triangle-indicator');
      return {parent:arrow.parentElement.style.transform, heading:parseFloat(arrow.style.getPropertyValue('--tanuki-direction')),
        width:getComputedStyle(arrow).width, radius:arrow.style.getPropertyValue('--tanuki-marker-radius')};})()`);
    assert.equal(pose.parent, 'rotate(35deg)'); assert.ok(Math.abs(pose.heading - 55) < 0.001);
    assert.equal(pose.width, '25px'); assert.equal(pose.radius, '10px');
  });
  await check('reload and history navigation accept reinjection', async () => {
    await setup();
    await load('woods'); await load('reserve');
    await evaluate('history.back()');
    for (let i = 0; i < 100 && await evaluate('location.pathname') !== '/maps/woods'; i++) await delay(20);
    assert.equal(await evaluate('location.pathname'), '/maps/woods');
    await evaluate(direction); await evaluate(bridge);
    assert.equal(await evaluate('tanukiDirection.ensure()'), true);
    await evaluate('history.forward()');
    for (let i = 0; i < 100 && await evaluate('location.pathname') !== '/maps/reserve'; i++) await delay(20);
    assert.equal(await evaluate('location.pathname'), '/maps/reserve');
    await evaluate(direction); await evaluate(bridge);
    assert.equal(await evaluate('tanukiDirection.ensure()'), true);
  });

  if (archive) {
    const index = new Map();
    for (const file of await readdir(path.join(archive, 'maps'))) {
      if (!file.endsWith('.json')) continue;
      for (const [url, entry] of Object.entries(JSON.parse(await readFile(path.join(archive, 'maps', file), 'utf8'))))
        if (!index.has(url)) index.set(url, entry);
    }
    assert.ok(index.size, 'Archive must contain map responses');
    // Online 역할의 임시 프로필에 데이터를 남긴다. Local용 컨텍스트를 없애도 보존되어야 한다.
    await evaluate(`localStorage.setItem('tanuki-regression-profile', 'online');
      new Promise((resolve,reject) => {const request=indexedDB.open('tanuki-regression-profile');
        request.onsuccess=()=>{request.result.close();resolve(true)}; request.onerror=()=>reject(request.error);})`);
    const versionInfo = await (await fetch(`http://127.0.0.1:${port}/json/version`)).json();
    const controller = await connect(versionInfo.webSocketDebuggerUrl);
    for (const map of ['lighthouse', 'reserve']) {
      await check(`isolated archived ${map}: storage and position with network blocked`, async () => {
        const { browserContextId } = await controller.send('Target.createBrowserContext');
        try {
          const { targetId } = await controller.send('Target.createTarget', { url: 'about:blank', browserContextId });
          const pages = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
          const local = await connect(pages.find(page => page.id === targetId).webSocketDebuggerUrl);
          const inspect = async expression => {
            const result = await local.send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true });
            if (result.exceptionDetails) throw new Error(result.exceptionDetails.exception?.description ?? result.exceptionDetails.text);
            return result.result.value;
          };
          local.on('Fetch.requestPaused', async ({ requestId, request }) => {
            try {
              const entry = index.get(request.url) ?? index.get(request.url.split('?')[0]);
              await local.send('Fetch.fulfillRequest', { requestId, responseCode: entry?.status || (entry ? 200 : 404),
                responseHeaders: [{ name: 'content-type', value: entry?.mime || 'text/plain' }],
                body: entry ? (await readFile(path.join(archive, 'blobs', entry.blob))).toString('base64') : '',
              });
            } catch (error) { console.error(error); failed++; }
          });
          await local.send('Page.enable');
          await local.send('Fetch.enable', { patterns: [{ urlPattern: '*' }] });
          await local.send('Page.navigate', { url: `${origin}/maps/${map}` });
          let rendered = false;
          for (let attempt = 0; attempt < 60; attempt++) {
            try {
              await inspect(direction); await inspect(bridge);
              rendered = await inspect(`window.tanukiPilot.sendScreenshot(${JSON.stringify(filename)})`);
              if (rendered) break;
            } catch { /* 문서와 지도 서비스가 준비되면 재시도한다. */ }
            await delay(200);
          }
          assert.equal(rendered, true, `Archived ${map} did not render position`);
          assert.equal(await inspect(`tanukiPilot.isRendered(${JSON.stringify(filename)})`), true);
          assert.equal(await inspect(`localStorage.getItem('tanuki-regression-profile')`), null);
          assert.equal(await inspect(`indexedDB.databases().then(dbs=>dbs.some(db=>db.name==='tanuki-regression-profile'))`), false);
        } finally { await controller.send('Target.disposeBrowserContext', { browserContextId }); }
        assert.equal(await evaluate(`localStorage.getItem('tanuki-regression-profile')`), 'online');
        assert.equal(await evaluate(`indexedDB.databases().then(dbs=>dbs.some(db=>db.name==='tanuki-regression-profile'))`), true);
      });
    }
  }
} finally {
  for (const socket of sockets) socket.close();
  if (browser.exitCode === null) { const closed = once(browser, 'exit'); browser.kill(); await closed; }
  assert.equal(path.dirname(path.resolve(profile)), path.resolve(os.tmpdir()));
  assert.ok(path.basename(profile).startsWith('tanuki-recovery-'));
  await rm(profile, { recursive: true, force: true, maxRetries: 20, retryDelay: 100 });
}
console.log(`${passed} passed, ${failed} failed`);
process.exitCode = failed ? 1 : 0;
