<!--
INTENT
검토자가 이번 브랜치에서 무엇이 왜 고장 났고 코드에서 무엇을 바꿨는지를 한 번에 판단하도록 쓴
작업 보고서다. PR 설명으로도 쓴다. 설계 근거의 원천은 각 파일의 머리 주석과 docs의 설계 문서이고,
이 문서는 문제와 수정의 대응과 검증 결과만 정리한다.
-->

# 위치 추적과 지도 맞춤 복구 작업 보고

## 요약

- 2026-10-07 사이트 배포 이후 Online에서 스크린샷을 찍어도 위치가 바뀌지 않았고, 맵을 열어도 창에
  맞춰지지 않았으며, 확대하면 지형이 화면 밖으로 사라졌습니다. 원인은 사이트가 위치 입력 함수를
  없앤 것과 바닥 맵을 그리는 캔버스 구조를 바꾼 것입니다.
- Online 위치 입력과 지도 맞춤, 이동 제한을 복구했습니다. 검증 도구를 새로 만들어 돌리는 과정에서
  맞춤이 위치 마커를 화면 밖으로 밀어내는 순서 문제를 추가로 찾아 고쳤습니다.
- 1차 작업물(`27dd3d9`)의 Local 미니맵은 사이트와 다른 화면에 구조가 과했습니다. 사이트 화면을 그대로
  따르는 미니맵으로 다시 만들고 구조를 줄였습니다.
- 실제 사이트 12개 맵에서 Online 경로를, headless 브라우저에서 Local 미니맵 72개 항목을 검증했습니다.
  실제 앱 실행 확인은 남아 있습니다.

## 문제와 수정

### 1. Online: 스크린샷을 찍어도 위치가 바뀌지 않음

**증상**: 앱 로그에 `[PilotBridge] Screenshot pending: pilot-unavailable`만 남고 지도에 위치가
표시되지 않았습니다.

**원인**: 2026-10-07에 배포된 사이트 번들에서 앱이 쓰던 위치 입력 함수 두 개가 모두 사라졌습니다.

- `window.pilot.positionFromScreenshot(filename)`
- Nuxt Pilot 서비스의 `$pilot.positionUpdate(z, x, y)`. 서비스에는 `onPositionUpdate`,
  `offPositionUpdate` 같은 구독 함수만 남았습니다

main과 1차 작업물의 브리지(`pilot-bridge.js`의 `getPilot()`)는 이 두 함수만 찾았으므로 위치를 넣을
곳이 없었습니다. 사이트 UI의 "Where am i" 창에 같은 파일명을 붙여 넣으면 위치가 표시되는 것으로, 사이트
기능은 살아 있고 프로그램용 진입점만 사라졌음을 확인했습니다.

**수정** (`src/TanukiTarkovMap/Models/JavaScript/Scripts/pilot-bridge.js`, 판 4)

- 입력 경로를 어댑터 목록 `inputs`로 바꾸고 페이지에 실제로 있는 첫 경로를 씁니다
  (`global-pilot` -> `pilot-service` -> `where-am-i`).
- `where-am-i` 어댑터는 렌더 중인 Vue 트리를 훑는 `findProps()`로 "Where am i" 처리기를 가진
  컴포넌트의 props를 찾고, 사용자가 붙여 넣을 때와 같은 인자로 부릅니다.

```js
{
    name: 'where-am-i',
    find() {
        return findProps(props => typeof props.onScreenPositionChange === 'function'
            && props.playerData && typeof props.playerData === 'object'
            && 'isPlayerMarkerVisible' in props.playerData);
    },
    send(panel, filename) {
        panel.playerData.isPlayerMarkerVisible = true;   // 창이 닫혀 있어도 마커가 남게
        panel.onScreenPositionChange({ target: { value: filename } });
        panel.onMakePlayerMarkerPing?.();
    }
}
```

  배포마다 이름이 바뀌는 압축된 내부 함수 대신 템플릿의 prop 이름을 기준으로 삼았습니다. Vue 3는
  부모의 이벤트 리스너도 `onXxx` prop으로 넘기고, 이 이름은 압축해도 남습니다.
