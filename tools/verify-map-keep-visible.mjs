#!/usr/bin/env node
/**
 * INTENT
 * 지형과 같은 클래스를 공유하는 숨은 캔버스 때문에 실제 그림을 찾지 못하는 회귀를 검사한다.
 * 실제 Chromium의 DOM, 캔버스와 입력 이벤트로 초기 맞춤, 드래그 제한과 캔버스 교체를 확인하고,
 * 최소 배율에서도 지형이 창보다 큰 맵에서 맞춘 뒤에도 내 위치 마커가 화면 안에 남는지 확인한다.
 * 판정은 주입 스크립트의 캐시 대신 원본 캔버스의 전체 픽셀을 읽어 수행한다.
 * 페이지는 외부 네트워크 없이 작은 지도 입력 모델로 실행하므로 라이브 사이트 호환성 검증은 별도다.
 * Node 22+, CHROME_PATH로 브라우저 지정 가능. --script <path>로 이전 판과 비교할 수 있다.
 */
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { mkdtemp, readFile, rm } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';

const executable = [process.env.CHROME_PATH,
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  '/usr/bin/chromium', '/usr/bin/chromium-browser', '/usr/bin/google-chrome',
].filter(Boolean).find(existsSync);
assert.ok(executable, 'CHROME_PATH에 Chromium 브라우저 경로를 지정해야 합니다.');
const argument = process.argv.indexOf('--script');
const script = await readFile(argument < 0
  ? new URL('../src/TanukiTarkovMap/Models/JavaScript/Scripts/map-keep-visible.js', import.meta.url)
  : path.resolve(process.argv[argument + 1]), 'utf8');
const profile = await mkdtemp(path.join(os.tmpdir(), 'tanuki-keep-visible-'));
const browser = spawn(executable, ['--headless=new', '--disable-gpu', '--no-first-run',
  '--no-default-browser-check', '--remote-debugging-address=127.0.0.1',
  '--remote-debugging-port=0', `--user-data-dir=${profile}`, 'about:blank'],
{ stdio: 'ignore', windowsHide: true });
let launchError, socket, passed = 0;
browser.on('error', error => { launchError = error; });
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));

// tall은 최소 배율(0.5)에서도 지형이 창보다 큰 맵이고, marker는 맞추기 전에 이미 표시된 내 위치다.
function fixture({ overview = true, unrelated = true, ambiguous = false, hiddenSized = false, tall = false, marker = false } = {}, id) {
  return `<!doctype html><style>
    body{margin:0}.map-cont{position:relative;width:800px;height:600px;overflow:hidden}
    .map-wrap{position:absolute;width:2000px;height:1800px;transform-origin:0 0}
    canvas{position:absolute;inset:0;width:800px;height:600px}
    .map-scene{width:2000px;height:1800px}
    .marker{position:absolute;left:1000px;top:1600px;width:20px;height:20px;margin:-10px 0 0 -10px}
    </style>
    ${unrelated ? '<canvas class="doc-map-canvas" width="800" height="600" id="unrelated"></canvas>' : ''}
    <div class="map-cont pan">
    <div class="map-wrap"><div class="map-scene"></div>${marker ? '<div class="marker"></div>' : ''}</div>
    ${overview ? '<canvas class="doc-map-canvas" style="display:none" id="overview"></canvas>' : ''}
    ${hiddenSized ? '<canvas class="doc-map-canvas" width="800" height="600" style="visibility:hidden" id="hiddenSized"></canvas>' : ''}
    <canvas class="doc-map-canvas" width="800" height="600" id="terrain"></canvas>
    ${ambiguous ? '<canvas class="doc-map-canvas" width="800" height="600" id="ambiguous"></canvas>' : ''}
    </div><script>
    const container=document.querySelector('.map-cont'),wrap=document.querySelector('.map-wrap');
    let terrain=document.querySelector('#terrain'),world=${tall ? '{x:800,y:100,width:400,height:1600}' : '{x:800,y:650,width:400,height:500}'};
    const minScale=${tall ? 0.5 : 0},camera={x:0,y:0,scale:${tall ? 0.5 : 0.4}};let cursor=null;
    function draw(){
      wrap.style.transform='translate('+camera.x+'px,'+camera.y+'px) scale('+camera.scale+')';
      const context=terrain.getContext('2d');context.clearRect(0,0,800,600);
      context.fillStyle='green';context.fillRect(camera.x+world.x*camera.scale,camera.y+world.y*camera.scale,world.width*camera.scale,world.height*camera.scale);
    }
    // 사이트 panzoom처럼 최소 배율에서는 축소가 막힌다
    container.addEventListener('wheel',event=>{
      event.preventDefault();const step=1-Math.sign(event.deltaY)*Math.min(0.25,Math.abs(event.deltaY)/128);
      const r=Math.max(minScale,camera.scale*step)/camera.scale;
      camera.x=r*camera.x+(1-r)*event.clientX;camera.y=r*camera.y+(1-r)*event.clientY;camera.scale*=r;draw();
    },{passive:false});
    container.addEventListener('mousedown',event=>{cursor={x:event.clientX,y:event.clientY};});
    document.addEventListener('mousemove',event=>{if(!cursor)return;camera.x+=event.clientX-cursor.x;camera.y+=event.clientY-cursor.y;cursor={x:event.clientX,y:event.clientY};draw();});
    document.addEventListener('mouseup',()=>{cursor=null;});
    window.fixture={id:${id},replace(){
      const replacement=terrain.cloneNode();terrain.replaceWith(replacement);terrain=replacement;
      world={x:800,y:650,width:100,height:500};draw();
    },state:()=>({...camera})};
    // 사이트의 지도 객체처럼 panzoom의 moveBy를 내놓는다. 화면 이동은 Pilot 브리지가 찾은 지도 객체를 쓴다.
    // revealPosition은 Pilot 브리지처럼 마커가 창 안쪽 70% 밖에 있을 때만 마커를 가운데로 옮긴다
    window.tanukiPilot={getMap:()=>({panzoom:{moveBy(dx,dy){camera.x+=dx;camera.y+=dy;draw();}}}),
      hasPosition:()=>!!document.querySelector('.marker'),
      revealPosition(){
        const m=document.querySelector('.marker');if(!m)return false;
        const b=m.getBoundingClientRect(),v=container.getBoundingClientRect(),x=b.left+b.width/2,y=b.top+b.height/2;
        if(x>=v.left+v.width*0.15&&x<=v.right-v.width*0.15&&y>=v.top+v.height*0.15&&y<=v.bottom-v.height*0.15)return false;
        camera.x+=v.left+v.width/2-x;camera.y+=v.top+v.height/2-y;draw();return true;
      }};
    for(const decoy of document.querySelectorAll('#unrelated, #hiddenSized'))decoy.getContext('2d').fillRect(0,0,800,600);
    draw();</script>`;
}

