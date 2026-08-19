#!/usr/bin/env node
/**
 * archive-maps.mjs - tarkov-market 맵 페이지를 오프라인 사본으로 저장한다
 *
 * 왜 도구로 만드는가: 사본을 사람이 손으로 모으면 사이트가 바뀔 때마다 낡고, 무엇이 빠졌는지
 * 알 방법이 없다. 한 번의 명령으로 다시 만들 수 있어야 갱신이 유지된다.
 *
 * 무엇을 저장하는가: 맵 페이지를 실제 브라우저로 열고 그 페이지가 받은 모든 응답을 저장한다.
 * 앱은 오프라인일 때 이 응답을 그대로 돌려주므로, 사이트 코드가 무엇을 필요로 하는지 우리가
 * 알아낼 필요가 없다.
 *
 * 저장 구조: 같은 파일이 맵마다 반복되지 않도록 내용 해시로 blobs에 한 번만 두고,
 * 맵별 index.json이 주소에서 그 해시를 가리킨다. JS 번들과 폰트가 12개 맵에 공통이라
 * 이 구분이 없으면 사본이 열 배로 불어난다.
 *
 *   archive/
 *     blobs/<sha1>            응답 본문
 *     maps/<맵ID>.json        { url: { blob, mime, status } }
 *     manifest.json           만든 시각, 맵 목록, 크기
 *
 * 사용법:
 *   node tools/archive-maps.mjs                     모든 맵
 *   node tools/archive-maps.mjs --maps lab,customs  일부만
 *   node tools/archive-maps.mjs --out D:\archive    저장 위치 지정
 *
 * 본문을 두 경로로 받는다: 브라우저가 들고 있는 응답 본문을 CDP로 달라고 하고, 주지 못하면
 * 같은 주소를 직접 한 번 더 받는다. 미리 읽기(preload/prefetch)로 받은 조각은 CDP가 본문을
 * 주지 않는데, 맵의 지형 svg가 바로 그 조각에 들어 있다. 이것을 빈 채로 담으면 앱은 200에 빈
 * 본문을 돌려주고 사이트는 지형만 빠진 채로 뜬다 (2026-08-18에 겪은 증상).
 *
 * css가 가리키는 자원도 채운다. 브라우저는 그 규칙이 실제로 쓰일 때만 폰트와 배경을 받아 오므로,
 * 맵 화면에서 안 쓰이는 이탤릭 폰트나 한국어 서브셋은 페이지를 열어도 요청되지 않는다. 사본에
 * 그대로 빠지면 오프라인에서 그 글자만 다른 글꼴로 나온다. 그래서 마지막에 css와 html의
 * url()과 @import를 훑어 빠진 것을 직접 받아 담는다.
 *
 * 주의: 디버깅 포트는 9224를 쓴다. 9222는 실행 중인 앱, 9223은 재현용 브라우저 자리다.
 */
import { spawn } from 'node:child_process';
import { mkdir, writeFile, readFile, rm, stat } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';

const PORT = Number(process.env.ARCHIVE_PORT || 9224);
const SITE = 'https://tarkov-market.com';

// MapConfiguration.cs의 맵 ID와 같은 순서로 둔다. 새 맵이 생기면 양쪽을 함께 고친다
const ALL_MAPS = [
  'ground-zero', 'factory', 'customs', 'interchange', 'woods', 'shoreline',
  'reserve', 'lighthouse', 'streets', 'lab', 'labyrinth', 'icebreaker',
];

// 사본에 담지 않을 요청. 추적기는 오프라인에서 실패해도 페이지에 영향이 없고,
// 담아 두면 앱이 켜질 때마다 옛 추적 요청을 되돌려주는 셈이 된다
const SKIP_HOSTS = ['google-analytics.com', 'googletagmanager.com', 'google.com', 'doubleclick.net'];

// 저장할 이유가 없는 주소. 봇 확인은 한 번 쓰고 버리는 값이라 사본에 담아도 쓸모가 없다
const SKIP_PATHS = ['/cdn-cgi/challenge-platform/'];

const args = process.argv.slice(2);
const argValue = (name, fallback) => {
  const index = args.indexOf(name);
  return index >= 0 && args[index + 1] ? args[index + 1] : fallback;
};

const OUT = path.resolve(argValue('--out', path.join(process.cwd(), 'archive')));
const MAPS = argValue('--maps', '').trim()
  ? argValue('--maps', '').split(',').map((m) => m.trim()).filter(Boolean)
  : ALL_MAPS;

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

function session(page) {
  const ws = new WebSocket(page.webSocketDebuggerUrl);
  let nextId = 1;
  const pending = new Map();
  const listeners = new Map();

  ws.addEventListener('message', (message) => {
    const data = JSON.parse(message.data);
    if (data.id && pending.has(data.id)) {
      const { resolve, reject } = pending.get(data.id);
      pending.delete(data.id);
      data.error ? reject(new Error(data.error.message)) : resolve(data.result);
    } else if (data.method) {
      (listeners.get(data.method) || []).forEach((fn) => fn(data.params));
    }
  });

  return {
    ready: new Promise((r) => ws.addEventListener('open', r)),
    send: (method, params = {}) =>
      new Promise((resolve, reject) => {
        const id = nextId++;
        pending.set(id, { resolve, reject });
        ws.send(JSON.stringify({ id, method, params }));
      }),
    on: (event, fn) => listeners.set(event, [...(listeners.get(event) || []), fn]),
    close: () => ws.close(),
  };
}