- 성공 판정은 호출의 반환값이 아니라 `positionMatches()`입니다. 사이트 지도 객체의 `playerPos`가
  파일명 좌표와 같고 마커 요소가 있어야 성공입니다. 실패 사유에는
  `position-not-rendered (where-am-i)`처럼 어느 경로였는지를 함께 남깁니다.
- 새 위치가 화면 안쪽 70% 밖이면 `revealMarker()`가 사이트의 `map.centerOnPosition()`으로 가운데에
  놓습니다(`REVEAL_INSET = 0.15`).
- C#의 설치 확인문(`PilotBridge.js.cs`의 `IS_INSTALLED_SCRIPT`)을 판 4와 새 함수 기준으로 맞췄습니다.

**확인**: `verify-map-recovery.mjs`에 "Pilot 함수가 없는 판에서 Where am i 처리기 사용" 재현 사례를
추가했습니다(12/12 통과). 실제 사이트 12개 맵에서 입력 경로가 `where-am-i`로 잡히고 마커와 방향
삼각형이 화면 안에 표시됐습니다(`verify-online.mjs`).

### 2. Online: 맵을 열어도 창에 맞춰지지 않고 이동 제한이 동작하지 않음

**증상**: 맵을 열면 그림이 작게 뜨고 끌면 지형이 화면 밖으로 나갔습니다. 맞춤 로그에는 오류가
없었습니다.

**원인**: 사이트가 바닥 맵을 같은 클래스(`canvas.doc-map-canvas`)의 캔버스 둘로 그리고, 배율에 따라
하나만 보이게 바꿨습니다. 보통 배율에서는 첫째(지도 전체 캔버스)가 `display: none`이고, 많이 줄이면
둘째가 `visibility: hidden`이 됩니다. main의 맞춤 스크립트는 첫째를 골라 쟀습니다.

```js
var canvas = document.querySelector(CANVAS_SELECTOR);   // 숨은 첫째를 고름
```

화면 크기가 0인 캔버스에서는 그림을 잴 수 없고, 그림을 재지 못하면 쉬는 규칙에 따라 맞춤과 이동
제한이 오류 없이 멈췄습니다.

**수정** (`map-keep-visible.js`): 1차 작업물에서 들어온 `findTerrainCanvas()`(표시된 캔버스만 고르고,
보이는 캔버스가 바뀌면 재 둔 범위 `contentCache`를 버림)를 유지했습니다. 숨은 쪽이
`visibility: hidden`이라 화면 크기를 가진 경우도 이 조건에서 걸러집니다.

```js
if (!canvas.width || !canvas.height || !screen.width || !screen.height) continue;
var style = getComputedStyle(canvas);
if (style.visibility === 'hidden' || style.visibility === 'collapse' || style.opacity === '0') continue;
if (terrain) return null;   // 보이는 후보가 둘이면 재지 않음
```

SVG 측정 분기(`readSvgPicture`)는 지웠습니다. 지금 사이트에는 `svg.svg-map`이 없고, 그 경로를 쓰던
사이트 사본도 이번에 지웠습니다(9번).

**확인**: `verify-map-keep-visible.mjs`에 "visibility: hidden 캔버스는 크기가 있어도 재지 않음" 사례를
추가했습니다(9/9 통과). 실제 사이트에서 채움 비율이 0.799~0.953으로 맞춤 범위(0.75~0.98) 안에
들었습니다. Icebreaker는 4번에서 설명하는 최소 배율 맵입니다.

### 3. Online: 휠로 확대하면 지형이 화면 밖으로 밀려남

**증상**: 지형 바깥에 커서를 두고 확대하면 지형이 화면 밖으로 사라졌습니다.

