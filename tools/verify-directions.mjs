#!/usr/bin/env node
/**
 * verify-directions.mjs - 쿼터니언 방향 계산을 온라인 사이트의 마커 회전값과 대조한다
 *
 * 사본에서 옮긴 수식끼리 비교하면 같은 실수를 양쪽에 복제해도 통과한다. 별도 Chrome에서
 * 온라인 지도를 열고 북/동/남/서 쿼터니언을 게임 각도로 바꾼 뒤 window.pilot.position에
 * 넘긴다. 사이트가 마커에 쓴 CSS rotate 값과 로컬 화면각의 원형 각도 차이를 보고한다.
 *
 * 사용법:
 *   node tools/verify-directions.mjs
 *   node tools/verify-directions.mjs --map shoreline
 *
 * 기본 CDP 포트는 9232다. 실행 중인 앱에는 연결하지 않는다.
 */
import { spawn } from 'node:child_process';
import { createServer as createNetServer } from 'node:net';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {
  gameDirectionToMapDirection,
  parseScreenshotPosition,
} from '../viewer/coords.js';

const CDP_PORT = Number(process.env.VERIFY_DIRECTIONS_PORT || 9232);
if (!Number.isInteger(CDP_PORT) || CDP_PORT < 9230 || CDP_PORT > 65_535) {
  throw new Error('VERIFY_DIRECTIONS_PORT는 9230~65535 사이 정수여야 합니다.');
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

const mapId = argValue('--map', 'shoreline');
if (!/^[a-z0-9-]+$/.test(mapId)) throw new Error(`올바르지 않은 맵 ID입니다: ${mapId}`);
const meta = JSON.parse(await readFile(
  path.resolve('resources', 'maps', mapId, 'meta.json'),
  'utf8'
));
const siteUrl = meta.source?.page;
if (!/^https:\/\//.test(siteUrl || '')) throw new Error('meta.json에 온라인 source.page가 없습니다.');

const CHROME_CANDIDATES = [
  'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
  'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
  process.env.CHROME_PATH,
].filter(Boolean);
const chromePath = CHROME_CANDIDATES.find((candidate) => existsSync(candidate));
if (!chromePath) throw new Error('Chrome을 찾지 못했습니다. CHROME_PATH로 경로를 지정하세요.');

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

function yawQuaternion(degrees) {
  const halfRadians = degrees * (Math.PI / 360);
  return { x: 0, y: Math.sin(halfRadians), z: 0, w: Math.cos(halfRadians) };
}

function screenshotFilename(quaternion) {
  const look = [quaternion.x, quaternion.y, quaternion.z, quaternion.w]
    .map((value) => value.toFixed(12))
    .join(', ');
  return `2026-08-22[21-49]_0.00, 0.00, 0.00_${look}_0.00 (0).png`;
}

function angleDifference(left, right) {
  return Math.abs(((left - right + 540) % 360) - 180);
}

if (!await portAvailable(CDP_PORT)) {
  throw new Error(`CDP 포트 ${CDP_PORT}을 이미 다른 프로세스가 사용 중입니다.`);
}

const profilePath = await mkdtemp(path.join(os.tmpdir(), 'tanuki-direction-verify-'));
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
const killChrome = () => chrome.kill();
process.once('exit', killChrome);

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

const cases = [
  { name: '북', quaternion: yawQuaternion(0) },
  { name: '동', quaternion: yawQuaternion(90) },
  { name: '남', quaternion: yawQuaternion(180) },
  { name: '서', quaternion: yawQuaternion(270) },
];
let cdp = null;

try {
  const page = await connect();
  cdp = createSession(page);
  await cdp.ready;
  await cdp.send('Page.enable');
  await cdp.send('Page.navigate', { url: siteUrl });
  await waitUntil(
    cdp,
    `typeof window.pilot?.position === 'function' && !!document.querySelector('.map-wrap.inited')`
  );

  let failed = false;
  console.log(`${mapId}: transform.rotate ${meta.transform.rotate}°`);
  for (const testCase of cases) {
    const filename = screenshotFilename(testCase.quaternion);
    const gameDirection = parseScreenshotPosition(filename).look;
    const localDirection = gameDirectionToMapDirection(gameDirection, meta.transform);
    let siteState = null;
    // window.pilot 등록과 맵 컴포넌트의 위치 구독 사이에는 짧은 틈이 있다. 첫 사건을 놓쳐도
    // 마커가 생길 때까지만 같은 값을 다시 보내 검사가 준비 시점에 좌우되지 않게 한다.
    for (let attempt = 0; attempt < 20; attempt++) {
      await evaluate(
        cdp,
        `window.pilot.position(100, 200, 0, ${JSON.stringify(gameDirection)})`
      );
      await delay(100);
      siteState = JSON.parse(await evaluate(cdp, `JSON.stringify((() => {
        const marker = document.querySelector('.marker, .marker-arrow');
        const transform = marker?.style.transform || '';
        const match = transform.match(/rotate\\(([-+\\d.eE]+)deg\\)/);
        return { exists: !!marker, transform, degrees: match ? Number(match[1]) : null };
      })())`));
      if (siteState.exists && Number.isFinite(siteState.degrees)) break;
    }
    if (!siteState.exists || !Number.isFinite(siteState.degrees)) {
      throw new Error(
        `${testCase.name}: 사이트 마커의 CSS rotate 값을 읽지 못했습니다: ` +
        JSON.stringify(siteState)
      );
    }

    const difference = angleDifference(siteState.degrees, localDirection);
    console.log(
      `${testCase.name}: 게임 ${gameDirection.toFixed(6)}°, 사이트 ${siteState.degrees.toFixed(6)}°, ` +
      `로컬 ${localDirection.toFixed(6)}°, 각도 차이 ${difference.toFixed(6)}°`
    );
    if (difference > 0.0001) failed = true;
  }

  if (failed) throw new Error('온라인 사이트와 로컬 뷰어의 방향이 일치하지 않습니다.');
  console.log(`OK   ${mapId}: 네 방향의 최대 허용 오차 0.0001° 안에서 일치합니다.`);
} finally {
  cdp?.close();
  if (chrome.exitCode === null) {
    chrome.kill();
    await Promise.race([chromeExited, delay(3_000)]);
  }
  process.removeListener('exit', killChrome);
  await rm(profilePath, { recursive: true, force: true }).catch(() => {});
}
