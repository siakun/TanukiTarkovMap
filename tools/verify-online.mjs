#!/usr/bin/env node
/**
 * INTENT
 * 지금 온라인 사이트에서 앱의 Online 경로가 동작하는지 확인하는 진단 도구다. 사이트가 배포로 지도 구조나
 * 좌표 변환을 바꾸면 여기서 먼저 깨진다. "스크린샷을 찍어도 위치가 안 바뀐다"거나 "확대하면 지도가
 * 사라진다"는 보고를 받으면 앱을 실행하기 전에 이 도구부터 돌린다.
 *
 * 앱(WebBrowserViewModel)이 하는 일을 같은 파일, 같은 순서로 한다. 로드 시작에 지도 상태 기록을 넣고
 * (앱은 FrameLoadStart, 여기서는 새 문서마다 먼저 실행되는 스크립트로 넣는다), 로드가 끝나면 불필요한
 * 요소 제거와 여백 제거, 내 위치 표시와 위치 브리지 설치, 마지막 스크린샷 표시, 지도 맞춤, UI 숨김 순서다.
 * 위치를 맞춤보다 먼저 보내는 순서가 중요하다. 맞춤이 끝난 뒤에도 마커가 화면 안에 남는지가 여기서
 * 갈린다. 그다음 맵마다 확인한다.
 * - 위치: 맞춤이 끝난 뒤 앱이 그린 원이 화면 안에 있고 사이트의 좌표 변환으로 구한 자리에 있는지.
 *   사이트가 같은 파일명으로 그리는 자기 원("Where am i" 입력)과도 겹치는지 비교한다. 그 입력은 검사만 쓰고
 *   앱은 쓰지 않으며, 사이트가 입력을 없애면 그 비교만 건너뛴다. 실패하면 tanukiPilot.status()의 사유
 * - 방향: 삼각형이 원에서 벗어난 방향이 사이트가 방향 화살표를 그리는 식(시선 + 270 - 지도 회전 + 화면
 *   회전)과 같은지. 앱은 이 식을 쓰지 않고 시선 벡터를 투영하므로 서로 다른 경로의 대조다
 * - 층: 층이 여럿인 맵은 기본 층이 아닌 층의 위치를 맵을 열자마자 보내고, 사이트가 그 층으로 바꿨는지.
 *   기대 층은 사이트가 지금 가진 층 데이터에 Local의 층 판정 식(viewer/coords.js)을 적용해 구하고, 앱에 담긴
 *   리소스로 구한 층과 다르면 리소스가 사이트보다 뒤처진 것으로 알린다
 * - 재전송과 핑: 처음 표시할 때 핑이 켜지는지, 표시된 뒤 앱의 2초 위치 점검이 같은 위치를 다시 보내지
 *   않는지, 같은 파일을 다시 보내도 핑이 다시 켜지지 않는지
 * - 맞춤: map-keep-visible.js가 지형을 무엇으로 쟀고 화면을 얼마나 채웠는지. 최소 배율에서도
 *   지형이 창보다 크면 채움을 맞출 수 없으므로, 그때는 사이트 배율이 meta.json의 minZoom인지 본다
 * - 휠: 지형 바깥에서 확대한 뒤에도 실제 지형 캔버스 픽셀이 화면 가운데를 덮는지
 * - 회전: 사이트의 지도 회전 뒤에도 삼각형이 같은 식의 방향을 가리키는지
 * 네트워크가 필요하고 사이트의 봇 확인에 막힐 수 있어 CI에서는 돌리지 않는다.
 * Usage: node tools/verify-online.mjs [--maps customs,lab] [--artifacts dir]
 */
import { mkdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { levelAtPosition } from '../viewer/coords.js';
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
  capture: await source('map-state-capture.js'),
  cleanup: await source('ui-customization.js'),
  layout: await source('page-layout.js'),
  markers: await source('map-markers.js'),
  bridge: await source('pilot-bridge.js'),
  keepVisible: await source('map-keep-visible.js'),
  ui: await source('web-elements-control.js'),
};

