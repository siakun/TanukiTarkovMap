/**
 * INTENT
 * 지도 데이터 수집(collect-map-docs.mjs)과 검사 도구(verify-viewer.mjs, verify-online.mjs)가 함께 쓰는
 * 헤드리스 Chrome 실행기다. 임시 프로필과 자동 포트로 띄워 실행 중인 앱의 CDP(9222)나 다른 도구의
 * 브라우저에 붙지 않는다. 사이트는 User-Agent에 HeadlessChrome이 있으면 Cloudflare 확인 화면에서
 * 멈추므로, 실제 사이트에 접속하는 도구는 realUserAgent로 그 표시를 지운다.
 * Node 22 내장 fetch/WebSocket만 사용한다. CHROME_PATH로 브라우저를 지정할 수 있다.
 */
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';

const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

export function findChrome() {
  const executable = [process.env.CHROME_PATH,
    'C:/Program Files/Google/Chrome/Application/chrome.exe',
    'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',
    'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
    '/usr/bin/google-chrome', '/usr/bin/chromium', '/usr/bin/chromium-browser',
  ].filter(Boolean).find(existsSync);
  if (!executable) throw new Error('Chrome을 찾지 못했습니다. CHROME_PATH에 Chromium 계열 브라우저 경로를 지정해 주세요.');
  return executable;
}

async function connect(url) {
  const socket = new WebSocket(url);
  await new Promise((resolve, reject) => {
    socket.addEventListener('open', resolve, { once: true });
    socket.addEventListener('error', () => reject(new Error(`CDP 연결 실패: ${url}`)), { once: true });
  });
  let nextId = 0;
  const pending = new Map();
  const listeners = new Map();
  socket.addEventListener('message', ({ data }) => {
    const message = JSON.parse(data);
    if (message.id && pending.has(message.id)) {
      const request = pending.get(message.id);
      pending.delete(message.id);
      clearTimeout(request.timer);
      message.error ? request.reject(new Error(message.error.message)) : request.resolve(message.result);
    } else if (message.method) {
      for (const listener of listeners.get(message.method) ?? []) listener(message.params);
    }
  });
  return {
    send(method, params = {}, timeout = 60000) {
      return new Promise((resolve, reject) => {
        const id = ++nextId;
        const timer = setTimeout(() => { pending.delete(id); reject(new Error(`CDP timeout: ${method}`)); }, timeout);
        pending.set(id, { resolve, reject, timer });
        socket.send(JSON.stringify({ id, method, params }));
      });
    },
    on(method, listener) { listeners.set(method, [...(listeners.get(method) ?? []), listener]); },
    close() { socket.close(); },
  };
}

/**
 * 헤드리스 Chrome을 띄우고 첫 탭의 CDP 세션을 돌려준다. 끝나면 close()로 브라우저와 임시 프로필을 정리한다.
 */
export async function launchChrome({ realUserAgent = false, width = 1280, height = 900 } = {}) {
  const profile = await mkdtemp(path.join(os.tmpdir(), 'tanuki-chrome-'));
  // 사이트는 자동화 표시(navigator.webdriver)가 켜진 브라우저에 지도 데이터 API를 내주지 않는다(실측:
  // 페이지와 캔버스는 뜨지만 마커와 퀘스트 상태가 끝내 비어 있었다). 실제 사이트에 접속할 때만 끈다.
  const automationFlags = realUserAgent ? ['--disable-blink-features=AutomationControlled'] : [];
  const browser = spawn(findChrome(), ['--headless=new', '--no-first-run', '--no-default-browser-check',
    '--remote-debugging-address=127.0.0.1', '--remote-debugging-port=0', `--user-data-dir=${profile}`,
    `--window-size=${width},${height}`, ...automationFlags, 'about:blank'], { stdio: 'ignore', windowsHide: true });
  let launchError = null;
  browser.on('error', (error) => { launchError = error; });

  let port = null;
  for (let attempt = 0; attempt < 150 && !port; attempt++) {
    if (launchError) throw launchError;
    try { port = Number((await readFile(path.join(profile, 'DevToolsActivePort'), 'utf8')).split('\n')[0]); }
    catch { await delay(100); }
  }
  if (!port) throw new Error('헤드리스 Chrome이 시작되지 않았습니다.');
  const targets = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
  const session = await connect(targets.find((target) => target.type === 'page').webSocketDebuggerUrl);
  await session.send('Page.enable');
  await session.send('Runtime.enable');
  if (realUserAgent) {
    const { userAgent } = await session.send('Browser.getVersion');
    await session.send('Emulation.setUserAgentOverride', { userAgent: userAgent.replace('HeadlessChrome', 'Chrome') });
  }

  async function evaluate(expression, timeout = 60000) {
    const result = await session.send('Runtime.evaluate',
      { expression, awaitPromise: true, returnByValue: true, timeout }, timeout + 5000);
    if (result.exceptionDetails) {
      throw new Error(result.exceptionDetails.exception?.description ?? result.exceptionDetails.text);
    }
    return result.result.value;
  }

  return {
    ...session,
    evaluate,
    async size(viewWidth, viewHeight) {
      await session.send('Emulation.setDeviceMetricsOverride',
        { width: viewWidth, height: viewHeight, deviceScaleFactor: 1, mobile: false });
    },
    async navigate(url, ready, timeout = 30000) {
      await session.send('Page.navigate', { url });
      const deadline = Date.now() + timeout;
      while (Date.now() < deadline) {
        await delay(200);
        try { if (await evaluate(ready)) return true; } catch { /* 탐색 중에는 이전 실행 컨텍스트가 사라진다 */ }
      }
      return false;
    },
    async frames(count = 3) {
      await evaluate(`new Promise(resolve => { let left = ${count}; const step = () => --left ? requestAnimationFrame(step) : resolve(); requestAnimationFrame(step); })`);
    },
    wheel(x, y, deltaY, modifiers = 0) {
      return session.send('Input.dispatchMouseEvent', { type: 'mouseWheel', x, y, deltaX: 0, deltaY, modifiers });
    },
    mouse(type, x, y, buttons = 1) {
      return session.send('Input.dispatchMouseEvent', { type, x, y, button: 'left', buttons, clickCount: 1 });
    },
    async screenshot(file) {
      const { data } = await session.send('Page.captureScreenshot', { format: 'png' });
      await writeFile(file, Buffer.from(data, 'base64'));
      return file;
    },
    async close() {
      session.close();
      browser.kill();
      for (let attempt = 0; browser.exitCode === null && attempt < 50; attempt++) await delay(100);
      await rm(profile, { recursive: true, force: true, maxRetries: 10, retryDelay: 100 }).catch(() => {});
    },
  };
}
