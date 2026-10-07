# 프로젝트 에이전트 지침

일반 개발 규칙은 [CLAUDE.md](CLAUDE.md)를 함께 확인합니다.

## 릴리스 내역 작성

<!--
INTENT
릴리스 내역이 빠지지 않게 작업 지침에서 전용 스킬로 연결한다.
템플릿과 작성 기준을 중복 관리하지 않도록 이 파일에는 사용 지시만 둔다.
-->

새 릴리스를 준비하거나 기존 릴리스 설명을 수정할 때는 프로젝트 내장
[release-notes 스킬](.agents/skills/release-notes/SKILL.md)을 읽고 그 작성 기준에 따라
버전과 배포 범위, 내역의 구성과 설명 깊이를 정합니다.

## 스크린샷 위치 추적

<!--
INTENT
위치 표시는 이 앱의 코어이고 사이트가 배포할 때마다 가장 먼저 깨지는 곳이다. 고장 보고를 받은
작업자가 앱을 실행하기 전에 원인을 좁히도록 흐름과 진단 순서를 여기 모은다. 구현 세부와 값의
목록은 각 파일의 머리 주석과 코드가 원천이므로 이 절에는 경로와 판단 기준만 둔다.
-->

게임 안에서 스크린샷을 찍으면 파일명에 위치와 시선이 기록됩니다. 앱은 이미지를 읽지 않고
파일명만으로 위치와 방향을 표시합니다.

```
2026-09-09[14-14]_179.10, 3.29, -717.65_0, 0.70710678, 0, 0.70710678_15.67 (0).png
```

첫째와 셋째 수가 평면 좌표, 둘째 수가 높이이고, 그다음 네 수가 시선 쿼터니언(x, y, z, w)입니다.
메뉴나 은신처에서 찍은 파일에는 좌표가 없어 추적하지 않습니다. 감시 폴더는
[App.ScreenshotsFolder](src/TanukiTarkovMap/App.xaml.cs)이고 설정이나 자동 탐색으로 정해집니다.
보통 `문서\Escape from Tarkov\Screenshots`이며, 개인 경로를 코드나 문서에 적지 않습니다.

1. [ScreenshotsWatcher](src/TanukiTarkovMap/Models/FileSystem/ScreenshotsWatcher.cs)가 새 파일을
   감지해 [MapEventService](src/TanukiTarkovMap/Models/Services/MapEventService.cs)에 알립니다.
2. [WebBrowserViewModel](src/TanukiTarkovMap/ViewModels/WebBrowserViewModel.cs)이 파일을 지금 열려
   있거나 열리려는 맵에 묶어 최신 입력 하나로 보관하고, 페이지 로드 직후와 주기 점검
   (`MaintainPositionAsync`)에서 표시될 때까지 다시 보냅니다.
3. Online은 [pilot-bridge.js](src/TanukiTarkovMap/Models/JavaScript/Scripts/pilot-bridge.js)의
   `window.tanukiPilot.sendScreenshot`이 사이트의 위치 입력 경로로 넘기고, 방향 삼각형은
   [map-markers.js](src/TanukiTarkovMap/Models/JavaScript/Scripts/map-markers.js)가 그립니다.
   Local은 미니맵의 `window.tanukiViewer.showScreenshot`이 직접 그립니다.

성공은 호출이 예외 없이 끝난 것이 아니라 그 위치의 마커가 지도에 실제로 있는 것으로 판정합니다.
브리지는 사이트 지도 객체의 `playerPos`가 보낸 좌표와 같고 마커 요소가 있을 때만 성공을 돌려줍니다.
새 위치가 화면 밖이나 가장자리에 있으면 그 위치를 가운데로 옮기지만, 주기 점검은 화면을 옮기지
않습니다. 사용자가 지도를 옮겨 마커가 화면 밖에 있어도 표시된 것입니다.

사이트는 배포 때마다 위치를 받는 함수를 바꾸거나 없앴습니다. 브리지는 입력 경로마다 어댑터를 두고
페이지에 실제로 있는 첫 경로를 쓰며, 함수 이름이 아니라 함수와 상태가 있는지로 판별합니다. 사이트의
프로그램용 입력이 모두 사라진 판에서는 사용자가 파일명을 붙여 넣는 "Where am i" 입력의 처리기를
렌더 중인 Vue 트리의 컴포넌트 props에서 찾아 씁니다. 번들을 압축해도 템플릿의 prop 이름은 남기
때문입니다. 경로 목록과 판별 조건은 브리지의 `inputs`가 원천이고, 지금 쓰는 경로는
`tanukiPilot.input()`으로 확인합니다. 사이트가 입력을 또 바꾸면 어댑터를 더하고 그 판의 재현
페이지를 [verify-map-recovery.mjs](tools/verify-map-recovery.mjs)에 추가합니다.