const profileDir = path.join(os.tmpdir(), `tanuki-archive-${process.pid}`);
const chrome = spawn(chromePath, [
  '--headless=new',
  '--disable-gpu',
  `--remote-debugging-port=${PORT}`,
  `--user-data-dir=${profileDir}`,
  // 앱 창과 비슷한 폭으로 연다. 사이트가 폭에 따라 다른 화면을 그리므로 사본도 그 화면이어야 한다
  '--window-size=1280,1000',
  'about:blank',
], { stdio: 'ignore' });

process.on('exit', () => chrome.kill());

const page = await connect();
const cdp = session(page);
await cdp.ready;
await cdp.send('Page.enable');
// 응답 본문은 브라우저가 버퍼에 들고 있다가 우리가 달라고 할 때 준다. 기본 버퍼는 10MB쯤이라
// 맵 조각(200KB 넘는 js가 여러 개)이 밀려나면 본문이 빈 채로 온다. 사본에 빈 응답이 담기면
// 앱은 200에 빈 본문을 돌려주고 사이트는 조용히 망가진다(지형이 안 그려지던 원인).
await cdp.send('Network.enable', { maxTotalBufferSize: 512 * 1024 * 1024, maxResourceBufferSize: 128 * 1024 * 1024 });
await cdp.send('Network.setCacheDisabled', { cacheDisabled: true });

await mkdir(path.join(OUT, 'blobs'), { recursive: true });
await mkdir(path.join(OUT, 'maps'), { recursive: true });

const manifest = { site: SITE, createdAt: new Date().toISOString(), maps: {} };
const blobSizes = new Map();

// 본문을 못 받은 주소. 하나라도 있으면 사본이 불완전하므로 끝에 알리고 실패로 끝낸다
const emptyBodies = [];

// 직접 받을 때 쓰는 신원. 사이트가 브라우저를 구분하므로 같은 값으로 맞춘다
const USER_AGENT =
  'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36';

/**
 * 브라우저가 본문을 주지 않는 응답을 같은 주소로 직접 받아 온다.
 * 정적 자원이라 쿠키 없이도 같은 내용이 온다
 */
async function fetchBody(url, referer) {
  // 맵 열두 개를 잇달아 여는 동안 연결이 한 번씩 끊긴다. 한 번은 다시 시도한다
  for (let attempt = 0; attempt < 2; attempt++) {
    try {
      const res = await fetch(url, { headers: { 'user-agent': USER_AGENT, referer } });
      if (!res.ok) return null;

      // 서버가 정말 빈 본문을 주는 파일도 있다. 그것과 "못 받았다"를 가르려고 상태를 함께 돌려준다
      return { body: Buffer.from(await res.arrayBuffer()) };
    } catch (error) {
      if (attempt === 1) throw error;
      await sleep(500);
    }
  }

  return null;
}

/**
 * 응답 하나를 사본에 담는다. 본문이 비면 직접 받아 채우고, 그래도 비면 담지 않고 보고한다
 */
async function saveResponse(requestId, info, referer, index) {
  let buffer = null;

  try {
    const body = await cdp.send('Network.getResponseBody', { requestId });
    buffer = Buffer.from(body.body, body.base64Encoded ? 'base64' : 'utf8');
  } catch {
    buffer = null;
  }

  if (!buffer || buffer.length === 0) {
    const fetched = await fetchBody(info.url, referer).catch(() => null);

    // 직접 받아도 못 받으면 사본이 불완전한 것이다. 받았는데 비어 있으면 원래 빈 파일이므로 그대로 담는다
    if (fetched === null) {
      if (info.status !== 204 && info.status !== 304) emptyBodies.push(info.url);
      return;
    }

    buffer = fetched.body;
  }

  const hash = crypto.createHash('sha1').update(buffer).digest('hex');
  const blobPath = path.join(OUT, 'blobs', hash);

  if (!blobSizes.has(hash)) {
    await writeFile(blobPath, buffer);
    blobSizes.set(hash, buffer.length);
  }

  index[info.url] = { blob: hash, mime: info.mime, status: info.status };
}