**원인**: 휠은 커서 자리를 고정점으로 삼아 확대하므로 지형 바깥에서 확대하면 지형이 밀려납니다.
main과 1차 작업물 모두 끌기만 막고, 휠에서는 `userTookOver = true`만 기록했습니다. 창 크기 변경도
처리하지 않았습니다.

**수정**: 사용자의 휠(`isTrusted`, Alt 휠 제외)과 창 크기 변경 뒤에 `correctLater()`가 범위를 벗어난
만큼 되돌립니다. 이동은 사이트 panzoom의 `moveBy`를 부르는 `panBy()`로 해서 캔버스와 마커가 함께
움직입니다.

```js
requestAnimationFrame(function () { requestAnimationFrame(function () {
    var bounds = readBounds(false);
    var dx = overflow(bounds.x, bounds.minX, bounds.maxX);
    var dy = overflow(bounds.y, bounds.minY, bounds.maxY);
    if (Math.abs(dx) < 1 && Math.abs(dy) < 1) return;
    panBy(dx, dy);
}); });
```

측정은 두 프레임 뒤에 합니다. 처음에 한 프레임 뒤에 쟀더니 보정이 한 칸씩 늦었습니다. 캡처 단계의
처리기가 사이트보다 먼저 프레임 콜백을 예약하고 브라우저는 예약 순서대로 실행하므로, 한 프레임
뒤에는 panzoom이 아직 새 변환을 쓰기 전이기 때문입니다. 기록한 보정량이 그 휠이 아니라 바로 앞 휠의
값과 같은 것으로 확인했습니다.

**확인**: 재현 페이지에서 지형 밖 6번 연속 확대 뒤에도 지형이 화면 가운데를 덮었습니다. 실제 사이트
12개 맵에서 지형 밖 5번 확대 뒤 캔버스 픽셀을 따로 읽어 화면 가운데가 지형인지 확인했습니다.

### 4. Online: 맞춤이 위치 마커를 화면 밖으로 밀어냄, 최소 배율에서 배율이 올라감

**증상**: 새로 만든 실제 사이트 검사에서 Icebreaker만 실패했습니다. 위치를 보낸 직후 정중앙에 있던
마커가 몇 초 뒤 화면 위쪽 밖(y = -3)으로 나갔습니다.

**원인**: 두 문제가 겹쳤습니다.

- 실행 순서: 앱은 페이지가 열리면 위치부터 보내고(`MaintainPositionAsync`) 그 뒤에 맞춤 스크립트를
  넣습니다. 맞춤의 재확인(`verifyFit`)은 그림 중심이 20px 넘게 어긋나면 그림 가운데로 다시 옮겼는데,
  위치 때문에 옮긴 화면을 어긋남으로 보고 되돌렸습니다. 지형이 창보다 작은 맵에서는 마커가
  가장자리에 남아 드러나지 않았고, 최소 배율에서도 지형이 창보다 큰 Icebreaker(채움 1.808)에서
  화면 밖으로 나갔습니다.
- 가운데 맞추기 방식: 위치를 휠 두 칸(서로 다른 자리에서 0.8배 축소와 1.25배 확대)으로 옮겼습니다.
  최소 배율(0.12)에서는 축소 칸이 막히고 확대 칸만 적용되어 배율이 한 칸 올라가고(채움
  1.808 -> 2.26) 이동량도 틀어졌습니다.

**수정** (`map-keep-visible.js` 판 9, `pilot-bridge.js`)

- 휠 두 칸 이동(`shiftContent`, `PAIR_SHIFT_FACTOR`)을 지우고 가운데 맞추기도 `panBy()`로 합니다.
  `panzoom.moveBy`는 배율을 건드리지 않습니다.
- 맞추기가 사이트의 배율 한계에 닿으면 `zoomLimited`를 켭니다. 재확인은 그 상태에서 채움 비율을
  이유로 다시 맞추지 않습니다.