### 위치가 바뀌지 않을 때

1. 앱 로그에서 `[PilotBridge]`(Online)나 `[LocalViewer]`(Local) 줄을 봅니다. 표시되면
   `Position rendered (screenshot: <파일명>)`, 못 하면 `Screenshot pending: <상태> (<파일명>)`이
   남습니다. 이 줄이 없으면 감지나 맵 판정 단계이므로 `[ScreenshotsWatcher]`와 `[MapEventService]`
   줄을 봅니다. 상태 값은 브리지의 `lastFailure`와 미니맵의 `state.status`가 원천입니다.
   `pilot-unavailable`은 사이트에서 위치 입력 경로를 하나도 찾지 못했다는 뜻이고, 대부분 사이트
   배포로 경로가 바뀐 경우입니다.
2. 앱 없이 지금 사이트에서 Online 경로를 확인합니다. `node tools/verify-online.mjs --maps customs`는
   앱과 같은 스크립트를 같은 순서로 넣고 입력 경로, 마커와 방향 삼각형, 맞춤, 휠 확대 뒤 지형을
   맵마다 판정합니다. 맵을 생략하면 전체를 검사합니다.
3. 실행 중인 앱의 상태가 필요하면 사용자에게 Debug 빌드 실행을 요청하고 CLAUDE.md의 CDP 절차로
   `node tools/cdp-debug.mjs eval "tanukiPilot.status()"`처럼 확인합니다.

## 지도 맞춤과 줌

<!--
INTENT
작은 오버레이 창에서는 지도가 화면 밖으로 사라지면 위치를 알려 줄 수 없다. 사이트 구조가 바뀌면
맞춤이 오류 없이 멈출 수 있고, 맞춤과 위치 표시가 서로 화면을 옮기면 마커가 밀려난다. 그래서
둘이 지킬 규칙을 한곳에 적는다.
-->

지도를 열면 실제 지형이 창을 채우게 맞추고, 끌거나 확대한 뒤에도 화면 가운데에는 언제나 지형이
있게 합니다. 지형은 지도 좌표 공간 전체(`.map-wrap` 상자, SVG `viewBox`)가 아니라 실제로 그려진
범위입니다. 좌표 공간은 여백까지 포함해 지형보다 훨씬 크므로, 상자를 기준으로 막으면 상자가 가운데를
덮은 채 지형만 화면 밖으로 나갑니다. CEF 페이지 줌(상단바의 배율)과 지도 배율은 서로 다른 값입니다.

Online은 [map-keep-visible.js](src/TanukiTarkovMap/Models/JavaScript/Scripts/map-keep-visible.js)가
사이트 지도 위에서 이 규칙을 지킵니다.

- 지형은 화면에 표시된 `canvas.doc-map-canvas`의 불투명 픽셀로 잽니다. 같은 클래스의 캔버스가 둘이고
  사이트가 배율에 따라 하나만 보이므로, DOM 순서가 아니라 표시 상태로 고르며 표시된 후보가 여럿이면
  재지 않습니다. 재지 못하면 `.map-wrap` 상자로 대신하지 않고 맞춤과 이동 제한을 쉽니다. 대신하면
  맞춤이 틀린 채로 정상 종료한 것처럼 보입니다.
- 배율은 사이트 휠로 한 칸씩, 위치는 사이트 panzoom의 `moveBy`로 옮깁니다. 사이트의 최소나 최대
  배율에 닿으면 그 자리에서 맞춤을 멈춥니다. 최소 배율에서도 지형이 창보다 큰 맵이 있습니다.
- 끌기는 이동 이벤트를 줄여 범위 안에서 막고, 휠 확대와 창 크기 변경은 사이트가 변환을 쓴 다음
  프레임에 `moveBy`로 되돌립니다. 휠은 커서 자리를 기준으로 확대하므로 지형 밖에서 확대하면
  지형이 밀려납니다.
- 내 위치가 표시되면 화면 가운데는 위치가 정합니다. 앱은 맵을 열자마자 위치를 보내고 맞춤은 그
  뒤에 돌므로, 맞춤이 끝날 때마다 브리지의 `revealPosition`으로 마커를 화면 안쪽에 둡니다.
- 지도 요소의 `transform`을 직접 쓰지 않습니다. 사이트가 캔버스와 마커를 각자 다시 그려 두 층이
  어긋납니다. 합성 드래그로 옮기지 않습니다. 마커 위에서 시작한 드래그는 사이트가 지도 이동을
  끕니다. 휠 두 칸(축소와 확대)으로 위치를 옮기지 않습니다. 최소 배율에서는 축소 칸이 막혀 배율까지
  바뀝니다.