for (const mapId of MAPS) {
  const url = `${SITE}/maps/${mapId}`;
  const index = {};
  const meta = new Map();
  const saves = [];

  const onResponse = (params) => {
    meta.set(params.requestId, {
      url: params.response.url,
      mime: params.response.mimeType,
      status: params.response.status,
    });
  };

  const onFinished = (params) => {
    const info = meta.get(params.requestId);
    if (!info) return;
    if (SKIP_HOSTS.some((host) => info.url.includes(host))) return;
    if (SKIP_PATHS.some((part) => info.url.includes(part))) return;

    saves.push(saveResponse(params.requestId, info, url, index));
  };

  cdp.on('Network.responseReceived', onResponse);
  cdp.on('Network.loadingFinished', onFinished);

  process.stdout.write(`${mapId} ... `);
  await cdp.send('Page.navigate', { url });
  await sleep(12000);
  await Promise.all(saves);

  const bytes = Object.values(index).reduce((sum, entry) => sum + (blobSizes.get(entry.blob) || 0), 0);
  await writeFile(path.join(OUT, 'maps', `${mapId}.json`), JSON.stringify(index, null, 1), 'utf8');
  manifest.maps[mapId] = { url, responses: Object.keys(index).length, bytes };

  console.log(`응답 ${Object.keys(index).length}개, ${(bytes / 1024 / 1024).toFixed(1)}MB`);
}

// --- css가 가리키는 자원 채우기 ---
// 색인은 맵마다 따로 쓰지만 이 자원들은 어느 맵에서나 같으므로 모든 색인에 함께 넣는다.
// 앱은 색인을 전부 합쳐 쓰므로 한 곳에만 넣어도 되지만, 맵별 색인이 그 맵을 여는 데 필요한
// 것을 모두 담는다는 성질을 깨지 않는다
const REFERENCE_PATTERN = /url\(\s*['"]?([^'")]+)['"]?\s*\)|@import\s+(?:url\(\s*)?['"]([^'"]+)['"]/g;

const mapIndexes = new Map();
for (const mapId of MAPS) {
  const file = path.join(OUT, 'maps', `${mapId}.json`);
  mapIndexes.set(mapId, JSON.parse(await readFile(file, 'utf8')));
}

const known = new Set();
for (const index of mapIndexes.values()) for (const url of Object.keys(index)) known.add(url);

const wanted = new Map();
for (const index of mapIndexes.values()) {
  for (const [url, entry] of Object.entries(index)) {
    const mime = String(entry.mime || '');
    if (!mime.includes('css') && !mime.includes('html')) continue;

    const text = await readFile(path.join(OUT, 'blobs', entry.blob), 'utf8');

    for (const match of text.matchAll(REFERENCE_PATTERN)) {
      const raw = (match[1] || match[2] || '').trim();
      if (!raw || raw.startsWith('data:') || raw.startsWith('#') || raw.startsWith('blob:')) continue;

      let absolute;
      try { absolute = new URL(raw, url).toString(); } catch { continue; }
      if (known.has(absolute) || wanted.has(absolute)) continue;

      wanted.set(absolute, url);
    }
  }
}

const addedAssets = {};
for (const [assetUrl, fromUrl] of wanted) {
  let fetched = null;
  try {
    const res = await fetch(assetUrl, { headers: { 'user-agent': USER_AGENT, referer: fromUrl } });
    if (res.ok) {
      fetched = {
        body: Buffer.from(await res.arrayBuffer()),
        mime: (res.headers.get('content-type') || 'application/octet-stream').split(';')[0].trim(),
      };
    }
  } catch {
    fetched = null;
  }

  if (!fetched || fetched.body.length === 0) {
    emptyBodies.push(assetUrl);
    continue;
  }

  const hash = crypto.createHash('sha1').update(fetched.body).digest('hex');
  if (!blobSizes.has(hash)) {
    await writeFile(path.join(OUT, 'blobs', hash), fetched.body);
    blobSizes.set(hash, fetched.body.length);
  }

  addedAssets[assetUrl] = { blob: hash, mime: fetched.mime, status: 200 };
}

if (Object.keys(addedAssets).length > 0) {
  for (const [mapId, index] of mapIndexes) {
    Object.assign(index, addedAssets);
    await writeFile(path.join(OUT, 'maps', `${mapId}.json`), JSON.stringify(index, null, 1), 'utf8');
    manifest.maps[mapId].responses = Object.keys(index).length;
  }

  console.log(`css가 가리키는 자원 ${Object.keys(addedAssets).length}개를 더 받아 담았습니다`);
}

const uniqueBytes = [...blobSizes.values()].reduce((a, b) => a + b, 0);
manifest.totalBytes = uniqueBytes;
await writeFile(path.join(OUT, 'manifest.json'), JSON.stringify(manifest, null, 1), 'utf8');

console.log(`\n저장 위치: ${OUT}`);
console.log(`중복 제거 후 전체 크기: ${(uniqueBytes / 1024 / 1024).toFixed(1)}MB (blob ${blobSizes.size}개)`);

if (emptyBodies.length > 0) {
  const unique = [...new Set(emptyBodies)];
  console.error(`\n본문을 받지 못한 응답 ${unique.length}개. 사본이 불완전하다.`);
  for (const url of unique.slice(0, 20)) console.error(`  ${url}`);
  if (unique.length > 20) console.error(`  ... 외 ${unique.length - 20}개`);
  process.exitCode = 1;
}

cdp.close();
chrome.kill();
await rm(profileDir, { recursive: true, force: true }).catch(() => {});