- 위치가 표시된 동안은 화면 가운데를 위치가 정합니다. 맞추기의 모든 갈래가 끝나는 `centerStep()`이
  `revealPosition('fit')`을 부르고, 재확인은 그림 중심의 어긋남을 따지지 않습니다.

```js
var positionShown = hasPosition();
var refit = !zoomLimited && (!bounds.complete || fill < FIT_MIN || fill > FIT_MAX);
var recenter = !positionShown
    && (Math.abs(shift.x) > FIT_CENTER_TOLERANCE || Math.abs(shift.y) > FIT_CENTER_TOLERANCE);

if (refit) fitStep(FIT_MAX_STEPS);
else if (recenter) centerStep(CENTER_MAX_TRIES);
else revealPosition('verify');
```

- 브리지에 `hasPosition()`과 `revealPosition()`을 공개해, 두 스크립트가 "마커가 화면 안쪽에
  있는가"라는 같은 판정을 씁니다.

**확인**: 재현 페이지에 "최소 배율에서도 지형이 창보다 크고, 위치가 맞춤보다 먼저 표시된 경우"
사례를 추가했습니다. 이전 스크립트는 실제 증상 그대로 실패하고(마커가 화면 밖, 배율 0.5에서
0.625로 상승) 수정본은 통과합니다. 실제 사이트 Icebreaker에서 위치를 먼저 보낸 뒤 맞춤을 넣어도
마커가 정중앙에 남고, 그 뒤 화면이 더 움직이지 않았습니다.

### 5. Online: 맵을 열 때마다 "바닥 맵이 그려지지 않았다"는 거짓 경보

**원인**: `page-health.js`가 `svg.svg-map`이 있는지로 바닥 맵을 판정했는데 지금 사이트에는 이 요소가
없습니다. 그래서 모든 Online 로드에서
`[PageHealth] ... baseMap=False <- 바닥 맵이 그려지지 않았다`가 남아 진단을 오도했습니다.

**수정**: 표시된 `canvas.doc-map-canvas`에 불투명 픽셀이 있는지로 판정하는 `baseMapDrawn()`을 넣고,
마커 층은 `.map-cont canvas.markers-canvas`로 봅니다.

**확인**: 실제 사이트 Customs와 Icebreaker에서 `baseMap: true, markerLayer: true`를 보고했습니다.
같은 페이지에서 예전 기준(`svg.svg-map`)은 false였습니다.

### 6. Local: 미니맵이 기존 화면과 다르고 조작 UI가 사이트와 무관하게 설계됨

**원인**: 1차 작업물의 미니맵(`viewer/`)은 사이트 화면을 옮기지 않고 새로 디자인했습니다.
"Tanuki local map / 오프라인 전술 지도" 머리글, "레벨 표시 LAYERS", "마커 표시 MARKERS",
"게임 좌표 POSITION" 입력 폼으로 된 자체 계기판이었고 색, 아이콘, 마커도 사이트와 달랐습니다.

**수정**: 사이트 지도 페이지의 구조와 값을 옮겨 다시 만들었습니다.

- `index.html`: 사이트와 같은 요소와 클래스(`.map-cont.bg-grid`, `.map-wrap`, `.markers-canvas`,
  `.squad-layer > .marker`, `.panel_right`의 Levels 패널, Alt 휠 안내)
- `style.css`: 사이트 스타일시트의 값(격자 배경, Levels 패널, 라디오 테두리 `#9a8866`, 마커 크기
  애니메이션)
- `markers.js`: 사이트 마커 캔버스의 그리기 규칙. 탈출구 아이콘 경로, 진영 색(`pmc`와 `transit`은
  `#70a800`, `scav`는 `#aeaeb0`), 세 겹 그림자, 라벨 겹침 처리, 다른 층 표시를 옮겼습니다. 내 위치는
  사이트의 보라색 `.marker`에 앱의 방향 삼각형(`fill="#8a2be2" stroke="#70a800"`)을 붙였습니다
