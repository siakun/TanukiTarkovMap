#!/usr/bin/env node
/**
 * INTENT
 * 지금 온라인 사이트에서 앱의 Online 경로가 동작하는지 확인하는 진단 도구다. 사이트가 배포로 위치
 * 입력 경로나 지도 구조를 바꾸면 여기서 먼저 깨진다. "스크린샷을 찍어도 위치가 안 바뀐다"거나
 * "확대하면 지도가 사라진다"는 보고를 받으면 앱을 실행하기 전에 이 도구부터 돌린다.
 *
 * 앱(WebBrowserViewModel.OnFrameLoadEnd)이 페이지 로드 뒤 하는 일을 같은 파일, 같은 순서로 한다.
 * 불필요한 요소 제거와 여백 제거, 방향 표시와 위치 브리지 설치, 마지막 스크린샷 전송, 지도 맞춤,
 * UI 숨김 순서다. 위치를 맞춤보다 먼저 보내는 순서가 중요하다. 맞춤이 끝난 뒤에도 마커가 화면
 * 안에 남는지가 여기서 갈린다. 그다음 맵마다 확인한다.
 * - 위치: 맞춤이 끝난 뒤 사이트 마커가 화면 안에 보이고 방향 삼각형이 붙는지, 어떤 입력
 *   경로(tanukiPilot.input())를 썼는지, 실패하면 tanukiPilot.status()의 사유
 * - 맞춤: map-keep-visible.js가 지형을 무엇으로 쟀고 화면을 얼마나 채웠는지. 최소 배율에서도
 *   지형이 창보다 크면 채움을 맞출 수 없으므로, 그때는 사이트 배율이 meta.json의 minZoom인지 본다
 * - 휠: 지형 바깥에서 확대한 뒤에도 실제 지형 캔버스 픽셀이 화면 가운데를 덮는지
 * 네트워크가 필요하고 사이트의 봇 확인에 막힐 수 있어 CI에서는 돌리지 않는다.
 * Usage: node tools/verify-online.mjs [--maps customs,lab] [--artifacts dir]
 */
import { mkdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { launchChrome } from './headless-chrome.mjs';

const args = process.argv.slice(2);
const option = (name, fallback) => (args.includes(name) ? args[args.indexOf(name) + 1] : fallback);
const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const scripts = path.join(repository, 'src/TanukiTarkovMap/Models/JavaScript/Scripts');
const manifest = JSON.parse(await readFile(path.join(repository, 'resources/manifest.json'), 'utf8'));
const maps = option('--maps', '') ? option('--maps').split(',') : manifest.maps;
const artifacts = option('--artifacts', null);
if (artifacts) await mkdir(artifacts, { recursive: true });
const source = async (file) => readFile(path.join(scripts, file), 'utf8');
const injected = {
  cleanup: await source('ui-customization.js'),
  layout: await source('page-layout.js'),
  direction: await source('map-markers.js'),
  bridge: await source('pilot-bridge.js'),
  keepVisible: await source('map-keep-visible.js'),
  ui: await source('web-elements-control.js'),
};

// 첫 추출구 자리(지형 위)를 게임 좌표로 되돌려 파일명을 만든다. 사이트 파일명은 y, 높이, x 순서다.
function filenameFor(mapId) {
  const meta = JSON.parse(awaitRead(`resources/maps/${mapId}/meta.json`));
  const markers = JSON.parse(awaitRead(`resources/maps/${mapId}/markers.json`));
  const point = markers.markers[0]?.position ?? { x: meta.size.width / 2, y: meta.size.height / 2 };
  const t = meta.transform;
  let x = (t.xOffset - point.x) / t.ratio, y = (t.yOffset - point.y) / t.ratio;
  if (t.rotate) {
    const r = t.rotate * Math.PI / 180;
    [x, y] = [x * Math.cos(r) - y * Math.sin(r), x * Math.sin(r) + y * Math.cos(r)];
  }
  return `2026-01-01[00-00]_${y.toFixed(2)}, 0.00, ${x.toFixed(2)}_0, 0.25882, 0, 0.96593_1.00 (0).png`;
}
const files = new Map();
for (const mapId of maps) {
  for (const name of ['meta', 'markers']) {
    files.set(`resources/maps/${mapId}/${name}.json`, await readFile(path.join(repository, `resources/maps/${mapId}/${name}.json`), 'utf8'));
  }
}
const awaitRead = (name) => files.get(name);

// 지도 캔버스의 실제 픽셀로 지형이 화면 가운데를 덮는지 본다. 스크립트의 측정값과 독립된 판정이다.
const terrainAtCenter = `(() => {
  const view = document.querySelector('.pan.map-cont').getBoundingClientRect();
  const canvas = [...document.querySelectorAll('canvas.doc-map-canvas')].find(c => c.getBoundingClientRect().width > 0);
  if (!canvas) return { canvas: false };
  const box = canvas.getBoundingClientRect(), data = canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height).data;
  let left = Infinity, top = Infinity, right = -Infinity, bottom = -Infinity;
  for (let y = 0; y < canvas.height; y += 2) for (let x = 0; x < canvas.width; x += 2) {
    if (!data[(y * canvas.width + x) * 4 + 3]) continue;
    const px = box.left + x * box.width / canvas.width, py = box.top + y * box.height / canvas.height;
    left = Math.min(left, px); right = Math.max(right, px); top = Math.min(top, py); bottom = Math.max(bottom, py);
  }
  const cx = view.left + view.width / 2, cy = view.top + view.height / 2;
  return { canvas: true, covers: left <= cx && right >= cx && top <= cy && bottom >= cy };
})()`;

const browser = await launchChrome({ realUserAgent: true, width: 1280, height: 900 });
let failed = 0;
try {
  await browser.size(420, 360);
  for (const mapId of maps) {
    const problems = [];
    const opened = await browser.navigate(`https://tarkov-market.com/maps/${mapId}`,
      `location.pathname === '/maps/${mapId}' && !!document.querySelector('.map-wrap') && !!document.querySelector('canvas.doc-map-canvas')`, 45000);
    if (!opened) {
      failed++;
      console.log(`FAIL ${mapId}: map page did not open (bot check or page layout change)`);
      continue;
    }
    await browser.evaluate(injected.cleanup).catch(() => {});
    await browser.evaluate(injected.layout).catch(() => {});
    await browser.evaluate(injected.direction);
    await browser.evaluate(injected.bridge);
    const filename = filenameFor(mapId);
    const send = () => browser.evaluate(`window.tanukiPilot.sendScreenshot(${JSON.stringify(filename)})`).catch((error) => `error: ${error.message}`);
    let sent = await send();
    await browser.evaluate(injected.keepVisible);
    await browser.evaluate(injected.ui);
    await browser.evaluate('window.hideHeader(); window.hideFooter(); window.hidePanelLeft(); window.hidePanelRight(); window.hidePanelTop(); true');
    // 맞춤(600ms 뒤 시작)과 그 뒤 세 번의 재확인(1.2초 간격)이 끝날 때까지 기다린다. 첫 전송이
    // 실패했으면 앱의 위치 유지 타이머처럼 2초마다 다시 보낸다
    for (let waited = 0; waited < 7000; waited += 2000) {
      await new Promise((resolve) => setTimeout(resolve, 2000));
      if (sent !== true) sent = await send();
    }

    const position = await browser.evaluate(`(() => {
      const marker = document.querySelector('.marker, .marker-arrow'), box = marker?.getBoundingClientRect();
      const arrow = marker?.querySelector('.triangle-indicator');
      return { input: window.tanukiPilot.input(), status: window.tanukiPilot.status(),
        marker: !!marker && marker.checkVisibility(), inside: !!box && box.left >= 0 && box.top >= 0 && box.right <= innerWidth && box.bottom <= innerHeight,
        arrow: !!arrow && arrow.checkVisibility() };
    })()`);
    if (sent !== true) problems.push(`position not shown (${position.status})`);
    if (!position.marker || !position.inside) problems.push('marker not visible inside the view');
    if (!position.arrow) problems.push('direction arrow missing');

    const fit = await browser.evaluate(`(() => { const log = window.__tanukiKeepVisible.log();
      const last = log.filter(entry => entry['단계'] === 'verify' || entry['단계'] === 'fit').at(-1) ?? {};
      return { source: window.__tanukiKeepVisible.picture()?.source ?? null, fill: last['채움'] ?? null,
        scale: window.tanukiPilot.getMap()?.panzoom?.getTransform?.().scale ?? null }; })()`);
    const minZoom = JSON.parse(awaitRead(`resources/maps/${mapId}/meta.json`)).minZoom;
    const atMinZoom = Math.abs(fit.scale - minZoom) <= minZoom * 0.01;
    if (!fit.source) problems.push('terrain not measured (fit and clamp idle)');
    else if (!(fit.fill >= 0.75 && fit.fill <= 0.98) && !(fit.fill > 0.98 && atMinZoom)) {
      problems.push(`fit fill ${fit.fill} at zoom ${fit.scale}`);
    }

    for (let step = 0; step < 5; step++) { await browser.wheel(415, 20, -100); await browser.frames(4); }
    const wheel = await browser.evaluate(terrainAtCenter);
    if (!wheel.canvas) problems.push('terrain canvas missing');
    else if (!wheel.covers) problems.push('terrain left the view center after wheel zoom');

    if (artifacts) await browser.screenshot(path.join(artifacts, `online-${mapId}.png`));
    if (problems.length) failed++;
    console.log(`${problems.length ? 'FAIL' : 'PASS'} ${mapId}: input=${position.input}, fit=${fit.source}/${fit.fill}`
      + (atMinZoom && fit.fill > 0.98 ? ' (min zoom)' : '')
      + (problems.length ? ` -> ${problems.join('; ')}` : ''));
    // 실패하면 맞춤 과정을 함께 남긴다. 맞춤이 어디서 멈췄는지, 마커를 보이게 옮겼는지 여기서 읽힌다
    if (problems.length) {
      const log = await browser.evaluate('window.__tanukiKeepVisible.log().slice(-10)').catch(() => []);
      for (const entry of log) console.log(`    ${JSON.stringify(entry)}`);
    }
  }
} finally {
  await browser.close();
}
console.log(failed ? `${failed} map(s) failed` : 'all maps passed');
process.exitCode = failed ? 1 : 0;
