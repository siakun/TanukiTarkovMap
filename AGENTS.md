# TanukiTarkovMap 프로젝트 지침

- 이 파일이 담당하는 것: 이 저장소에서 작업하는 에이전트가 지킬 프로젝트 규칙입니다. 코어와 우선순위,
  빌드와 코드 규칙, 브랜치 전략, 기능별 작업 기준, 문서 관리 원칙과 사용자 선호를 담습니다.
- 위치만 참조하는 것: 검사 목록과 실행 방법은 [TESTING.md](TESTING.md), 앱 구조는 [PROJECT.md](PROJECT.md),
  설계 근거는 `docs/`, 릴리스 내역 작성 기준은 내장 [release-notes 스킬](.agents/skills/release-notes/SKILL.md)에
  있습니다.
- 담지 않는 것: 커밋 메시지, 작성자 표기, 푸시처럼 사용자의 모든 저장소에 공통인 규칙은 사용자 전역
  지침이 맡고, 위 참조 문서들의 본문은 그 문서에 둡니다.

## 이 프로젝트의 코어

**레이드 중인 사용자에게 지금 어디에 있고 어디를 보고 있는지 지도로 알려 주는 것.** 이 앱은 그것
하나를 위해 존재하고 나머지는 전부 그 주변입니다.

운전 중에 내비게이션이 꺼지는 상황을 생각하면 됩니다. 초보 운전자에게 그것은 불편이 아니라
비상입니다. 레이드도 같습니다. 총소리가 나는 상황에서 위치 표시가 멈추면 사용자는 아무것도 할 수
없습니다. 그래서 이 기능은 느려져서도 안 되고, 조용히 멈춰서도 안 됩니다.

### 우선순위

1. **현재 위치와 바라보는 방향** - 절대 고장나면 안 되는 코어입니다
2. **지도와 탈출구** - 위치를 알아도 나갈 곳을 모르면 쓸모가 없습니다. 상단바의 `PMC`/`SCAV`
   버튼이 여기에 속합니다. 진영마다 쓸 수 있는 탈출구가 다르므로 이 구분은 부가 기능이 아니라
   코어의 일부입니다
3. **그 밖의 모든 것** - 퀘스트, 키, 루트, 그리기, 레이어, 시세는 있으면 좋은 부가 기능입니다.
   없어도 이 앱의 목적은 달성됩니다

### 여기서 나오는 판단 기준

- 부가 기능 때문에 코어가 느려지거나 깨질 위험이 생기면 **부가 기능을 버립니다**. 반대로 하지
  않습니다
- 코어에 닿는 변경은 실제로 동작하는지 확인한 근거 없이 넣지 않습니다. "빌드가 된다"는 근거가
  아닙니다
- 코어가 끊긴 것을 발견하면 그 시점의 다른 모든 작업보다 앞섭니다
- 사용자가 코어의 이상을 말하면 재현부터 합니다. 재현하지 못한 채로 고쳤다고 하지 않습니다

### 로컬 모드가 존재하는 이유

사이트가 패치되어 위치 표시 경로가 끊기거나 사이트 자체가 죽어도 **코어가 계속 돌게 하려는
비상 경로**입니다. 2026-08-17에 tarkov-market이 Pilot v2를 배포하면서 앱의 위치 전달이 하루아침에
끊긴 적이 있습니다. 그때 사용자는 앱을 열어도 자기 위치를 볼 수 없었습니다.

그러므로 로컬 모드의 목표는 사이트를 복제하는 것이 아니라 **지형, 탈출구, 내 위치와 방향을 확실히
보여 주는 것**입니다. 퀘스트 마커는 넣지 않습니다. 자주 바뀌어 갱신 부담만 크고, 위 우선순위에서
3순위이기 때문입니다.