// 파일명의 회전값(y축 30도)이다. 삼각형 각도는 이 값을 사이트의 화살표 식에 넣은 값과 비교한다.
const LOOK = 30;

const round2 = (value) => Math.round(value * 100) / 100;

// 첫 추출구 자리(지형 위)를 게임 평면 좌표로 되돌린다
function basePoint(meta, markers) {
  const point = markers.markers[0]?.position ?? { x: meta.size.width / 2, y: meta.size.height / 2 };
  const t = meta.transform;
  let x = (t.xOffset - point.x) / t.ratio, y = (t.yOffset - point.y) / t.ratio;
  if (t.rotate) {
    const r = t.rotate * Math.PI / 180;
    [x, y] = [x * Math.cos(r) - y * Math.sin(r), x * Math.sin(r) + y * Math.cos(r)];
  }
  return { x: round2(x), y: round2(y), z: 0 };
}

// 층이 여럿인 맵에서 기본 층이 아닌 층에 드는 위치를 고른다. 높이 범위를 가진 층은 첫 추출구 자리에서 그 범위의
// 가운데 높이를 쓰고, 구역만 가진 층은 구역 안의 격자점과 그 구역의 가운데 높이를 쓴다. 파일명에 적는 소수 둘째
// 자리로 판정해 반올림 때문에 다른 층으로 넘어가지 않게 한다.
function levelTarget(meta, base) {
  const levels = [...meta.levels].sort((a, b) => b.sourceLevel - a.sourceLevel);
  const defaultLevel = (meta.levels.find((level) => level.defaultVisible) ?? levels[0])?.sourceLevel;
  const fits = (level, x, y, z) => levelAtPosition(levels, x, y, z)?.sourceLevel === level.sourceLevel;
  if (levels.length < 2) return null;
  for (const level of levels) {
    if (level.sourceLevel === defaultLevel) continue;
    if (level.height?.length === 2) {
      const z = round2((level.height[0] + level.height[1]) / 2);
      if (fits(level, base.x, base.y, z)) return { x: base.x, y: base.y, z, level: level.sourceLevel };
    }
    for (const zone of level.zones ?? []) {
      const points = zone.rect ?? zone.poly;
      if (!points || zone.height?.length !== 2) continue;
      const z = round2((zone.height[0] + zone.height[1]) / 2);
      const xs = points.map((p) => p[0]), ys = points.map((p) => p[1]);
      for (let i = 1; i < 10; i++) for (let j = 1; j < 10; j++) {
        const x = round2(Math.min(...xs) + (Math.max(...xs) - Math.min(...xs)) * i / 10);
        const y = round2(Math.min(...ys) + (Math.max(...ys) - Math.min(...ys)) * j / 10);
        if (fits(level, x, y, z)) return { x, y, z, level: level.sourceLevel };
      }
    }
  }
  return null;
}

// 사이트 파일명은 y, 높이, x 순서다. 회전값은 y축 30도다
const filenameAt = ({ x, y, z }) => `2026-01-01[00-00]_${y.toFixed(2)}, ${z.toFixed(2)}, ${x.toFixed(2)}_0, 0.25882, 0, 0.96593_1.00 (0).png`;
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

// 앱이 그린 원과 삼각형을 요소의 화면 위치로 확인한다. 원 중심은 브리지가 구한 자리(markerPoint)와,
// 삼각형 방향은 사이트가 방향 화살표를 그리는 식과 비교한다. 핑이 원을 키우는 중이어도 중심은 그대로다.
const markerCheck = `(() => {
  const map = window.tanukiPilot.getMap(), point = window.tanukiPilot.markerPoint();
  const container = map?.cont, view = container?.getBoundingClientRect();
  const result = { status: window.tanukiPilot.status(), point,
    inside: !!point && !!view && point.x >= 0 && point.y >= 0 && point.x <= view.width && point.y <= view.height };
  const circle = container?.querySelector('.tanuki-position'), triangle = circle?.querySelector('.tanuki-direction');
  result.shown = !!circle && circle.checkVisibility() && window.tanukiMarker?.pose?.().shown === true;
  if (result.shown && view && point) {
    const box = circle.getBoundingClientRect(), tip = triangle.getBoundingClientRect();
    const cx = box.left + box.width / 2, cy = box.top + box.height / 2;
    result.offset = Math.hypot(cx - (view.left + container.clientLeft + point.x), cy - (view.top + container.clientTop + point.y));
    const dx = tip.left + tip.width / 2 - cx, dy = tip.top + tip.height / 2 - cy;
    result.angle = Math.atan2(dx, -dy) * 180 / Math.PI;
    result.distance = Math.hypot(dx, dy);
    result.expectedAngle = ${LOOK} + 270 - (map.transform?.rotate ?? 0) + (map.viewRotation ?? 0);
    result.circle = { x: cx - view.left, y: cy - view.top };
  }
  return result;
})()`;