// 프로덕션 코드의 축소 표본과 캐시를 사용하지 않고 실제 캔버스의 전체 픽셀을 검사한다.
const measurement = `(() => {
  const c=document.querySelector('#terrain'),data=c.getContext('2d').getImageData(0,0,c.width,c.height).data;
  let left=c.width,top=c.height,right=-1,bottom=-1;
  for(let y=0;y<c.height;y++)for(let x=0;x<c.width;x++)if(data[(y*c.width+x)*4+3]){left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);}
  return {left,top,right:right+1,bottom:bottom+1,fill:Math.max((right-left+1)/800,(bottom-top+1)/600),
    errorX:(left+right+1)/2-400,errorY:(top+bottom+1)/2-300,
    containsCenter:left<=400&&right>=400&&top<=300&&bottom>=300,empty:right<0};
})()`;

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
  socket = new WebSocket(targets.find(target => target.type === 'page').webSocketDebuggerUrl);
  const pending = new Map();
  let nextId = 0;
  socket.addEventListener('message', ({ data }) => {
    const response = JSON.parse(data), request = pending.get(response.id);
    if (!request) return;
    pending.delete(response.id); clearTimeout(request.timer);
    response.error ? request.reject(new Error(response.error.message)) : request.resolve(response.result);
  });
  await new Promise((resolve, reject) => {
    socket.addEventListener('open', resolve, { once: true });
    socket.addEventListener('error', reject, { once: true });
  });
  const send = (method, params = {}) => new Promise((resolve, reject) => {
    const id = ++nextId;
    const timer = setTimeout(() => { pending.delete(id); reject(new Error(`CDP timeout: ${method}`)); }, 10000);
    pending.set(id, { resolve, reject, timer });
    socket.send(JSON.stringify({ id, method, params }));
  });
  const evaluate = async expression => {
    const result = await send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (result.exceptionDetails) throw new Error(result.exceptionDetails.exception?.description ?? result.exceptionDetails.text);
    return result.result.value;
  };
  await send('Emulation.setDeviceMetricsOverride', { width: 800, height: 600, deviceScaleFactor: 1, mobile: false });

  let fixtureId = 0;
  async function load(options) {
    const id = ++fixtureId;
    await send('Page.navigate', { url: `data:text/html;base64,${Buffer.from(fixture(options, id)).toString('base64')}` });
    let ready = false;
    for (let attempt = 0; attempt < 50; attempt++) {
      try { ready = await evaluate(`window.fixture?.id === ${id} && document.readyState === 'complete'`); }
      catch (error) {
        if (!/context.*(destroyed|find)|find.*context/i.test(error.message)) throw error;
      }
      if (ready) break;
      await delay(40);
    }
    assert.ok(ready, 'New fixture did not finish loading');
    await evaluate(script);
    await delay(2200);
  }
  async function fitted(label, expression = measurement) {
    const actual = await evaluate(expression);
    assert.ok(actual.fill >= 0.75 && actual.fill <= 0.98, `${label}: fill ${actual.fill}`);
    assert.ok(Math.abs(actual.errorX) <= 4 && Math.abs(actual.errorY) <= 4,
      `${label}: center ${actual.errorX}, ${actual.errorY}`);
    assert.ok(actual.containsCenter, `${label}: no terrain at viewport center`);
    console.log(`PASS ${label} ${JSON.stringify(actual)}`); passed++;
  }
  async function drag(delta) {
    await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: 20, y: 300, button: 'left', buttons: 1, clickCount: 1 });
    for (let step = 1; step <= 15; step++) {
      await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: 20 + delta * step / 15, y: 300, button: 'left', buttons: 1 });
      await delay(20);
    }
    await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: 20 + delta, y: 300, button: 'left', buttons: 0, clickCount: 1 });
  }

  await load({ overview: false, unrelated: false }); await fitted('single terrain canvas');
  await load({ unrelated: false }); await fitted('hidden overview before terrain');
  await load({}); await fitted('hidden overview and unrelated canvas');
  await drag(2000);
  assert.ok((await evaluate(measurement)).containsCenter, 'right drag lost terrain');
  await drag(-2000);
  assert.ok((await evaluate(measurement)).containsCenter, 'left drag lost terrain');
  console.log('PASS trusted drag containment'); passed++;

  // 커서 자리를 고정점으로 확대하므로 지형 밖에서 확대하면 지형이 화면 밖으로 밀린다.
  // 다음 프레임의 보정이 지형을 다시 화면 가운데에 걸치게 해야 한다.
  for (let step = 0; step < 6; step++) {
    await send('Input.dispatchMouseEvent', { type: 'mouseWheel', x: 790, y: 20, deltaX: 0, deltaY: -100 });
    await evaluate('new Promise(r => requestAnimationFrame(() => requestAnimationFrame(() => requestAnimationFrame(r))))');
  }
  const zoomed = await evaluate(measurement);
  assert.ok(zoomed.containsCenter && !zoomed.empty, `wheel zoom lost terrain: ${JSON.stringify(zoomed)}`);
  console.log(`PASS wheel zoom outside terrain stays containable ${JSON.stringify(zoomed)}`); passed++;

  await load({});
  await evaluate('fixture.replace()');
  await drag(2000);
  const replacement = await evaluate(measurement);
  assert.ok(replacement.containsCenter && Math.abs(replacement.errorX) <= 4,
    `replacement canvas retained stale bounds: ${JSON.stringify(replacement)}`);
  console.log(`PASS replacement canvas invalidates geometry ${JSON.stringify(replacement)}`); passed++;

  await load({ ambiguous: true });
  assert.deepEqual(await evaluate('fixture.state()'), { x: 0, y: 0, scale: 0.4 },
    'ambiguous terrain must not receive fitting input');
  assert.equal(await evaluate('window.__tanukiKeepVisible.picture()'), null);
  console.log('PASS ambiguous rendered canvases stop measurement'); passed++;

  // 최소 배율에서도 지형이 창보다 크면 맞추기는 배율 한계에서 멈춘다. 앱은 맞추기 전에 위치를 보내므로
  // 맞추기와 재확인이 끝난 뒤에도 마커가 창 안쪽에 남고, 배율이 바뀌지 않고, 화면이 더 움직이지 않아야 한다.
  await load({ tall: true, marker: true, overview: false, unrelated: false });
  await delay(4500);
  const markerCheck = `(() => { const b=document.querySelector('.marker').getBoundingClientRect(),x=b.left+b.width/2,y=b.top+b.height/2;
    return { inside: x>=120&&x<=680&&y>=90&&y<=510, camera: fixture.state(), limited: window.__tanukiKeepVisible.zoomLimited?.() }; })()`;
  const settled = await evaluate(markerCheck);
  assert.ok(settled.inside, `position marker left the view after fitting: ${JSON.stringify(settled)}`);
  assert.equal(settled.camera.scale, 0.5, `zoom drifted at the minimum: ${JSON.stringify(settled)}`);
  assert.ok(settled.limited, 'fit must report the zoom limit');
  await delay(1500);
  assert.deepEqual(await evaluate('fixture.state()'), settled.camera, 'view kept moving after the fit settled');
  console.log(`PASS position stays visible when terrain exceeds the view at minimum zoom ${JSON.stringify(settled)}`); passed++;

  // 사이트는 배율에 따라 보이는 캔버스를 맞바꾸고, 숨은 쪽을 visibility: hidden으로 두면 화면 크기가
  // 그대로 남는다. 크기만 보고 고르면 칠해진 숨은 캔버스를 지형으로 재게 된다.
  await load({ hiddenSized: true, unrelated: false });
  await fitted('canvas hidden by visibility keeps its size but is not measured');
  console.log(`PASS ${passed} keep-visible checks`);
} catch (error) {
  console.error(error.stack); process.exitCode = 1;
} finally {
  socket?.close();
  browser.kill();
  for (let attempt = 0; browser.exitCode === null && attempt < 50; attempt++) await delay(100);
  // 생성한 임시 디렉터리만 정리한다. 브라우저가 종료되지 않았으면 프로필을 남긴다.
  const resolved = path.resolve(profile), temporary = path.resolve(os.tmpdir());
  if (browser.exitCode !== null && path.dirname(resolved) === temporary
      && path.basename(resolved).startsWith('tanuki-keep-visible-')) {
    await rm(resolved, { recursive: true, force: true, maxRetries: 3 }).catch(() => {});
  }
}
