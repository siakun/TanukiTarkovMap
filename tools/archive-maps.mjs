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
 * 주의: 디버깅 포트는 9224를 쓴다. 9222는 실행 중인 앱, 9223은 재현용 브라우저 자리다.
 */
import { spawn } from 'node:child_process';
import { mkdir, writeFile, rm, stat } from 'node:fs/promises';
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
await cdp.send('Network.enable');
await cdp.send('Network.setCacheDisabled', { cacheDisabled: true });

await mkdir(path.join(OUT, 'blobs'), { recursive: true });
await mkdir(path.join(OUT, 'maps'), { recursive: true });

const manifest = { site: SITE, createdAt: new Date().toISOString(), maps: {} };
const blobSizes = new Map();

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

    saves.push(
      cdp
        .send('Network.getResponseBody', { requestId: params.requestId })
        .then(async (body) => {
          const buffer = Buffer.from(body.body, body.base64Encoded ? 'base64' : 'utf8');
          const hash = crypto.createHash('sha1').update(buffer).digest('hex');
          const blobPath = path.join(OUT, 'blobs', hash);

          if (!blobSizes.has(hash)) {
            await writeFile(blobPath, buffer);
            blobSizes.set(hash, buffer.length);
          }

          index[info.url] = { blob: hash, mime: info.mime, status: info.status };
        })
        .catch(() => {})
    );
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

const uniqueBytes = [...blobSizes.values()].reduce((a, b) => a + b, 0);
manifest.totalBytes = uniqueBytes;
await writeFile(path.join(OUT, 'manifest.json'), JSON.stringify(manifest, null, 1), 'utf8');

console.log(`\n저장 위치: ${OUT}`);
console.log(`중복 제거 후 전체 크기: ${(uniqueBytes / 1024 / 1024).toFixed(1)}MB (blob ${blobSizes.size}개)`);

cdp.close();
chrome.kill();
await rm(profileDir, { recursive: true, force: true }).catch(() => {});
