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
 *
 * 결과: 맵 하나라도 실패하면 종료 코드 1로 끝난다. 사본을 다시 만든 뒤에는 이 검사를 통과해야
 * 배포에 넣는다.
 *
 * 주의: 디버깅 포트는 9225를 쓴다. 9222는 실행 중인 앱, 9223은 재현용 브라우저,
 * 9224는 수집 도구 자리다. 같은 포트를 쓰면 명령이 엉뚱한 브라우저로 흘러간다.
 *
 * 수집 도구(archive-maps.mjs)와 브라우저 실행 부분이 겹치지만 각자 둔다. 사본을 만드는 도구와
 * 검사하는 도구가 같은 코드를 공유하면, 그 코드가 틀렸을 때 두 쪽이 같이 틀린 채로 통과한다.
 */
import { readFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { spawn } from 'node:child_process';
import { rm, mkdtemp } from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';

const PORT = Number(process.env.VERIFY_PORT || 9225);

// MapConfiguration.cs의 맵 ID와 같은 순서로 둔다
const ALL_MAPS = [
  'ground-zero', 'factory', 'customs', 'interchange', 'woods', 'shoreline',
  'reserve', 'lighthouse', 'streets', 'lab', 'labyrinth', 'icebreaker',
];

const args = process.argv.slice(2);
const argValue = (name, fallback) => {
  const index = args.indexOf(name);
  return index >= 0 && args[index + 1] ? args[index + 1] : fallback;
};

const ARCHIVE = path.resolve(argValue('--archive', path.join(process.cwd(), 'archive')));
const MAPS = argValue('--maps', '').trim()
  ? argValue('--maps', '').split(',').map((m) => m.trim()).filter(Boolean)
  : ALL_MAPS;

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

for (const mapId of MAPS) {
  blocked.clear();

  await send('Page.navigate', { url: `https://tarkov-market.com/maps/${mapId}` });
  await sleep(SETTLE);

  const state = JSON.parse(
    (await send('Runtime.evaluate', { expression: CHECK, returnByValue: true, awaitPromise: true })).result.value
  );

  const ok = !!state.baseMap && state.groups > 0 && state.markerLayer && state.canvas > 0;
  if (!ok) failed++;

  console.log(
    `${ok ? 'OK  ' : '실패'} ${mapId.padEnd(12)} 바닥맵 ${state.baseMap || '없음'} (그림 ${state.groups}겹), ` +
    `마커층 ${state.markerLayer ? '있음' : '없음'}, pilot ${state.pilot}, 막은 요청 ${blocked.size}개`
  );

  if (!ok) for (const url of [...blocked].slice(0, 10)) console.log(`       막힘: ${url.slice(0, 110)}`);
}

await send('Fetch.disable').catch(() => {});
ws.close();
chrome.kill();
await rm(profileDir, { recursive: true, force: true }).catch(() => {});

if (failed > 0) {
  console.error(`\n맵 ${failed}개가 사본만으로 뜨지 않습니다.`);
  process.exitCode = 1;
} else {
  console.log(`\n맵 ${MAPS.length}개 모두 사본만으로 떴습니다.`);
}