- `camera.js`(신규): 사이트 panzoom의 휠 배율 식과 끌기 감각에, 지형이 화면 가운데를 덮게 하는
  규칙(`MARGIN_RATIO = 0.1`)과 창의 90% 맞춤(`FIT_RATIO = 0.9`)을 더했습니다
- 오버레이: `.controls-hidden`으로 시작해 조작 UI를 숨기고, 상단바의 "UI 요소 숨기기"를 끄면
  `setControlsVisible(true)`로 보입니다
- 층 자동 선택: 위치의 높이로 층 구역(`zones`)을 먼저, 그다음 높이 범위(`height`)를 봅니다(`levelAt()`)

**확인**: `verify-viewer.mjs` 72/72 통과. 12개 맵마다 좌표식이 사이트가 계산한 값과 같은지, 마커가
SVG의 그 지점에 그 방향으로 그려지는지, 카메라, 오버레이, 층, 진영 색을 봅니다.

### 7. Local: 1차 작업물의 과한 구조

**원인**

- `LocalMapBundle`: 앱이 시작할 때 모든 리소스의 SHA-256을 계산하고 SVG를 XML로 파싱해 메모리에 들고
  응답했습니다. CI 검사와 같은 일을 사용자 PC에서 반복했고, 해시를 맞추려고 `.gitattributes`에
  `resources/** -text`까지 두었습니다.
- `ScreenshotDelivery`와 버전 프로토콜(`requestId`, `queued`, `getPositionStatus`): 이미
  `WebBrowserViewModel`에 있는 최신 입력 보관과 재전달을 Local용으로 한 벌 더 만든 것이고, 이를
  검사하는 `TanukiTarkovMap.HostTests` 프로젝트까지 있었습니다.
- 리소스 봉인(`resource-bundle.mjs`의 `seal`과 `verify`)과, 쓰지 않는 코드를 남겨 둔 `<Compile Remove>`

**수정**

- `Models/Offline/LocalViewer.cs`(신규, 정적 클래스): Local 전용 메모리 `RequestContext`에 CefSharp의
  `FolderSchemeHandlerFactory`를 등록해 `https://tanuki-map.local/`을 실행 파일 옆 `LocalMap` 폴더로
  응답합니다. 폴더가 없으면 앱을 멈추지 않고 로그만 남깁니다.

```csharp
var context = new RequestContext(new RequestContextSettings { CachePath = string.Empty });
if (Directory.Exists(Root))
    context.RegisterSchemeHandlerFactory("https", Host, new FolderSchemeHandlerFactory(Root, hostName: Host));
else
    Logger.SimpleLog($"[LocalViewer] Viewer folder missing: {Root}");
```

- `WebBrowserViewModel`: Local 페이지의 로드 끝(`OnFrameLoadEnd`)에서는 주입 없이
  `ApplyZoomLevel` -> `ApplyUIVisibilityAsync` -> `ApplyExtractionFilterAsync` -> `MaintainPositionAsync`만
  실행합니다. 위치 전달은 Online과 같은 `MaintainPositionAsync`가 맡고, Local에서는
  `tanukiViewer.showScreenshot`과 `isRendered`를 부릅니다. 최신 입력은 주소 대신 맵 이름
  (`MapInfo.Name`)에 묶어 모드를 바꿔도 이어집니다.
- `TanukiTarkovMap.csproj`: `viewer/`와 `resources/`를 `LocalMap\viewer`, `LocalMap\resources`로 출력에
  포함합니다.
- `resource-bundle.mjs`에는 구조 검사(`check`)만 남겼습니다.
- 삭제: `LocalMapBundle.cs`, `LocalMapRequestHandlerFactory.cs`, `ScreenshotDelivery.cs`,
  `TanukiTarkovMap.HostTests/`, `viewer/protocol.js`, `viewer/resources.js`, `viewer/map-view.js`