Local은 사이트 코드를 실행하지 않습니다. 사이트에서 받은 지도 데이터(`resources/`)를 앱의
미니맵(`viewer/`)이 그리므로, 사이트가 입력 함수나 렌더링 구조를 바꿔도 Local의 위치 표시는 끊기지
않습니다. 스크린샷 추적, 지도 맞춤, Local 화면과 리소스 갱신의 작업 기준은 이 문서의 기능별 절,
구조와 리소스 계약은 [로컬 뷰어 설계](docs/20260821-local-viewer-design.md)를 확인합니다.

## 프로젝트 정보
- **구조**: WPF MVVM 패턴
- **타겟 프레임워크**: .NET 8.0
- **주요 기술**: WPF, CefSharp (CefSharp.Wpf.NETCore)
- **솔루션 경로**: `src/TanukiTarkovMap.sln`

## 빌드와 테스트

```bash
cd src && dotnet build
```

**에이전트는 빌드는 해도 앱은 실행하지 않습니다.** 실행해야 확인되는 것은 사용자에게 Debug 빌드 실행을
요청합니다. 무엇을 고쳤을 때 어떤 검사를 돌리는지와 앱에서 직접 확인할 항목은 [TESTING.md](TESTING.md)에
있습니다.

## 프로젝트 선호사항
- WPF의 순수한 MVVM 디자인 패턴으로 개발하는것을 선호
- WinForm을 사용하지 않는것을 선호

## MVVM 패턴 및 Code-behind 금지 원칙

**Code-behind(*.xaml.cs)에 로직을 추가하지 마세요.**

- View의 Code-behind는 `InitializeComponent()` 호출만 포함해야 함
- 이벤트 핸들러, UI 조작 로직은 **절대** Code-behind에 작성하지 않음
- UI 인터랙션이 필요한 경우 `Microsoft.Xaml.Behaviors.Wpf`의 **Behavior**를 사용
- 데이터/비즈니스 로직은 **ViewModel**에서 처리
- Command 바인딩으로 버튼 클릭 등 처리

### 올바른 패턴
```
View (XAML)
  ├── DataContext → ViewModel (데이터 바인딩)
  └── Behaviors (UI 인터랙션)
```

### 기존 Code-behind 발견 시
Code-behind에 로직이 있는 파일을 발견하면:
1. 해당 내용을 사용자에게 보고
2. Behavior 또는 ViewModel로 리팩토링 제안
3. 수정 작업 항목으로 등록

### 참고 예시
- `Behaviors/HotkeyInputBehavior.cs` - 키 입력 캡처 Behavior
- `ViewModels/SettingsViewModel.cs` - 설정 페이지 ViewModel

## 브랜치 전략: GitHub Flow

이 저장소는 GitHub Flow를 씁니다. main은 항상 배포 가능한 상태로 두고, 모든 작업은 main에서 딴 작업 브랜치에서 합니다.

전역 규칙의 develop 브랜치 운용은 이 저장소에 적용하지 않습니다. 이 절이 우선합니다. "기본 브랜치 직접 커밋 금지"는 그대로 지키되, 커밋할 곳은 develop이 아니라 그 작업의 브랜치입니다.

작업마다 main에서 브랜치를 따고, 끝나면 PR로 main에 합친 뒤 브랜치를 지웁니다. 오래 사는 브랜치를 두지 않습니다. 다만 문서 오타나 .mailmap 같은 메타데이터처럼 배포 가능 상태를 해칠 수 없는 사소한 변경은 브랜치 없이 main에 직접 커밋해도 됩니다.

커밋 메시지 컨벤션, 작성자 표기, 푸시 정책은 전역 규칙을 그대로 따릅니다. push는 사용자가 직접 합니다.

### GitHub 작업 계정