// 사이트가 같은 파일명을 받아 그리는 자기 원의 중심. 검사만 "Where am i" 입력 처리기를 불러 사이트에 그리게 하고,
// 내 위치 캔버스에서 칠해진 픽셀의 중심을 잰 뒤 표시를 다시 끈다. 처리기가 없으면 비교하지 않는다.
const siteCircle = (filename) => `(async () => {
  const nodes = [document.getElementById('__nuxt')?._vnode], seen = new Set();
  let panel = null;
  for (let i = 0; i < nodes.length && !panel; i++) {
    const node = nodes[i];
    if (!node || typeof node !== 'object' || seen.has(node)) continue;
    seen.add(node);
    if (typeof node.props?.onScreenPositionChange === 'function' && node.props.playerData
      && 'isPlayerMarkerVisible' in node.props.playerData) panel = node.props;
    nodes.push(node.component?.subTree, node.suspense?.activeBranch);
    if (Array.isArray(node.children)) nodes.push(...node.children);
  }
  if (!panel) return { available: false };
  panel.playerData.isPlayerMarkerVisible = true;
  panel.onScreenPositionChange({ target: { value: ${JSON.stringify(filename)} } });
  await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
  const container = window.tanukiPilot.getMap().cont, canvas = container.querySelector('canvas.players-canvas');
  const box = canvas.getBoundingClientRect(), view = container.getBoundingClientRect();
  const data = canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height).data;
  let sumX = 0, sumY = 0, count = 0;
  for (let y = 0; y < canvas.height; y++) for (let x = 0; x < canvas.width; x++) {
    if (!data[(y * canvas.width + x) * 4 + 3]) continue;
    sumX += x; sumY += y; count++;
  }
  panel.playerData.isPlayerMarkerVisible = false;
  if (!count) return { available: true, drawn: false };
  const scaleX = box.width / canvas.width, scaleY = box.height / canvas.height;
  return { available: true, drawn: true,
    x: box.left - view.left + (sumX / count + 0.5) * scaleX, y: box.top - view.top + (sumY / count + 0.5) * scaleY };
})()`;
const angleError = (a, b) => Math.abs(((a - b) % 360 + 540) % 360 - 180);
const pinging = `(document.querySelector('.tanuki-position')?.getAnimations().length ?? 0) > 0`;

// 사이트가 지금 가진 층 데이터(지도 상태 기록이 함께 남긴 층 데이터 표)에 Local의 층 판정 식을 적용해, 이 위치에서
// 사이트가 골라야 할 층을 구한다. 층 데이터 표의 순서는 사이트가 판정하는 순서(위층부터)와 같다.
const coordsModule = `data:text/javascript;base64,${Buffer.from(await readFile(path.join(repository, 'viewer/coords.js'), 'utf8')).toString('base64')}`;
const levelCheck = ({ x, y, z }) => `(async () => {
  const { levelAtPosition } = await import(${JSON.stringify(coordsModule)});
  const copy = (value) => value == null ? value : JSON.parse(JSON.stringify(value));
  const live = window[Symbol.for('TanukiTarkovMap.mapStates')]?.levelSets?.().at(-1) ?? null;
  const levels = live && Object.entries(live).map(([id, level]) => ({ id, num: level.num, height: copy(level.height), zones: copy(level.zones) }));
  return { sync: window.tanukiPilot.levelSync(), expected: levels ? levelAtPosition(levels, ${x}, ${y}, ${z})?.num ?? null : null };
})()`;