### 8. 설정: 확인 없이 모든 사용자를 Local로 옮기는 이전

**원인**: 1차 작업물이 `AppSettings.MigrateViewerSettings()`와 기본값 `LocalMapEnabled = true`,
`LocalMapModeActive = true`를 넣어 기존 사용자까지 한 번 Local로 옮겼고, CLAUDE.md의 로컬 모드
정의도 "비상 경로"에서 "기본 경로"로 바꿨습니다. 새 Local은 실제 앱에서 확인한 적이 없으므로 "코어에
닿는 변경은 동작 근거 없이 넣지 않는다"는 프로젝트 규칙에 어긋납니다.

**수정**: `DataTypes.cs`와 `Settings.cs`를 main 동작으로 되돌렸습니다(기본값 false, 이전 로직 삭제).
Local은 설정에서 켜는 실험적 기능으로 남습니다. CLAUDE.md도 원래 정의로 되돌리고 구현 방식을
설명하는 한 문단만 더했습니다.

### 9. 쓸모가 없어진 사이트 사본과 깨진 도구

**원인**: 새 Local은 사이트 사본(`archive/`)을 읽지 않는데 사본과 그 도구가 남아 있었고, 사본 README는
이미 지운 `MapArchive.cs`를 설명했습니다. 여러 도구는 이미 동작하지 않았습니다.

- `verify-coordinates.mjs`, `verify-directions.mjs`: 사라진 `window.pilot.position`에 의존
- `verify-resources.mjs`: 지운 `viewer/resources.js`와 옛 함수명 `parseScreenshotPosition`을 참조
- `extract-resources.mjs`: 옛 형식의 리소스를 생성해 새 검사에 실패
- `verify-archive.mjs --local-core`: 지운 Local 코어 UI를 검사

**수정**: `archive/`, `archive-maps.mjs`, `extract-resources.mjs`, `verify-archive.mjs`와 위 도구들,
`MapArchive.cs`, `ArchiveResourceRequestHandlerFactory.cs`, `web-elements-control.js`의 Local 코어
규칙과 `setLocalMapMode`, `verify-map-recovery.mjs`의 `--archive` 경로를 지웠습니다. 좌표와 방향
대조는 수집할 때 사이트가 계산한 값(`meta.source.coordinateChecks`, `manifest.screenshotChecks`)을
`verify-viewer.mjs`가 기준으로 쓰는 방식으로 대신합니다. 지운 파일은 휴지통과 git(main, `27dd3d9`)에
남아 있습니다.

### 10. 지도 데이터 수집이 특정 브라우저 도구에 묶였고 headless에서 실패함

**원인**: 1차 작업물의 수집기는 유지보수자의 Orca 브라우저 탭이 있어야 돌았습니다. headless Chrome으로
바꾸자 두 가지에 막혔습니다. User-Agent에 `HeadlessChrome`이 있으면 Cloudflare 확인 화면에서 멈췄고,
`navigator.webdriver`가 true이면 페이지와 캔버스는 그려도 사이트가 지도 데이터 상태(`$squestsState`)를
채우지 않았습니다.

**수정**: `tools/headless-chrome.mjs`(신규)가 임시 프로필과 자동 포트(`--remote-debugging-port=0`)로
Chrome을 띄웁니다. 실제 사이트에 접속할 때는 `Emulation.setUserAgentOverride`로 UA의
`HeadlessChrome`을 지우고 `--disable-blink-features=AutomationControlled`로 띄웁니다.
`collect-map-docs.mjs`는 이 실행기를 쓰고, 캔버스가 그려지고 `$squestsState.map === mapId`가 될 때까지
기다립니다.

### 11. 앱 없이 지금 사이트를 진단할 수단이 없음