GitHub 조회와 이슈 및 PR 작성은 로컬 `gh` CLI만 사용합니다. GitHub 커넥터는 사용하지 않습니다.
원격에 기록을 남기기 전에 `gh api user --jq .login`으로 로그인 계정이 `siakun`인지 확인합니다.
다른 계정이면 쓰기 작업을 중단합니다. Git 커밋 작성자 설정과 GitHub 로그인 계정은 별개이므로
`git config user.name`과 `git config user.email`만 확인하고 진행하지 않습니다.

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
   `window.tanukiPilot.showScreenshot`이 파일명을 직접 읽고,
   [map-markers.js](src/TanukiTarkovMap/Models/JavaScript/Scripts/map-markers.js)가 사이트 지도 위에 내
   위치 원과 방향 삼각형을 그립니다. Local은 미니맵의 `window.tanukiViewer.showScreenshot`이 직접
   그립니다. 두 모드의 파일명 해석과 표시 모양은 같습니다.

Online에서 사이트로부터 빌리는 것은 지도 컨테이너와 좌표 변환(`gamePosToMapPos`, `mapPosToScreenPos`)
뿐입니다. 사이트의 위치 입력 경로와 내 위치 그리기는 쓰지 않습니다. 둘은 배포 때마다 바뀌어 위치 표시를
끊어 왔고, 좌표 변환은 그동안 같은 이름으로 남았습니다. 지형과 함께 움직이는 자리는 사이트와 같은 변환으로
구해야 맞으므로 변환을 앱에 따로 구현하지 않습니다. 따로 구현하면 사이트가 식을 바꿀 때 경고 없이 틀린
자리에 그립니다. 사이트의 다른 마커와 지도 기능은 사이트의 것을 그대로 씁니다. 이 결정의 근거와 버린 대안은
[Pilot 연동과 위치 전달 경로](docs/20260817-pilot-bridge.md)에 있습니다.

층은 사이트가 고릅니다. 사이트는 지도 문서에서 층마다 높이 범위와 구역을 받아 두고, 지도 상태의 `playerPos`가
바뀌면 그 위치로 층을 바꿉니다. 그래서 브리지는 위치를 보일 때 같은 좌표를 `playerPos`에도 넣습니다. 사이트는
층 데이터가 도착하기 전에 바뀐 위치로는 층을 고르지 않고 나중에 다시 고르지도 않으므로, 브리지는 층 데이터가
채워질 때까지 기다렸다가 넣습니다. 사이트의 표시 상태는 켜지 않으므로 사이트가 자기 원을 그리지 않고, 층 선택
말고 사이트의 내 위치 기능(스쿼드 공유, 사이트 UI의 위치 표시)에는 반영되지 않습니다. Local은 미니맵이 같은
규칙(구역 먼저, 그다음 높이 범위)으로 직접 고르며, 그 식은
[viewer/coords.js](viewer/coords.js)의 `levelAtPosition`입니다.

성공은 호출이 예외 없이 끝난 것이 아니라 그 위치의 마커가 지도에 실제로 보이는 것으로 판정합니다.
새 위치가 화면 밖이나 가장자리에 있으면 그 위치를 가운데로 옮기지만, 주기 점검은 화면을 옮기지 않습니다.
사용자가 지도를 옮겨 마커가 화면 밖에 있어도 표시된 것입니다. 핑(마커가 커졌다 줄어드는 표시)은 새
스크린샷이 처음 보일 때만 켭니다. 표시가 사라졌다고 판단해 같은 파일을 다시 보낼 때마다 켜면, 새
스크린샷이 없는데도 마커가 커졌다 줄어듭니다. 다른 맵으로 옮기면 그 맵에서 표시한 위치는 끝납니다.

좌표 변환은 사이트의 지도 상태 객체가 가집니다. 2026-10 판부터 사이트는 이 객체를 컴포넌트 props에 두지
않아, 만들어진 뒤에는 닿을 경로가 없습니다. 그래서 앱은 로드 시작 시점(`FrameLoadStart`)에
[map-state-capture.js](src/TanukiTarkovMap/Models/JavaScript/Scripts/map-state-capture.js)를 넣어,
Vue가 지도 상태를 만들며 원본과 프록시를 WeakMap에 등록하는 순간 그 프록시를 기록합니다. 층 데이터 표도 같은
방법으로 기록합니다. 브리지와 맞춤 스크립트는 그 기록에서 화면에 붙은 가장 최근 지도 상태를 사용합니다.
사이트가 지도 상태의 모양이나 좌표 변환을 바꾸면 Online은 위치를 그리지 않고 `map-unavailable`로 알립니다.
그때는 번들에서 지도 상태를 만드는 코드를 다시 찾고 [verify-map-recovery.mjs](tools/verify-map-recovery.mjs)의
재현 페이지를 그 판에 맞춥니다.