Local은 [camera.js](viewer/camera.js)가 같은 규칙을 직접 구현합니다. 지형 범위는 배경을 뺀 SVG
바운딩 박스이고, 배율 한계는 각 맵 `meta.json`의 `minZoom`과 `maxZoom`이며, 모든 이동과 확대 뒤에
같은 범위 조건을 적용합니다. 휠 한 칸의 배율은 사이트와 같고 Alt 휠은 층 전환입니다.

맞춤과 줌을 고치면 다음을 통과시킵니다. headless 검사는 실제 WPF 창의 크기 변경, 포커스와 모드
전환 때의 브라우저 교체를 검증하지 않으므로, 그 부분은 사용자에게 Debug 빌드 확인을 요청합니다.

```
node tools/verify-map-keep-visible.mjs   Online 맞춤과 제한, 작은 재현 페이지 (CI)
node tools/verify-viewer.mjs             Local 미니맵 전체, 카메라 포함 (CI)
node tools/verify-online.mjs             지금 사이트에서 Online 경로 (네트워크 필요, CI 제외)
```

## Local 미니맵

<!--
INTENT
Local은 사이트가 바뀌거나 죽어도 코어를 지키는 경로이지만, 사용자에게는 같은 앱의 다른 화면일 뿐이다.
자체 디자인으로 흐르면 낯선 화면이 되어 쓰이지 않는다. 화면은 사이트를 따르고 기능만 코어로 줄인다.
-->

설정의 "로컬 맵 사용"(실험적 기능)을 켜면 상단바에 Online/Local 전환이 생깁니다. Local은 사이트
코드를 실행하지 않고 앱에 담긴 지도 리소스(`resources/`)를 미니맵(`viewer/`)이 그립니다. 앱은
[LocalViewer](src/TanukiTarkovMap/Models/Offline/LocalViewer.cs)가 만든 전용 브라우저 저장 공간에서
`https://tanuki-map.local/`로 이 폴더를 열고, `window.tanukiViewer`의 함수만 부릅니다.

- 화면은 사이트의 지도 화면을 따릅니다. 요소 구조와 클래스 이름, 색, 탈출구 아이콘, 위치 마커를
  사이트의 CSS와 번들에서 옮겼습니다. 고칠 때는 지금 사이트의 DOM과 CSS를 먼저 확인하고 그 값을
  따르며, 임의의 디자인으로 바꾸지 않습니다.
- 오버레이 미니맵이므로 조작 UI(Levels 패널, Alt 휠 안내)는 기본으로 숨기고, 상단바의 "UI 요소
  숨기기"를 끄면 보입니다(`setControlsVisible`). 로딩 실패 안내와 위치 마커는 숨기지 않습니다.
- 층은 위치의 높이로 자동으로 고릅니다. 각 층의 구역(`zones`)이 먼저이고 그다음 높이 범위입니다.
- 리소스를 읽지 못하면 미니맵이 오류를 표시합니다. Online으로 자동 전환하지 않습니다.
- 퀘스트 마커는 넣지 않습니다. 이유는 CLAUDE.md의 로컬 모드 절에 있습니다.

## 지도 리소스 갱신

<!--
INTENT
리소스는 사이트 코드가 아닌 데이터라서, 사이트가 입력 함수나 렌더러를 바꿔도 Local은 그대로 돈다.
지도 자체가 바뀌었을 때만 갱신하고, 갱신이 기존 정상 리소스를 덮어쓰지 않게 단계마다 새 폴더를 쓴다.
-->

`resources/`는 사이트의 지도 문서를 변환한 SVG와 JSON입니다. 스크립트나 외부 참조를 담지 않습니다.
사이트의 지도가 바뀌었을 때만 아래 순서로 갱신합니다. 각 단계는 비어 있는 새 폴더에 쓰고 기존
폴더를 덮어쓰지 않습니다.

```
node tools/collect-map-docs.mjs --out <빈 폴더>
node tools/build-map-resources.mjs --input <수집 폴더> --out <새 후보 폴더>
node tools/verify-map-docs.mjs --input <수집 폴더> --resources <후보 폴더> --report <새 보고서 폴더>
```

보고서에서 층마다 사이트 렌더러와의 차이를 확인한 뒤 후보로 `resources/`를 교체하고,
`node tools/resource-bundle.mjs check`와 `node tools/verify-viewer.mjs`를 통과시킵니다. 수집기는
실제 사이트를 headless Chrome으로 엽니다. 사이트는 User-Agent에 HeadlessChrome이 있으면 Cloudflare
확인 화면에서 멈추고, 자동화 표시(`navigator.webdriver`)가 켜진 브라우저에는 지도 데이터를 채우지
않습니다. 그래서 실제 사이트에는 두 표시를 끄는 [headless-chrome.mjs](tools/headless-chrome.mjs)로만
접속합니다.
