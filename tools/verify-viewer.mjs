#!/usr/bin/env node
/**
 * INTENT
 * Local 미니맵(viewer/)이 리소스(resources/)와 함께 실제 브라우저에서 코어를 지키는지 맵마다 검사한다.
 * 판정은 뷰어가 스스로 보고하는 값이 아니라 DOM과 SVG 변환, 캔버스 픽셀, 사이트가 계산해 둔 값으로 한다.
 * - 사이트 대조: 수집 때 사이트의 파일명 해석기와 gamePosToMapPos가 낸 값(manifest.screenshotChecks,
 *   meta.source.coordinateChecks)과 뷰어의 식이 같은지
 * - 위치와 방향: 스크린샷 파일명을 넣으면 마커가 SVG의 그 지점 위에 보이고 삼각형이 그 방향을 가리키는지
 * - 카메라: 지형 밖에서 휠 확대, 멀리 끌기, 창 크기 변경 뒤에도 지형이 화면 가운데를 덮는지
 * - 오버레이: UI가 기본으로 숨고, 켜면 Levels가 보이고, Alt 휠과 높이로 층이 바뀌는지, 진영 필터가 맞는지
 * - 안내 언어: 앱이 넘긴 언어(?lang=, setLanguage)로 오류 안내를 그리는지, 언어를 넘겨도 지도가 열리는지.
 *   번역 사전을 다시 적지 않고 화면에 그려진 글자의 문자 종류(가나, 한글, ASCII)로 판정한다
 * 앱과 게임은 실행하지 않으며 네트워크로 나가는 요청도 없다.
 * Usage: node tools/verify-viewer.mjs [--root <folder with viewer/ and resources/>] [--maps a,b] [--artifacts dir]
 *   배포 결과를 검사할 때는 --root publish/LocalMap
 */
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { mkdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { launchChrome } from './headless-chrome.mjs';

const args = process.argv.slice(2);
const option = (name, fallback) => (args.includes(name) ? args[args.indexOf(name) + 1] : fallback);
const repository = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const root = path.resolve(option('--root', repository));
const artifacts = option('--artifacts', null);
if (artifacts) await mkdir(artifacts, { recursive: true });
const manifest = JSON.parse(await readFile(path.join(root, 'resources/manifest.json'), 'utf8'));
const maps = option('--maps', '') ? option('--maps').split(',') : manifest.maps;

// 정적 서버. 앱의 LocalViewer처럼 viewer/와 resources/만 응답한다.
const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.svg': 'image/svg+xml' };
const server = createServer(async (request, response) => {
  try {
    const name = decodeURIComponent(new URL(request.url, 'http://localhost').pathname).slice(1);
    if (!/^(viewer|resources)\//.test(name) || name.split('/').includes('..')) throw new Error('outside');
    const body = await readFile(path.join(root, name));
    response.writeHead(200, { 'content-type': types[path.extname(name)] ?? 'application/octet-stream' });
    response.end(body);
  } catch {
    response.writeHead(404);
    response.end();
  }
});
await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
const origin = `http://127.0.0.1:${server.address().port}`;

// 게임 좌표 <-> 지도 좌표. 뷰어 코드와 별개로 사이트의 식을 다시 적어 대조한다.
const round = (value) => Math.round(value * 1e4) / 1e4;
function gameToMap({ x, y }, t) {
  if (t.rotate) {
    const r = -t.rotate * Math.PI / 180;
    [x, y] = [round(x * Math.cos(r) - y * Math.sin(r)), round(x * Math.sin(r) + y * Math.cos(r))];
  }
  return { x: round(t.xOffset - x * t.ratio), y: round(t.yOffset - y * t.ratio) };
}
function mapToGame({ x, y }, t) {
  let gx = (t.xOffset - x) / t.ratio, gy = (t.yOffset - y) / t.ratio;
  if (t.rotate) {
    const r = t.rotate * Math.PI / 180;
    [gx, gy] = [gx * Math.cos(r) - gy * Math.sin(r), gx * Math.sin(r) + gy * Math.cos(r)];
  }
  return { x: gx, y: gy };
}
// 사이트 파일명 순서: 첫 수가 y, 둘째가 높이 z, 셋째가 x다. 바라보는 각 look은 y축 회전 쿼터니언으로 만든다.
function filenameFor({ x, y, z = 0 }, look) {
  const half = look * Math.PI / 360;
  return `2026-01-01[00-00]_${y.toFixed(2)}, ${z.toFixed(2)}, ${x.toFixed(2)}_0, ${Math.sin(half).toFixed(5)}, 0, ${Math.cos(half).toFixed(5)}_1.00 (0).png`;
}

let passed = 0, failed = 0;
async function check(name, run) {
  try { await run(); passed++; console.log(`PASS ${name}`); }
  catch (error) { failed++; console.error(`FAIL ${name}: ${error.message}`); }
}

const browser = await launchChrome({ width: 420, height: 360 });
const exceptions = [];
browser.on('Runtime.exceptionThrown', (event) => exceptions.push(event.exceptionDetails?.exception?.description ?? event.exceptionDetails?.text));
try {
  await browser.size(420, 360);

  await check('site screenshot parser outputs match the viewer', async () => {
    assert.ok(await browser.navigate(`${origin}/viewer/index.html?map=${maps[0]}`, `window.tanukiViewer?.status() === 'ready'`), 'viewer did not load');
    if (!manifest.screenshotChecks?.length) { console.log('  (resources have no screenshotChecks; skipped)'); return; }
    for (const { filename, result } of manifest.screenshotChecks) {
      const parsed = await browser.evaluate(`import('/viewer/coords.js').then(m => m.parseScreenshot(${JSON.stringify(filename)}))`);
      for (const key of ['x', 'y', 'z']) assert.equal(parsed[key], result[key], `${filename} ${key}`);
      assert.ok(Math.abs(parsed.look - result.look) < 1e-6, `${filename} look ${parsed.look} != ${result.look}`);
    }
  });

  await check('loading and error notices follow the app language', async () => {
    const kana = /[぀-ヿ]/, hangul = /[가-힣]/;
    const notice = `document.getElementById('loadStatus').textContent`;
    // 언어를 넘겨도 지도는 그대로 열리고, 글꼴 선택에 쓰는 문서 언어가 따라온다
    assert.ok(await browser.navigate(`${origin}/viewer/index.html?map=${maps[0]}&lang=ja`, `window.tanukiViewer?.status() === 'ready'`), 'viewer did not load with lang');
    assert.equal(await browser.evaluate('document.documentElement.lang'), 'ja');
    // 없는 맵을 열어 오류 안내를 띄운다. 안내는 화면 언어로, 앱 로그에 남는 status()는 영어로 나온다
    assert.ok(await browser.navigate(`${origin}/viewer/index.html?map=__missing__&lang=ja`, `window.tanukiViewer?.status().startsWith('error')`), 'missing map did not fail');
    const japanese = await browser.evaluate(notice);
    assert.ok(kana.test(japanese) && japanese.includes('__missing__'), `ja notice: ${japanese}`);
    const status = await browser.evaluate('window.tanukiViewer.status()');
    assert.ok(!kana.test(status) && !hangul.test(status) && status.includes('__missing__'), `status: ${status}`);
    // 실행 중 전환과 모르는 언어(영어로 대체)
    assert.equal(await browser.evaluate(`window.tanukiViewer.setLanguage('ko')`), true);
    const korean = await browser.evaluate(notice);
    assert.ok(hangul.test(korean) && korean.includes('__missing__'), `ko notice: ${korean}`);
    await browser.evaluate(`window.tanukiViewer.setLanguage('xx')`);
    const fallback = await browser.evaluate(notice);
    assert.ok(/^[\x20-\x7e]+$/.test(fallback) && fallback.includes('__missing__'), `fallback notice: ${fallback}`);
    assert.equal(await browser.evaluate('document.documentElement.lang'), 'en');
  });

  for (const mapId of maps) {
    const meta = JSON.parse(await readFile(path.join(root, `resources/maps/${mapId}/meta.json`), 'utf8'));
    const markerData = JSON.parse(await readFile(path.join(root, `resources/maps/${mapId}/markers.json`), 'utf8'));
    exceptions.length = 0;
    await browser.size(420, 360);

    await check(`${mapId}: opens with UI hidden and no errors`, async () => {
      assert.ok(await browser.navigate(`${origin}/viewer/index.html?map=${mapId}`, `window.tanukiViewer?.status() !== 'loading' && !!window.tanukiViewer`, 30000), 'timeout');
      assert.equal(await browser.evaluate('window.tanukiViewer.status()'), 'ready');
      assert.equal(await browser.evaluate(`getComputedStyle(document.querySelector('.panel_right')).display`), 'none');
      assert.deepEqual(exceptions, []);
    });

    await check(`${mapId}: coordinates match the site's gamePosToMapPos`, async () => {
      for (const { game, map } of meta.source?.coordinateChecks ?? []) {
        assert.deepEqual(gameToMap(game, meta.transform), map, `tool formula ${JSON.stringify(game)}`);
        const viewer = await browser.evaluate(`import('/viewer/coords.js').then(m => m.gamePositionToMapPosition(${game.x}, ${game.y}, ${JSON.stringify(meta.transform)}))`);
        assert.deepEqual(viewer, map, `viewer formula ${JSON.stringify(game)}`);
      }
    });

    // 첫 추출구 자리(지형 위가 보장된 지점)를 게임 좌표로 되돌려 스크린샷 파일명을 만든다.
    const target = markerData.markers[0]?.position ?? { x: meta.size.width / 2, y: meta.size.height / 2 };
    const look = 30;
    const filename = filenameFor(mapToGame(target, meta.transform), look);
    await check(`${mapId}: screenshot shows the marker on that map point with its direction`, async () => {
      assert.equal(await browser.evaluate(`window.tanukiViewer.showScreenshot(${JSON.stringify(filename)})`), true);
      await browser.frames(30);
      const state = await browser.evaluate(`(() => {
        const marker = document.getElementById('playerMarker'), box = marker.getBoundingClientRect();
        const svg = document.querySelector('#mapWrap > svg'), point = svg.createSVGPoint();
        point.x = ${target.x}; point.y = ${target.y};
        const screen = point.matrixTransform(svg.getScreenCTM());
        const arrow = new DOMMatrixReadOnly(getComputedStyle(marker.querySelector('.triangle-indicator')).transform);
        return { center: [box.left + box.width / 2, box.top + box.height / 2], screen: [screen.x, screen.y],
          visible: marker.checkVisibility(), inside: box.left >= 0 && box.top >= 0 && box.right <= innerWidth && box.bottom <= innerHeight,
          angle: (Math.atan2(arrow.b, arrow.a) * 180 / Math.PI + 360) % 360,
          rendered: window.tanukiViewer.isRendered(${JSON.stringify(filename)}) };
      })()`);
      assert.ok(state.visible && state.inside, 'marker not visible inside the view');
      assert.ok(Math.hypot(state.center[0] - state.screen[0], state.center[1] - state.screen[1]) <= 1.5,
        `marker ${state.center} is not on map point ${state.screen}`);
      const expected = ((look + 270 - meta.transform.rotate) % 360 + 360) % 360;
      const difference = Math.abs(((state.angle - expected) % 360 + 540) % 360 - 180);
      assert.ok(difference < 0.5, `direction ${state.angle} != ${expected}`);
      assert.equal(state.rendered, true);
    });

    // 지형 범위를 SVG 변환으로 다시 재서 화면 가운데를 덮는지 본다. 바탕 사각형은 지형이 아니다.
    const coversCenter = `(() => {
      const svg = document.querySelector('#mapWrap > svg');
      const group = [...svg.querySelectorAll(':scope > g[data-map-level]')].find(g => g.style.display !== 'none') ?? svg;
      let left = Infinity, top = Infinity, right = -Infinity, bottom = -Infinity;
      for (const child of group.children) {
        if (child.matches('[data-map-background="true"], defs')) continue;
        const b = child.getBBox(); if (!b.width || !b.height) continue;
        left = Math.min(left, b.x); top = Math.min(top, b.y); right = Math.max(right, b.x + b.width); bottom = Math.max(bottom, b.y + b.height);
      }
      const m = svg.getScreenCTM(), c = document.getElementById('mapCont').getBoundingClientRect();
      const a = new DOMPoint(left, top).matrixTransform(m), z = new DOMPoint(right, bottom).matrixTransform(m);
      return a.x <= c.width / 2 && z.x >= c.width / 2 && a.y <= c.height / 2 && z.y >= c.height / 2;
    })()`;
    await check(`${mapId}: camera keeps terrain at the view center (wheel, drag, resize)`, async () => {
      for (let step = 0; step < 6; step++) { await browser.wheel(415, 5, -100); await browser.frames(); }
      assert.equal(await browser.evaluate(coversCenter), true, 'after wheel zoom at a corner');
      await browser.mouse('mousePressed', 210, 180);
      for (let step = 1; step <= 8; step++) await browser.mouse('mouseMoved', 210 - step * 120, 180 - step * 90);
      await browser.mouse('mouseReleased', 210 - 960, 180 - 720, 0);
      await browser.frames();
      assert.equal(await browser.evaluate(coversCenter), true, 'after a long drag');
      await browser.size(300, 240);
      await browser.frames();
      assert.equal(await browser.evaluate(coversCenter), true, 'after resize');
      await browser.size(420, 360);
      await browser.frames();
    });

    if (meta.levels.length > 1) {
      await check(`${mapId}: Levels panel, Alt+wheel and height-based level selection`, async () => {
        await browser.evaluate('window.tanukiViewer.setControlsVisible(true)');
        assert.notEqual(await browser.evaluate(`getComputedStyle(document.querySelector('.panel_right')).display`), 'none');
        const levels = [...meta.levels].sort((a, b) => b.sourceLevel - a.sourceLevel).map((level) => level.id);
        const before = await browser.evaluate(`document.querySelector('#levelsList input:checked').value`);
        await browser.wheel(200, 200, -100, 1);
        await browser.frames();
        const after = await browser.evaluate(`document.querySelector('#levelsList input:checked').value`);
        assert.equal(after, levels[Math.max(0, levels.indexOf(before) - 1)], 'Alt+wheel up');
        for (const level of meta.levels.filter((entry) => entry.zones?.length)) {
          const zone = level.zones.find((entry) => entry.rect && entry.height?.length === 2 && !entry.rotate);
          if (!zone) continue;
          const center = { x: (zone.rect[0][0] + zone.rect[1][0]) / 2, y: (zone.rect[0][1] + zone.rect[1][1]) / 2,
            z: (zone.height[0] + zone.height[1]) / 2 };
          await browser.evaluate(`window.tanukiViewer.showScreenshot(${JSON.stringify(filenameFor(center, 0))})`);
          assert.equal(await browser.evaluate(`document.querySelector('#levelsList input:checked').value`), level.id, `height ${center.z} in ${level.id}`);
        }
        await browser.evaluate('window.tanukiViewer.setControlsVisible(false)');
      });
    }

    await check(`${mapId}: faction filter switches extraction colors`, async () => {
      const count = `(() => {
        const c = document.getElementById('markersCanvas'), d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data;
        let pmc = 0, scav = 0;
        for (let i = 0; i < d.length; i += 4) {
          if (d[i + 3] < 200) continue;
          if (Math.abs(d[i] - 112) < 6 && Math.abs(d[i + 1] - 168) < 6 && d[i + 2] < 8) pmc++;
          if (Math.abs(d[i] - 174) < 6 && Math.abs(d[i + 1] - 174) < 6 && Math.abs(d[i + 2] - 176) < 6) scav++;
        }
        return { pmc, scav };
      })()`;
      // 앞의 카메라 검사가 확대하고 끌어 둔 화면에서는 추출구가 화면 밖에 있다. 처음 맞춘 화면에서 센다.
      assert.ok(await browser.navigate(`${origin}/viewer/index.html?map=${mapId}`, `window.tanukiViewer?.status() === 'ready'`), 'reload');
      await browser.evaluate('window.tanukiViewer.setFaction(true)');
      await browser.frames();
      const pmc = await browser.evaluate(count);
      await browser.evaluate('window.tanukiViewer.setFaction(false)');
      await browser.frames();
      const scav = await browser.evaluate(count);
      const has = (subtype) => markerData.markers.some((marker) => marker.subtype === subtype);
      if (has('pmc') || has('transit')) assert.ok(pmc.pmc > 0 && scav.pmc === 0, `PMC colors ${pmc.pmc} -> ${scav.pmc}`);
      if (has('scav')) assert.ok(scav.scav > 0 && pmc.scav === 0, `SCAV colors ${pmc.scav} -> ${scav.scav}`);
      await browser.evaluate('window.tanukiViewer.setFaction(true)');
      await browser.frames();
      if (artifacts) await browser.screenshot(path.join(artifacts, `${mapId}.png`));
    });
  }
} finally {
  await browser.close();
  server.close();
}
console.log(`${passed} passed, ${failed} failed`);
process.exitCode = failed ? 1 : 0;