const browser = await launchChrome({ realUserAgent: true, width: 1280, height: 900 });
let failed = 0;
try {
  await browser.size(420, 360);
  // 앱은 로드 시작 시점에 넣는다. 지도 상태는 사이트가 만들 때만 기록할 수 있다.
  await browser.send('Page.addScriptToEvaluateOnNewDocument', { source: injected.capture });
  for (const mapId of maps) {
    const problems = [], notes = [];
    const opened = await browser.navigate(`https://tarkov-market.com/maps/${mapId}`,
      `location.pathname === '/maps/${mapId}' && !!document.querySelector('.pan.map-cont') && !!document.querySelector('canvas.doc-map-canvas')`, 45000);
    if (!opened) {
      failed++;
      console.log(`FAIL ${mapId}: map page did not open (bot check or page layout change)`);
      continue;
    }
    await browser.evaluate(injected.cleanup).catch(() => {});
    await browser.evaluate(injected.layout).catch(() => {});
    await browser.evaluate(injected.markers);
    await browser.evaluate(injected.bridge);
    const meta = JSON.parse(awaitRead(`resources/maps/${mapId}/meta.json`));
    const base = basePoint(meta, JSON.parse(awaitRead(`resources/maps/${mapId}/markers.json`)));
    // 층이 여럿인 맵은 기본 층이 아닌 층의 위치로 시작한다. 앱처럼 맵을 열자마자 보내므로, 사이트의 층 데이터가
    // 아직 없을 때 맡긴 위치로도 층이 바뀌는지가 여기서 갈린다
    const target = levelTarget(meta, base);
    const position = target ?? base;
    const filename = filenameAt(position);
    const send = () => browser.evaluate(`window.tanukiPilot.showScreenshot(${JSON.stringify(filename)})`).catch((error) => `error: ${error.message}`);
    const rendered = () => browser.evaluate(`window.tanukiPilot.isRendered(${JSON.stringify(filename)})`).catch(() => false);
    let sent = await send();
    if (sent === true && !(await browser.evaluate(pinging))) problems.push('first position did not ping');
    await browser.evaluate(injected.keepVisible);
    await browser.evaluate(injected.ui);
    await browser.evaluate('window.hideHeader(); window.hideFooter(); window.hidePanelLeft(); window.hidePanelRight(); window.hidePanelTop(); true');
    // 맞춤(600ms 뒤 시작)과 그 뒤 세 번의 재확인(1.2초 간격)이 끝날 때까지 기다린다. 앱의 위치 유지
    // 타이머처럼 2초마다 표시를 확인하고, 사라졌으면 다시 보낸다. 표시된 뒤에 다시 보냈다면 앱에서는
    // 그때마다 위치가 다시 전달되는 것이므로 실패로 센다.
    let resends = 0;
    for (let waited = 0; waited < 7000; waited += 2000) {
      await new Promise((resolve) => setTimeout(resolve, 2000));
      if (sent === true && !(await rendered())) resends++;
      if (sent !== true || !(await rendered())) sent = await send();
    }
    if (resends) problems.push(`shown position needed ${resends} resend(s) (periodic check lost it)`);

    // 층: 맵을 연 직후 맡긴 위치로 사이트가 층을 바꿨는지 본다. 사이트 원과의 비교(아래)는 같은 좌표를 다시 넣으므로
    // 층에 영향이 없다
    let levelSummary = 'single';
    if (target) {
      const level = await browser.evaluate(levelCheck(position));
      levelSummary = `${level.sync.level}/${level.expected}`;
      if (level.expected === null) problems.push('site level data not found');
      else if (level.sync.level !== level.expected) {
        problems.push(`site shows level ${level.sync.level} instead of ${level.expected} (sync ${level.sync.state})`);
      }
      if (level.expected !== null && level.expected !== target.level) {
        notes.push(`resources pick level ${target.level} where the site picks ${level.expected} (resources may be stale)`);
      }
    }

    const marker = await browser.evaluate(markerCheck);
    if (artifacts) await browser.screenshot(path.join(artifacts, `online-${mapId}-position.png`));
    if (sent !== true) problems.push(`position not shown (${marker.status})`);
    if (!marker.inside) problems.push('marker not inside the view');
    if (!marker.shown) problems.push('position marker missing');
    else {
      if (marker.offset > 1) problems.push(`circle is ${marker.offset.toFixed(1)}px off the projected point`);
      if (Math.abs(marker.distance - 17.25) > 0.5) problems.push(`triangle sits ${marker.distance.toFixed(2)}px from the circle center`);
      if (angleError(marker.angle, marker.expectedAngle) > 3)
        problems.push(`triangle points ${marker.angle.toFixed(1)} instead of ${marker.expectedAngle.toFixed(1)}`);
      const site = await browser.evaluate(siteCircle(filename)).catch((error) => ({ available: false, error: error.message }));
      if (!site.available) notes.push('site circle comparison skipped ("Where am i" input not found)');
      else if (!site.drawn) problems.push('site did not draw its own circle for the same screenshot');
      else {
        const gap = Math.hypot(site.x - marker.circle.x, site.y - marker.circle.y);
        if (gap > 1.5) problems.push(`circle is ${gap.toFixed(1)}px away from the site's own circle`);
      }
    }

    // 같은 파일을 다시 보내도 핑을 다시 켜지 않아야 한다. 처음 핑(0.7초)은 이미 끝났다.
    const resent = await send();
    if (resent !== true) problems.push(`resending the same screenshot failed (${await browser.evaluate('window.tanukiPilot.status()')})`);
    if (await browser.evaluate(pinging)) problems.push('resend restarted the ping');

    const fit = await browser.evaluate(`(() => { const log = window.__tanukiKeepVisible.log();
      const last = log.filter(entry => entry['단계'] === 'verify' || entry['단계'] === 'fit').at(-1) ?? {};
      return { source: window.__tanukiKeepVisible.picture()?.source ?? null, fill: last['채움'] ?? null,
        scale: window.tanukiPilot.getMap()?.panzoom?.getTransform?.().scale ?? null }; })()`);
    const atMinZoom = Math.abs(fit.scale - meta.minZoom) <= meta.minZoom * 0.01;
    if (!fit.source) problems.push('terrain not measured (fit and clamp idle)');
    else if (!(fit.fill >= 0.75 && fit.fill <= 0.98) && !(fit.fill > 0.98 && atMinZoom)) {
      problems.push(`fit fill ${fit.fill} at zoom ${fit.scale}`);
    }

    for (let step = 0; step < 5; step++) { await browser.wheel(415, 20, -100); await browser.frames(4); }
    const wheel = await browser.evaluate(terrainAtCenter);
    if (!wheel.canvas) problems.push('terrain canvas missing');
    else if (!wheel.covers) problems.push('terrain left the view center after wheel zoom');

    // 사이트의 회전 버튼과 같은 호출이다. 회전 뒤 다음 프레임에 삼각형이 새 화면 방향을 따라야 한다.
    await browser.evaluate('window.tanukiPilot.getMap().rotateView(90); true');
    await browser.frames(6);
    const turned = await browser.evaluate(markerCheck);
    if (!turned.shown || angleError(turned.angle, turned.expectedAngle) > 3
        || angleError(turned.angle, marker.angle ?? turned.angle) < 45) {
      problems.push(`triangle after rotating the map: ${JSON.stringify({ shown: turned.shown, angle: turned.angle, expected: turned.expectedAngle })}`);
    }
    await browser.evaluate('window.tanukiPilot.getMap().rotateView(-90); true');

    if (artifacts) await browser.screenshot(path.join(artifacts, `online-${mapId}.png`));
    if (problems.length) failed++;
    console.log(`${problems.length ? 'FAIL' : 'PASS'} ${mapId}: fit=${fit.source}/${fit.fill}`
      + (atMinZoom && fit.fill > 0.98 ? ' (min zoom)' : '') + `, level=${levelSummary}`
      + (notes.length ? ` (${notes.join('; ')})` : '')
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