**수정**: `tools/verify-online.mjs`(신규)는 앱(`WebBrowserViewModel.OnFrameLoadEnd`)과 같은 스크립트를
같은 순서로 실제 사이트에 넣습니다. 불필요한 요소 제거, 여백 제거, 방향 표시, 브리지, 스크린샷 전송,
맞춤, UI 숨김 순서입니다. 맵마다 다음을 판정하고 실패하면 맞춤 기록을 함께 출력합니다.

- 입력 경로(`tanukiPilot.input()`)와 실패 사유
- 마커와 방향 삼각형이 화면 안에 보이는지
- 맞춤 채움 비율. 최소 배율 맵이면 사이트 배율이 `meta.json`의 `minZoom`인지
- 휠 확대 뒤 지형 픽셀이 화면 가운데를 덮는지

4번 문제를 이 도구로 찾았습니다. 네트워크와 사이트의 봇 확인에 좌우되므로 CI에는 넣지 않았습니다.

### 12. CI와 빌드 스크립트가 지운 도구와 봉인 검증을 실행함

**수정**

- `.github/workflows/verify-map-recovery.yml`: `resource-bundle.mjs check`, `test-resource-pipeline.mjs`,
  `verify-map-recovery.mjs`, `verify-map-keep-visible.mjs`(새로 연결), `verify-viewer.mjs`, 빌드
- `.github/workflows/release.yml`: publish 뒤 `verify-viewer.mjs --root publish/LocalMap`으로 패키지에서
  미니맵 파일이 빠지지 않았는지 확인
- `build.bat`: 시작 전 리소스 검사, publish 뒤 같은 미니맵 검사

### 13. 문서가 지운 구조와 바뀐 기본값을 설명함

**수정**: AGENTS.md의 스크린샷 추적(파일명 형식, 흐름, 로그와 진단 순서), 지도 맞춤과 줌, Local
미니맵, 리소스 갱신 장을 구체적으로 다시 썼습니다. PROJECT.md의 다이어그램과 절(`LocalViewer`,
`MapKeepVisible`), README, Pilot 브리지 문서(2026-10-07 변경), 로컬 뷰어 설계 문서(1차 작업물의 별도
문서를 합침)를 갱신했고, 이번에 검증한 기법을 임베디드 웹페이지 제어 레퍼런스에 더했습니다.

## 검증 결과

| 검사 | 결과 | 보는 것 |
|---|---|---|
| `verify-online.mjs` | 12/12 맵 통과 | 실제 사이트의 위치 입력, 마커와 방향, 맞춤, 휠 확대 뒤 지형 |
| `verify-map-recovery.mjs` | 12/12 통과 | 사이트 판별 위치 입력 경로와 복구 (재현 페이지) |
| `verify-map-keep-visible.mjs` | 9/9 통과 | 맞춤, 끌기와 휠 제한, 숨은 캔버스, 최소 배율에서 위치 유지 |
| `verify-viewer.mjs` | 72/72 통과 | Local 미니맵 12개 맵 |
| `resource-bundle.mjs check` | 통과 | 리소스 계약 |
| `test-resource-pipeline.mjs` | 7/7 통과 | 잘못된 지도 문서와 파일의 거절 |
| `dotnet build` | 오류 0 | 경고는 main부터 있던 nullable 경고뿐 |

실제 WPF 앱은 프로젝트 지침에 따라 실행하지 않았습니다.

## 남은 일

- Debug 빌드 실행 확인: Online에서 스크린샷으로 위치와 방향이 바뀌는지, Local 전환 뒤 지도와 위치가
  이어지는지, "UI 요소 숨기기"를 끄면 Local에서 Levels 패널이 보이는지
- 결정 필요
  - Local을 기본 모드로 올릴지 (앱 확인 뒤 판단 권장)
  - 지도 데이터를 자동 수집해 앱에 담아 배포하는 것이 사이트 이용약관에 맞는지
  - Bender 글꼴 포함 여부 (지금은 대체 글꼴로 표시)
  - 사이트의 장소 이름 라벨 포함 여부
