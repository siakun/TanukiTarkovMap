#!/usr/bin/env node
/**
 * verify-coordinates.mjs - 온라인 사이트와 로컬 뷰어의 게임 좌표 변환값을 대조한다
 *
 * bundle 수식을 읽어 옮긴 것만으로는 같은 오해를 양쪽에 복제할 수 있다. 사이트가 페이지에
 * 공개한 window.pilot.position에 게임 좌표를 넘기고, 실제 마커의 left/top과 로컬 변환값을
 * 숫자로 비교한다. 이 검사는 네트워크가 필요하지만 앱은 실행하거나 연결하지 않는다.
 *
 * 사용법:
 *   node tools/verify-coordinates.mjs
 *   node tools/verify-coordinates.mjs --map shoreline --x 100 --y 200
 *
 * 기본 CDP 포트는 9233이다. 9230~9232는 추출, 리소스 검사와 방향 검사 자리다.
 */
import { spawn } from 'node:child_process';
import { createServer as createNetServer } from 'node:net';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { gamePositionToMapPosition } from '../viewer/coords.js';

const CDP_PORT = Number(process.env.VERIFY_COORDINATES_PORT || 9233);
if (!Number.isInteger(CDP_PORT) || CDP_PORT < 9230 || CDP_PORT > 65_535) {
  throw new Error('VERIFY_COORDINATES_PORT는 9230~65535 사이 정수여야 합니다.');
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

const mapId = argValue('--map', 'shoreline');
if (!/^[a-z0-9-]+$/.test(mapId)) throw new Error(`올바르지 않은 맵 ID입니다: ${mapId}`);
const gameX = Number(argValue('--x', '100'));
const gameY = Number(argValue('--y', '200'));
if (!Number.isFinite(gameX) || !Number.isFinite(gameY)) {
  throw new Error('--x와 --y에는 숫자를 지정해야 합니다.');
}

const metaPath = path.resolve('resources', 'maps', mapId, 'meta.json');
const meta = JSON.parse(await readFile(metaPath, 'utf8'));
const localPosition = gamePositionToMapPosition(gameX, gameY, meta.transform);
const siteUrl = meta.source?.page;
if (!/^https:\/\//.test(siteUrl || '')) throw new Error('meta.json에 온라인 source.page가 없습니다.');

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

const profilePath = await mkdtemp(path.join(os.tmpdir(), 'tanuki-coordinate-verify-'));
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

async function waitUntil(cdp, expression, timeout = 35_000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    if (await evaluate(cdp, expression)) return;
    await delay(250);
  }
  throw new Error(`온라인 지도 준비를 ${timeout}ms 안에 확인하지 못했습니다.`);
}

let cdp = null;

try {
  const page = await connect();
  cdp = createSession(page);
  await cdp.ready;
  await cdp.send('Page.enable');
  await cdp.send('Page.navigate', { url: siteUrl });
  await waitUntil(
    cdp,
    `typeof window.pilot?.position === 'function' && !!document.querySelector('.map-wrap.inited svg.svg-map')`
  );

  // 루트의 window.pilot과 맵 컴포넌트의 위치 구독은 서로 다른 마운트 단계에서 준비된다.
  // 둘 사이의 짧은 틈에 한 번만 보내면 사건을 놓치므로 마커가 생길 때까지만 같은 좌표를 다시 보낸다.
  for (let attempt = 0; attempt < 20; attempt++) {
    await evaluate(
      cdp,
      `window.pilot.position(${JSON.stringify(gameX)}, ${JSON.stringify(gameY)}, 0, null)`
    );
    if (await evaluate(cdp, `!!document.querySelector('.marker, .marker-arrow')`)) break;
    await delay(250);
  }
  if (!await evaluate(cdp, `!!document.querySelector('.marker, .marker-arrow')`)) {
    throw new Error('온라인 사이트가 위치 마커를 만들지 않았습니다.');
  }

  const sitePosition = JSON.parse(await evaluate(cdp, `JSON.stringify((() => {
    const marker = document.querySelector('.marker, .marker-arrow');
    const style = getComputedStyle(marker);
    return {
      x: Number.parseFloat(marker.style.left || style.left),
      y: Number.parseFloat(marker.style.top || style.top),
      markerClass: marker.className,
    };
  })())`));

  const xDifference = Math.abs(sitePosition.x - localPosition.x);
  const yDifference = Math.abs(sitePosition.y - localPosition.y);
  console.log(`게임 좌표: X ${gameX}, Y ${gameY}`);
  console.log(`온라인 사이트 맵 좌표: X ${sitePosition.x}, Y ${sitePosition.y}`);
  console.log(`로컬 뷰어 맵 좌표: X ${localPosition.x}, Y ${localPosition.y}`);
  console.log(`차이: X ${xDifference}, Y ${yDifference}`);

  if (xDifference > 0.0001 || yDifference > 0.0001) {
    throw new Error('온라인 사이트와 로컬 뷰어의 좌표가 일치하지 않습니다.');
  }
  console.log(`OK   ${mapId}: 온라인 사이트와 로컬 뷰어의 좌표가 일치합니다.`);
} finally {
  cdp?.close();
  if (chrome.exitCode === null) {
    chrome.kill();
    await Promise.race([chromeExited, delay(3_000)]);
  }
  await rm(profilePath, { recursive: true, force: true }).catch(() => {});
}