### 위치가 바뀌지 않을 때

1. 앱 로그에서 `[PilotBridge]`(Online)나 `[LocalViewer]`(Local) 줄을 봅니다. 표시되면
   `Position rendered (screenshot: <파일명>)`, 못 하면 `Screenshot pending: <상태> (<파일명>)`이
   남습니다. 이 줄이 없으면 감지나 맵 판정 단계이므로 `[ScreenshotsWatcher]`와 `[MapEventService]`
   줄을 봅니다. 상태 값은 브리지의 `lastFailure`와 미니맵의 `state.status`가 원천입니다.
   `map-unavailable`은 위치를 그릴 지도 상태가 없다는 뜻입니다. `capture-missing`이면 기록 스크립트가
   들어가지 않은 것이고, `not-captured`이면 페이지가 지도 상태를 만든 뒤에 들어갔거나 사이트가 지도
   상태의 모양을 바꾼 것입니다. `site-error`는 사이트의 좌표 변환이 예외를 던진 경우이고,
   `position-not-shown`은 마커를 붙였지만 화면에 보이지 않는 경우(지도 영역이 숨겨진 경우 등)입니다.
   층만 바뀌지 않으면 `tanukiPilot.levelSync()`를 봅니다. `state`가 `waiting`이면 사이트의 층 데이터를
   기다리는 중이고, `no-level-data`면 층 데이터 표를 기록하지 못한 것(사이트가 표의 모양을 바꾼 경우)입니다.
2. 앱 없이 지금 사이트에서 Online 경로를 확인합니다. `node tools/verify-online.mjs --maps customs`는
   앱과 같은 스크립트를 같은 순서로 넣고 내 위치 원의 자리, 방향 삼각형의 각도, 층 자동 선택, 재전송과 핑,
   맞춤, 휠 확대 뒤 지형, 지도 회전을 맵마다 판정합니다. 맵을 생략하면 전체를 검사합니다.
3. 실행 중인 앱의 상태가 필요하면 사용자에게 Debug 빌드 실행을 요청하고 아래
   [CefSharp 렌더링 디버깅](#cefsharp-렌더링-디버깅-cdp) 절차로
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

맞춤과 줌을 고친 뒤 돌릴 검사는 [TESTING.md](TESTING.md)의 상황별 표에 있습니다. headless 검사는 실제
WPF 창의 크기 변경, 포커스와 모드 전환 때의 브라우저 교체를 검증하지 않으므로, 그 부분은 사용자에게
Debug 빌드 확인을 요청합니다.

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
- 퀘스트 마커는 넣지 않습니다. 이유는 위 [로컬 모드가 존재하는 이유](#로컬-모드가-존재하는-이유)에 있습니다.

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

## 화면 언어

설정 맨 위에서 고른 언어는 다시 시작하지 않고 그 자리에서 앱 전체 문구를 바꿉니다. 기본값은 Windows 표시
언어이고, 번역이 없는 언어는 영어로 표시합니다. 지금 언어와 바꾸는 방법은
[AppLanguage](src/TanukiTarkovMap/Localization/AppLanguage.cs) 한곳에 있고, 고를 수 있는 언어 목록의 원천은
`AppLanguage.Supported`입니다.

- 화면 문구는 `Localization/Strings.resx`(기본 언어, 영어)에 두고 같은 키로 언어별 `Strings.<코드>.resx`에
  번역합니다. XAML은 `{loc:Text 키}`, C#은 `Strings.키`로 읽고, XAML이나 C#에 화면 문구를 직접 적지 않습니다.
  로그와 예외 메시지는 화면 문구가 아니므로 번역하지 않습니다
- ViewModel은 번역된 문구를 필드에 저장하지 않습니다. 언어가 실행 중에 바뀌면 저장해 둔 문구만 이전 언어로
  남습니다. 읽을 때 `Strings`에서 만드는 계산 속성으로 두고 `LanguageChangedMessage`를 받아 다시 알립니다.
  [SettingsViewModel](src/TanukiTarkovMap/ViewModels/SettingsViewModel.cs)이 이 방식을 씁니다
- 언어를 바꾸면 창 안의 요소만 다시 그려집니다. 창 밖에서 코드로 만드는 트레이 메뉴는 메시지를 받아 새로
  만들고, 툴팁 문구는 ToolTip 요소 안이 아니라 소유 요소의 `ToolTip` 속성에 둡니다. 꾸민 툴팁의 모양은
  암시적 ToolTip 스타일로 입힙니다
- Local 미니맵은 앱이 그리는 안내(로딩, 오류)만 번역하며 번역은 [viewer/i18n.js](viewer/i18n.js)에 있습니다.
  앱이 주소의 `lang`과 `setLanguage`로 언어를 넘기고, 언어를 바꿔도 페이지를 다시 열지 않습니다. Levels 패널은
  사이트 화면을 옮긴 것이라 영어로 둡니다
- Online 사이트의 언어와 CEF 로캘은 앱 언어와 묶지 않습니다. 상단바의 PMC/SCAV 전환은 사이트 필터를 영어
  라벨(`'PMC Extraction'`, `'Scav Extraction'`)로 찾으므로, 사이트를 다른 언어로 열면 코어인 진영 전환이
  깨집니다. 사이트 언어를 맞추려면 먼저 필터를 언어와 무관하게 찾도록 바꾸고 `verify-online.mjs`로 확인합니다
- 언어를 더하려면 `Strings.<코드>.resx`를 추가하고 `AppLanguage.Supported`와 `viewer/i18n.js`에 같은 언어를
  넣습니다. 키, 자리 표시자, 언어 목록이 맞는지는 `node tools/verify-localization.mjs`가 확인합니다. 소수점에
  쉼표를 쓰는 언어라면 WPF 바인딩의 `StringFormat`이 창 언어로 숫자를 쓰므로 소수를 표시하는 자리를 확인합니다

문구 클래스를 빌드할 때 만드는 방식이 WPF에서 실패하는 이유와, 언어 전환이 닿지 않는 곳의 원인과 진단은
[WPF 다국어 레퍼런스](docs/20261008-wpf-localization-resx-and-live-switch.md)에 있습니다.

## CefSharp 렌더링 디버깅 (CDP)

Debug 빌드는 CDP(Chrome DevTools Protocol) 원격 디버깅 포트 9222를 엽니다
(`App.xaml.cs`의 `InitializeCef()`, `#if DEBUG` 한정. Release 빌드는 열지 않습니다).
이 포트로 CefSharp가 렌더링한 페이지의 DOM, JavaScript, 스크린샷을 앱 밖에서 조회할 수 있습니다.
주입 스크립트(`Models/JavaScript/Scripts/*.js`)가 실제로 적용됐는지 검증할 때 씁니다.

### 절차
1. 사용자에게 Debug 빌드 앱 실행을 요청합니다 (에이전트는 직접 실행하지 않습니다)
2. `node tools/cdp-debug.mjs targets`로 연결을 확인합니다
3. 아래 명령으로 검사합니다 (Node 22+ 내장 기능만 사용, 의존성 설치 불필요)

```bash
node tools/cdp-debug.mjs targets                  # 디버깅 가능한 페이지 목록
node tools/cdp-debug.mjs eval "document.title"    # 페이지 컨텍스트에서 JS 실행 (Promise는 await)
node tools/cdp-debug.mjs html ".panel_left"       # 선택자의 outerHTML 출력 (생략 시 문서 전체)
node tools/cdp-debug.mjs screenshot               # 렌더링 화면 PNG 캡처 (저장 경로 출력)
```

### 참고
- 스크린샷으로 저장된 PNG를 열어 보면 렌더링 결과를 눈으로 확인할 수 있습니다
- F12는 사람용 DevTools 창(`ShowDevTools()`)을 엽니다. 에이전트는 이 창을 읽을 수 없으므로 위 CDP 방식을 씁니다
- 포트는 localhost 전용으로만 열립니다. 포트를 바꾸면 스크립트는 `CDP_PORT` 환경변수로 맞춥니다
- 포트 9222는 앱 전용입니다. 재현 실험용 별도 Chrome을 띄울 때는 반드시 다른 포트를 씁니다
  (예: `--remote-debugging-port=9223` + `CDP_PORT=9223`).
  앱이 9222를 점유한 상태에서 같은 포트로 Chrome을 띄우면 Chrome은 바인딩에 실패해도 조용히 떠 있고,
  CDP 명령이 전부 실행 중인 앱으로 흘러 들어가 사용자가 보는 화면을 조작하게 됩니다 (실제 사고 사례)

## 체크리스트/문서 관리 원칙

- 완료된 작업 항목은 체크리스트에서 제거할 것
- 리스트가 제거될 때, 챕터 숫자 존재 시 오름차순으로 되도록 맞춰야 함
- 히스토리 기록보다 현재 남은 작업에 집중
- 불필요한 정보는 즉시 정리하여 문서를 간결하게 유지

### docs 파일 이름 규칙

`docs/` 아래 설계 근거 문서는 `yyyymmdd-{주제}.md`로 이름을 붙입니다. 날짜는 그 문서를 처음
기록한 날이며, 나중에 내용을 고쳐도 바꾸지 않습니다. 결정은 그 시점의 상황에서 나온 것이라
언제 판단한 기록인지가 이름에서 바로 읽혀야 합니다.

### PROJECT.md 동기화 (필수)

PROJECT.md는 코드와 함께 유기적으로 갱신해야 하는 살아 있는 설계 문서입니다.

- 클래스/서비스의 추가, 개명, 삭제 같은 구조 변경 시 PROJECT.md의 다이어그램, 표, 코드 샘플을 같은 작업 안에서 함께 수정합니다
- 개명/삭제 후에는 옛 이름을 저장소 전체에서 검색해 잔여 참조를 제거합니다

## 사용자 선호사항
- 중복되는 개념은 최대한 제외하고 싶음
- 유명한 좋은 한가지의 방법이 있다면 그것을 채용하고 싶음
- 항상 신중하고 가장 올바른 1가지의 답을 원함
- 내 의견은 항상 틀릴 수 있다고 가정함. 내 의견보다 더 좋은 올바른 답이 있다면 그것이 맞다고 수용하는 편
- 항상 공부하는 자세를 가지고 있으며, 내 의견보다 다른 좋은 유용하고 심플한 프로젝트 매니징 기법이 있으면 그것을 채용하는 편
- 중복되는 개념과 파일이 분산되어 복잡해지는 것을 싫어함
- 프로젝트의 큰 틀을 수정할 때 바로 수정하는 것이 아닌, 방향이 여러가지인 경우 에이전트와 큰 선택지에 대한 의견을 충분히 토의하여 Plan을 구축하고 프로젝트 반영해야 함
- 사용자는 현재 프로젝트의 방향성상 일반적이고 레거시 보다는 최신의 업데이트가 많은 라이브러리(Microsoft 공식 또는 업계 표준급 라이브러리)를 선호

### 사용자가 추구하는 개발 원칙

- **KISS 원칙** (Keep It Simple, Stupid) - 단순함을 최우선으로
- **YAGNI 원칙** (You Aren't Gonna Need It) - 필요하지 않은 복잡성 제거
- **실용주의** - 이론보다 실제 프로젝트에서 검증된 방식 선호

## Clean Code
The assistant writes self-documenting variable names that convey full meaning without requiring context inspection. Each variable name clearly expresses its purpose in one or two words, eliminating the need to examine surrounding code.

When naming variables, the assistant chooses words that maximize semantic clarity over brevity. If a more precise word exists that better captures the variable's purpose, the assistant uses it instead of generic terms.

The assistant follows these principles:
- Include essential context in the variable name itself (use 'userEmail' not 'email', 'productPrice' not 'price')
- Limit names to two meaningful words when possible, combining them for clarity
- Select words that precisely convey the variable's role and content
- For booleans, use descriptive states that indicate the condition being tracked

Examples of meaningful naming:
- Use 'paymentComplete' not 'complete' or 'isPaymentProcessingFinished'
- Use 'stockAvailable' not 'available' or 'hasStock'
- Use 'userLoggedIn' not 'loggedIn' or 'isUserCurrentlyLoggedIn'
- Use 'configLoaded' not 'loaded' or 'hasConfigurationBeenLoaded'
- Use 'sessionExpired' not 'expired' or 'isSessionStillValid'

The assistant prioritizes semantic richness, ensuring each variable name tells its complete story independently while maintaining readability through concise, meaningful word choices.

## Class Documentation Standards
The agent must create comprehensive documentation headers for every class that enable understanding the entire implementation without reading the code. The agent follows these documentation standards to ensure consistency across sessions and prevent repeated design failures.

### Required Documentation Structure
Every class must have a documentation header using this exact format:

```csharp
...
using MyUsingNamespace;

/**
[ClassName] - [One-line core responsibility]

Purpose: [Specific problem this code solves and why it exists]
Architecture: [Overall structure and how it integrates with the system]

Core Functionality:
- [Feature name]: [Detailed behavior, when triggered, expected outcomes]
- [Feature name]: [Detailed behavior, when triggered, expected outcomes]

State Management:
- [field/property name]: [Purpose, valid values, state transitions]
- [field/property name]: [Purpose, valid values, state transitions]

Method Flow:
  [Entry point] → [Processing steps] → [State changes]
  [Branches, callbacks, event flows with clear conditions]

Key Methods:
- MethodName(params): [What it does, when called, what it returns]
- MethodName(params): [What it does, when called, what it returns]

Dependencies:
- [ClassName]: [How they interact, what data flows between them]

Design Rationale: [Why this approach over alternatives]

Historical Context: [Past attempts and why they failed - with dates/versions]
Known Limitations: [Current constraints and potential solutions to explore]

[Include these sections when relevant:]
Edge Cases: [Special situations and how they're handled]
Critical Warnings: [DO NOT instructions with specific consequences]
Technical Debt: [Priority-ranked improvements needed]
Innovation Opportunities: [Concrete suggestions for future improvements]

Last Updated: [Date] | Unity [Version] | By [Context]
*/
namespace MyNamespace
{
...
```

### Documentation Guidelines
The agent follows these principles when creating documentation:
1. **Write for complete understanding**: If someone cannot recreate the class structure from the comment alone, the documentation is incomplete.
2. **Include concrete details**: Use actual method names, field names, parameter types, and specific error messages. Avoid vague descriptions.
3. **Document both current state and history**: Explain what exists now AND what was tried before. This prevents repeating past failures.
4. **Balance guidance with innovation**: Known limitations should be presented as challenges to overcome, not permanent restrictions. Include "this might be outdated" warnings where appropriate.
5. **Focus on why over what**: Code shows what happens. Documentation explains why it happens that way and what alternatives were considered.

### Specific Requirements
If the agent encounters unusual code patterns, it documents why they exist. Examples:
- Multiple null checks → Document the timing issue they solve
- Seemingly redundant code → Explain what breaks when removed
- Non-standard approaches → Justify why standard patterns failed

If the agent sees mixed responsibilities in a class, it marks it with a refactoring TODO but also documents why the current structure exists.
When modifying existing classes, the agent first reads the documentation to understand past failures, then updates it with any new learnings.
The agent includes ASCII diagrams for complex flows but keeps them readable and maintainable.
The agent references specific Unity versions, package versions, or environmental constraints that influenced design decisions.

### Innovation and Evolution
The agent treats existing documentation as valuable context, not unchangeable law. When the agent sees opportunities for improvement:
1. The agent acknowledges the historical context
2. The agent evaluates if current technology overcomes past limitations  
3. The agent documents both the attempt and the result
4. The agent updates the "Last Updated" timestamp

If documentation says "DO NOT use async/await - causes crashes", the agent considers: Was this written for Unity 2019? Might Unity 2023 handle it better? The agent documents the reasoning before attempting changes.

### Comment Style Rules
The agent uses these comment styles consistently:
- /** */ for class-level architectural documentation (no middle asterisks for token efficiency)
- /// for public API XML documentation
- // for inline implementation notes

The agent writes documentation that enables future agent sessions to:
- Understand the complete design without reading implementation
- Avoid repeating past failures
- Identify opportunities for improvement
- Maintain consistent behavior across sessions

### Quality Checklist

Before finalizing documentation, the agent verifies:
- Could someone implement this class using only the documentation?
- Are all state transitions and edge cases covered?
- Is the interaction with other classes crystal clear?
- Does it explain both what exists and why it exists that way?
- Are past failures and current limitations honestly documented?
- Are innovation opportunities highlighted rather than discouraged?

This comprehensive documentation serves as the source of truth for intended behavior while encouraging thoughtful evolution of the codebase.

## Code Design Philosophy
### YAGNI (You Aren't Gonna Need It) Principle
**Core Question: "Is this complexity solving a problem I have NOW, or a problem I MIGHT have?"**

Always choose the simplest solution that works today. Add complexity only when proven necessary.

### 1. **Immediate Red Flags** 🚩

Look for these patterns that indicate over-engineering:

```csharp
// 🚩 RED FLAG: Empty wrapper
public class Manager {
    private readonly Implementation impl = new();
    public void DoSomething() => impl.DoSomething(); // Just forwarding
}

// ✅ BETTER: Direct implementation
public class Manager {
    public void DoSomething() {
        // Actual logic here
    }
}
```

### 2. Decision Framework

Before creating separate classes, answer ALL of these:
| Question                        | Good Answer                      | Bad Answer                           |
|---------------------------------|----------------------------------|--------------------------------------|
| Why are these separate?         | "Different access levels needed" | "Might need it later"                |
| What does each class do?        | "Class A does X, Class B does Y" | "Class A calls Class B"              |
| Can I merge them?               | "No, because [specific reason]"  | "Yes, but separation is 'cleaner'"   |
| Is this solving a real problem? | "Yes, it fixes [current issue]"  | "It might help with future features" |

### 3. When Separation IS Justified
Only separate when you have these ACTUAL (not theoretical) needs:
- Security: Public API must hide internal implementation
- Circular Dependencies: A depends on B, B depends on A
- Multiple Implementations: You have 2+ working implementations NOW
- Team Boundaries: Different teams own different parts

### 4. The Right Approach
1. Start Simple
  - One class, one file
  - All logic in one place
2. Split When Reality Demands
  - You hit an actual limitation
  - Document WHY in code: // Split because [specific reason]
3. Measure Complexity
  - 2 simple files > 1 complex file
  - 1 simple file > 2 complex files

### 5. Real Example
```
// ❌ OVER-ENGINEERED (What we had)
// File 1: UIStateManager.cs (60 lines)
// File 2: UIState.cs (110 lines)
// Problem: Manager just forwards calls to State

// ✅ SIMPLE (What we should have)
// File 1: UIStateManager.cs (140 lines)
// All functionality in one place, no forwarding
```

## Remember
- Clean Code ≠ More Files
- Good Design = Solves TODAY'S problems
- YAGNI = Default mindset
- Complexity = Last resort

If you can't explain the separation in ONE sentence, merge it.
