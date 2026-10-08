<!--
INTENT
검토자가 이번 브랜치에서 무엇이 왜 고장 났고 코드에서 무엇을 변경했는지를 한 번에 판단하도록 작성한 작업 보고서다.
PR 설명으로도 사용한다. 설계 근거의 원천은 각 파일의 머리 주석, Pilot 연동 문서, 임베디드 웹페이지 제어 레퍼런스이고,
이 문서는 문제와 수정의 대응, 앱과 사이트의 책임 경계, 검증 결과만 정리한다.
-->

# Online 내 위치 표시 이식 작업 보고

## 요약

- 사이트가 지도 화면을 캔버스 방식으로 다시 짠 뒤(2026-10-08 확인) Online에서 바라보는 방향이 표시되지 않았고,
  새 스크린샷이 없는데도 내 위치가 3~10초의 불규칙한 간격으로 커졌다 줄어들었습니다. Local은 정상이었습니다.
  원인은 사이트가 내 위치를 DOM 요소 대신 캔버스에 그리고, 좌표 변환을 가진 지도 상태를 렌더 트리에서 숨긴
  것입니다.
- Online의 내 위치와 방향 표시를 사이트에서 떼어 앱이 주입하는 스크립트로 옮겼습니다. 파일명 해석, 원과 방향
  삼각형 그리기, 핑, 표시 확인은 앱이 맡고, 사이트에서는 지도 컨테이너와 좌표 변환 함수 두 개만 빌립니다.
  모양은 Local 미니맵과 같고, 사이트의 다른 마커와 지도 기능은 그대로 사용합니다.
- 옮긴 직후 층 자동 선택이 끊긴 것을 앱 확인에서 발견했습니다. 사이트가 층을 고를 때 지켜보는 값(`playerPos`)을
  같은 좌표로 설정해, 층은 지금도 사이트가 자기 층 데이터로 고릅니다.
- 이제 사이트가 위치 입력 경로나 내 위치 그리기를 변경해도 Online 위치 표시는 끊기지 않습니다. 사이트가 지도
  상태나 좌표 변환을 변경하면 위치를 그리지 않고 상태 이름으로 알리며, 그때는 Local이 비상 경로입니다.
- 실제 사이트 12개 맵에서 위치, 방향, 층, 핑, 지도 맞춤을 검증했고 오프라인 검사와 빌드가 통과했습니다. Local
  모드의 동작, 설정, 의존성은 그대로입니다. 실제 앱 실행 확인은 남아 있습니다.

## 바뀐 구조

Online 내 위치 표시에서 앱과 사이트가 맡는 일은 다음과 같습니다.

| 기능 | 이전 (main) | 지금 |
|---|---|---|
| 파일명 해석 | 사이트 ("Where am i" 입력 처리기) | 앱 (`pilot-bridge.js`, Local과 같은 식) |
| 내 위치 원 | 사이트 | 앱 (`map-markers.js`, Local과 같은 모양) |
| 방향 삼각형 | 앱 (사이트의 `.marker` 요소에 덧붙임) | 앱 (원 요소 안에 그림) |
| 핑 | 사이트 (전달할 때마다) | 앱 (새 스크린샷이 처음 보일 때만) |
| 표시 확인 | 사이트의 `playerPos` 값과 `.marker` 요소 | 앱이 그린 마커가 화면에 보이는지 |
| 화면 자리와 각도 | 사이트 (방향 각도만 앱이 사이트의 좌표 변환으로 계산) | 앱이 사이트의 좌표 변환으로 계산 |
| 층 선택 | 사이트 | 사이트 (앱이 같은 좌표를 `playerPos`에 설정) |
| 다른 마커와 지도 기능 | 사이트 | 사이트 |

핵심 스크립트는 `src/TanukiTarkovMap/Models/JavaScript/Scripts/`에 있습니다.

| 파일 | 하는 일 | 이번 변경 |
|---|---|---|
| `map-state-capture.js` | 사이트가 만드는 지도 상태와 층 데이터 표를 만들어지는 순간 기록 | 신규. 로드 시작(`FrameLoadStart`)에 주입 |
| `pilot-bridge.js` | 파일명 해석, 화면 자리와 각도 계산, 표시 확인, 화면 이동, 층 좌표 전달 | 위치 입력 어댑터를 삭제하고 다시 작성 (판 7) |
| `map-markers.js` | 원과 방향 삼각형 그리기, 매 프레임 자리 갱신, 핑 | 방향 표시기에서 내 위치 표시로 다시 작성 (판 7) |
| `map-keep-visible.js` | 지도 맞춤과 이동 제한 | 맵 상자를 지도 상태로 계산 (판 10) |
| `web-elements-control.js` | UI 숨김 | 앱의 내 위치 표시를 남길 층으로 등록 |

```
페이지 로드 시작  map-state-capture.js가 사이트의 지도 상태와 층 데이터 표를 기록
스크린샷 감지     WebBrowserViewModel이 파일명을 window.tanukiPilot.showScreenshot에 전달
  -> pilot-bridge.js가 파일명을 읽고 화면에 붙은 가장 새 지도 상태를 고름
  -> map-markers.js가 지도 컨테이너에 원과 삼각형을 붙이고, 좌표 변환으로 구한 자리와 각도를 매 프레임 적용
  -> 마커가 화면에 보이면 성공 (새 스크린샷이면 핑, 화면 가장자리 밖이면 사이트의 이동 함수로 가운데에 놓음)
  -> 층 데이터 표가 채워지면 같은 좌표를 사이트의 playerPos에 설정해 사이트가 층을 고르게 함
```

## 문제와 수정

### 1. Online: 바라보는 방향이 표시되지 않음

**증상**: 내 위치 원은 보이지만 방향 삼각형이 보이지 않았습니다. 같은 스크린샷을 Local에서 열면 방향까지
표시됐습니다.

**원인**: 사이트가 지도 화면을 다시 짰습니다.

- 마커와 내 위치를 DOM 요소가 아니라 캔버스(`markers-canvas`, `players-canvas`)에 그립니다. main의 방향
  스크립트(`map-markers.js` 판 5)는 사이트의 `.marker` 요소마다 삼각형을 붙였으므로 붙일 요소가 없어졌습니다.

  ```js
  // main의 map-markers.js
  document.querySelectorAll('.marker').forEach(marker => {
      let triangle = marker.querySelector('.triangle-indicator');
      ...
  });
  ```

- 사이트도 방향 화살표를 그리지만 PRO 사용자에게만 그립니다. 렌더러가 사용자 정보의 `pro`를 확인합니다.
- 방향 각도를 구하던 브리지의 `getMapHeading()`은 사이트의 지도 상태가 필요했는데, 그 상태를 찾지 못했습니다(3번).

**수정**: 방향 삼각형을 사이트 요소에 덧붙이지 않고 앱이 원과 함께 직접 그립니다(4번).

### 2. Online: 새 스크린샷이 없는데도 내 위치가 커졌다 줄어듦

**증상**: 스크린샷을 찍지 않는 동안에도 3~10초의 불규칙한 간격으로 내 위치 원이 커졌다 줄어들었습니다.

**원인**: 표시 확인이 매번 실패해 같은 스크린샷이 2초마다 다시 전달됐고, 브리지가 전달할 때마다 사이트의 핑을
켰습니다.

- main 브리지는 사이트 지도 상태의 `playerPos`가 파일명 좌표와 같고 `.marker` 요소가 있어야 표시된 것으로
  판정했습니다(`positionMatches()`). 지금 사이트에서는 지도 상태도 `.marker`도 찾을 수 없어 판정이 늘
  실패했습니다. main 스크립트를 지금 사이트에 주입해 재현하면 상태가 `position-not-rendered (where-am-i)`로 남고
  약 2초마다 재전송이 일어났습니다.
- 앱은 표시가 확인되지 않으면 2초 점검(`MaintainPositionAsync`)마다 마지막 스크린샷을 다시 보냅니다.
- 브리지의 "Where am i" 어댑터는 보낼 때마다 사이트의 핑을 불렀습니다.

  ```js
  // main의 pilot-bridge.js
  send(panel, filename) {
      panel.playerData.isPlayerMarkerVisible = true;
      panel.onScreenPositionChange({ target: { value: filename } });
      panel.onMakePlayerMarkerPing?.();   // 재전송마다 사이트의 핑을 켠다
  }
  ```

- 주기가 불규칙했던 이유는 사이트의 핑 동작에 있습니다. 사이트의 핑 표시는 켜진 뒤 2초 동안 유지되고, 꺼졌다
  다시 켜질 때만 애니메이션(약 0.7초 동안 최대 2.5배)을 새로 시작합니다. 재전송 간격이 2초 안팎에서 흔들리므로
  표시가 꺼진 직후에 도착한 재전송만 애니메이션을 시작했습니다.

**수정**

- 표시 확인을 복구해 재전송을 멈췄습니다. 확인은 앱이 그린 마커가 화면에 보이는지로 합니다(3번, 4번).
- 확인이 다시 깨져 재전송이 반복돼도 조용하도록, 핑은 새 스크린샷이 처음 보일 때만 켭니다. 브리지가 마지막으로
  핑을 켠 파일명을 기억하고, 마커가 실제로 보였을 때만 그 값을 갱신합니다. 보이지 않은 채 끝난 전달까지
  기록하면 다음 재전송이 처음 보이는 순간에도 핑을 켜지 않기 때문입니다.

  ```js
  const visible = view.show({ locate: () => locate(anchor), alive: () => alive(anchor),
      ping: pingedFilename !== filename });
  currentAnchor = anchor;
  if (!visible) { ... return false; }
  pingedFilename = filename;
  ```

**확인**: `verify-map-recovery.mjs`의 "같은 스크린샷을 다시 보내면 핑을 켜지 않고 새 스크린샷에만 켬" 사례가
통과합니다. 실제 사이트 12개 맵에서 처음 표시할 때 핑이 켜졌고, 표시된 뒤에는 2초 점검이 같은 위치를 다시
보내지 않았으며, 같은 파일을 다시 보내도 핑이 켜지지 않았습니다.

### 3. Online: 좌표 변환을 가진 지도 상태가 렌더 트리에서 사라짐

**원인**: main의 브리지는 렌더 중인 Vue 트리의 props에서 지도 상태를 찾았습니다(`findProps()`). 지도 상태는 지도
컨테이너, 좌표 변환(`gamePosToMapPos`, `mapPosToScreenPos`), `playerPos`, `panzoom`을 가진 객체입니다. 지금
사이트는 이 상태를 페이지 컴포넌트 안에서 `reactive()`로 만들고, 하위 컴포넌트에는 getter만 가진 얼린 객체를
넘깁니다.

```js
// 사이트 번들의 구조 (필요한 부분만)
reactive({ cont, zoom, x, y, playerPos, marker, panzoom, gamePosToMapPos, mapPosToScreenPos, ... })
Object.freeze({ get state() { ... }, rotateView, centerMap, ensureElementVisible })   // 하위 컴포넌트가 받는 객체
```

렌더 트리의 모든 컴포넌트를 훑어도 지도 상태는 나오지 않았습니다. getter와 클로저가 붙든 값은 페이지 밖에서
꺼낼 수 없으므로, 만들어진 뒤에 찾는 방법은 없고 만들어지는 순간에 잡는 방법만 남습니다.

**수정** (`map-state-capture.js`와 `MapStateCapture.js.cs` 신규, `WebBrowserViewModel.cs`)

- Vue의 `reactive()`는 원본을 키로, 프록시를 값으로 WeakMap에 등록합니다. 페이지 스크립트보다 먼저
  `WeakMap.prototype.set`을 감싸, 지도 상태 모양의 프록시만 약한 참조(`WeakRef`)로 기록합니다.

  ```js
  WeakMap.prototype.set = function set(key, value) {
      const result = nativeSet.call(this, key, value);   // 원래 동작과 반환값은 그대로 둔다
      if (value !== key) {
          if (isMapState(key) && isMapState(value)) mapStates = remember(mapStates, value);
          else if (isLevelSet(key) && hasSameNames(key, value)) levelSets = remember(levelSets, value);
      }
      return result;
  };
  ```

- 지도 상태는 앱이 사용하는 칸(컨테이너 `cont`와 좌표 변환 둘)만으로 알아봅니다. 사용하지 않는 칸까지 요구하면
  사이트가 그 칸만 변경해도 기록이 빕니다. 칸이 있는지는 `hasOwnProperty`로 확인해 Vue 프록시의 의존성 추적에
  걸리지 않게 했습니다.
- 층 데이터 표도 같은 방법으로 기록합니다(5번).
- 브리지는 기록 가운데 화면에 붙은 가장 새 지도 상태를 사용합니다. 맵을 옮기면 새 상태가 생기고, 새 페이지가
  붙기 전까지는 이전 지도가 화면에 남기 때문입니다.
- 앱은 이 스크립트를 `page-health.js`와 같은 시점인 `FrameLoadStart`에 주입합니다. 기록 스크립트가 없으면
  `map-unavailable (capture-missing)`, 스크립트가 지도 상태보다 늦게 들어가 기록이 비면
  `map-unavailable (not-captured)`를 남기고 위치를 그리지 않습니다.

**확인**: `verify-map-recovery.mjs`의 "기록이 WeakMap의 원래 동작을 유지하고 지도 상태만 기록" 사례와 "기록이 없거나
늦은 경우를 이름으로 알림" 사례가 통과합니다. 실제 사이트 검사는 문서가 만들어질 때 실행되는 스크립트
(`Page.addScriptToEvaluateOnNewDocument`)로 같은 파일을 주입하고, 12개 맵 모두에서 지도 상태를 찾았습니다.

### 4. Online: 위치 표시가 사이트 내부에 묶여 배포 때마다 끊김

**문제**: 1~3번만 수정하면 같은 일이 되풀이됩니다. 그때까지 Online 위치 표시는 사이트의 두 부분에 묶여 있었고,
둘 다 배포 때마다 바뀌었습니다.

- 위치 입력 경로: 앱의 WebSocket 서버(2026-08-17까지), 전역 함수 `window.pilot`, Nuxt Pilot 서비스,
  "Where am i" 입력 처리기(2026-10-07부터)
- 내 위치 렌더러: DOM 요소에서 캔버스로 옮기며 방향 화살표를 PRO 전용으로 변경

반면 지도 상태의 좌표 변환(`gamePosToMapPos`, `mapPosToScreenPos`)은 그동안 같은 이름으로 남았습니다.

**수정**: 위치 표시에 필요한 기능만 앱의 것으로 옮기고, 그 밖의 마커와 지도 기능은 사이트의 것을 그대로
사용합니다.

`pilot-bridge.js`(판 7)는 다음을 맡습니다.

- 파일명은 앱이 읽습니다. Local 미니맵(`viewer/coords.js`의 `parseScreenshot`)과 같은 정규식, 축 순서, 방향
  식이라 같은 파일명이 두 모드에서 같은 자리와 방향에 찍힙니다.
- 자리는 사이트의 좌표 변환으로 구합니다. 방향은 지도별 각도 식을 옮겨 오지 않고, 시선 방향의 벡터를 같은
  변환으로 화면에 투영해 구합니다. 지도마다 다른 회전과 사이트의 지도 회전이 함께 반영됩니다.

  ```js
  function screenPoint(map, position) {
      const point = map.gamePosToMapPos(position.x, position.y);
      const screen = point && map.mapPosToScreenPos(point.x, point.y);
      return Number.isFinite(screen?.x) && Number.isFinite(screen?.y) ? { x: screen.x, y: screen.y } : null;
  }
  ```

- 성공은 사이트 상태가 아니라 앱이 그린 마커가 화면에 보이는지로 판정합니다. 사용자가 지도를 옮겨 마커가 화면
  밖에 있어도 표시된 것으로 봅니다.
- 다른 맵 주소로 옮기면 그 맵에서 맡은 위치는 끝납니다. 같은 주소에서 사이트가 지도 상태를 새로 만들면 지난
  지도의 마커를 숨기고, 앱의 다음 점검이 같은 파일을 새 지도에 맡깁니다.
- 사이트에서 호출하는 함수는 좌표 변환 둘, 화면 이동(`centerOnPosition`), 퀘스트 완료(`questComplete`)뿐입니다.
  위치 입력 어댑터 세 개(`global-pilot`, `pilot-service`, `where-am-i`)와 렌더 트리 탐색(`findProps()`)은
  삭제했습니다.

`map-markers.js`(판 7)는 방향 표시기에서 내 위치 표시로 다시 작성했습니다.

- 지도 컨테이너에 원 요소 하나(안에 방향 삼각형)를 붙입니다. 크기, 색, 삼각형의 자리, 핑은 Local
  미니맵(`viewer/style.css`)과 같습니다. 지름 20px(테두리 포함)에 보라 바탕(`#8a2be2`)과 초록 테두리(`#70a800`)이고,
  핑은 0.7초 동안 지름을 2.5배까지 키웠다 되돌립니다.
- 사이트는 끌기, 휠, 회전, 창 크기 변경마다 지도를 다시 그리지만 그 사건을 밖에 알리지 않습니다. 그래서
  브리지가 위치를 계속 따라가라고 하는 동안 매 프레임 자리를 다시 구하고, 값이 바뀐 프레임에만 DOM을 갱신합니다.

  ```js
  const markerTransform = `translate(${place.x}px, ${place.y}px) translate(-50%, -50%)`;
  if (markerTransform !== shownMarker) {
      marker.style.setProperty('transform', markerTransform, 'important');
      shownMarker = markerTransform;
  }
  ```

- 스타일은 인라인 `!important`로 지정해 사이트 스타일이 덮어쓰지 못하게 했습니다. 원의 폭과 높이만
  `!important`에서 제외했습니다. `!important` 선언은 애니메이션보다 우선해 핑이 보이지 않기 때문입니다.
- 표시 층은 사이트의 내 위치 캔버스(z-index 7) 위, 마커 설명 창(10) 아래인 8입니다.

검토했지만 고르지 않은 방법은 다음과 같습니다.

| 방법 | 고르지 않은 이유 |
|---|---|
| 사이트가 그린 원에 방향 삼각형만 덧붙이기 | 원이 캔버스 그림이라 붙일 요소가 없고, 덧붙여도 사이트의 그리기 조건에 다시 묶임 |
| 사이트의 PRO 방향 화살표 켜기 | 사이트의 유료 기능 우회 |
| 캔버스 그리기 함수를 감싸 원의 자리 읽기 | 사이트 렌더러의 그리기 순서에 묶여 렌더러를 조금만 변경해도 깨짐 |
| 좌표 변환까지 앱에 구현 (리소스의 변환 식 사용) | 사이트가 식이나 지도를 변경하면 마커가 지형과 어긋난 채 경고 없이 남음 |

마지막 방법은 위치 표시를 사이트와 완전히 떼어 내는 길입니다. 좌표 변환을 사이트에서 빌리면 사이트가 식을
변경해도 마커가 지형과 함께 움직이고, 변환이 사라지면 틀린 자리에 그리지 않고 멈춥니다. 틀린 위치를 확신 있게
보여 주는 것보다 멈추고 알리는 편이 안전하다고 판단했습니다.

**대가**

| 사이트의 변경 | 이전 구조 | 지금 구조 |
|---|---|---|
| 위치 입력 경로 교체 | Online 위치 끊김 | 영향 없음 |
| 내 위치 렌더러, PRO 조건, 핑 동작 변경 | 방향이나 핑에 영향 | 영향 없음 |
| 지도 상태의 모양이나 좌표 변환 변경 | 위치는 사이트가 그리고 확인과 방향만 실패 | 위치와 방향 모두 표시하지 않음 |

- 마지막 줄이 실제 손해입니다. 그때는 Local이 비상 경로이고, 번들에서 지도 상태를 만드는 코드를 다시 찾아 기록
  조건과 재현 페이지를 맞춥니다.
- 사이트는 앱이 그린 위치를 모르므로, 층 선택 말고 사이트의 내 위치 기능(스쿼드 공유, 사이트 UI의 위치 표시)에는
  반영되지 않습니다.

**확인**: `verify-map-recovery.mjs`에서 다음 사례가 통과합니다.

- 사이트 입력 없이 파일명만으로 위치와 방향을 그림
- Online과 Local이 같은 파일명을 같은 위치와 방향으로 읽음
- 마커가 지도 이동과 회전을 따라감
- 주기 점검이 화면을 옮기지 않고 표시를 확인함
- 사이트가 지도 상태를 새로 만들면 다시 맡을 때까지 지난 위치를 숨김
- 지워진 마커와 교체된 컨테이너를 재전송 없이 복구함
- 맵을 떠나면 같은 맵으로 돌아와도 그 위치는 끝남
- 사이트의 좌표 변환이 예외를 던지면 마커를 숨기고 `site-error`로 알림
- 반복 주입과 공개 API 삭제 뒤에도 마커가 하나만 남음

실제 사이트 12개 맵에서 원이 사이트 좌표 변환으로 구한 자리의 1px 안에 있었고, 사이트가 같은 파일명으로 그린
자기 원과 1.5px 안에서 겹쳤습니다. 삼각형은 원 앞의 정해진 자리(중심에서 17.25px)에 있었고, 사이트가 방향
화살표를 그리는 식(시선 + 270 - 지도 회전 + 화면 회전)과 3도 안에서 같은 방향을 가리켰습니다. 지도를 90도
돌린 뒤에도 같았습니다.

### 5. Online: 이식 뒤 층이 자동으로 바뀌지 않음

**증상**: 4번을 적용한 뒤 앱에서 확인하니, 다른 층에서 스크린샷을 찍어도 지도의 층이 바뀌지 않았습니다.

**원인**: 사이트의 층 선택 코드(층 데이터 표를 만드는 컴포저블)는 지도 상태의 `playerPos`를 깊게 지켜보다가
값이 바뀌면 그 위치로 층을 고릅니다. 구역 안이고 높이가 맞는 층을 먼저 찾고, 없으면 층의 높이 범위로 찾습니다.
"Where am i" 처리기는 `playerPos`를 변경하는 일만 했고, 층은 그 변화를 본 watcher가 골랐습니다. 앱이 위치를
직접 그리면서 `playerPos`를 건드리지 않게 되자 층도 멈췄습니다.

이식할 때 처리기 코드만 읽고 "기존 경로도 층을 바꾸지 않는다"고 잘못 판단해 이 영향을 놓쳤습니다. 층 선택
코드는 지도 화면과 다른 번들 파일에 있었습니다.

조사 중에 경합도 하나 찾았습니다. 사이트는 층 데이터(지도 문서의 층별 높이 범위와 구역)가 채워지기 전에 바뀐
위치로는 층을 고르지 않고, 데이터가 채워진 뒤에 다시 고르지도 않습니다. 실제 사이트에서 맵을 연 직후 설정한
좌표로는 끝내 층이 바뀌지 않았고, 층 데이터가 채워진 뒤 설정한 좌표로만 층이 바뀌었습니다. 앱은 맵을 열자마자 위치를
전달하므로, 좌표만 설정하면 맵을 연 직후 받은 스크린샷에서는 층이 바뀌지 않습니다.

**수정** (`pilot-bridge.js`, `map-state-capture.js`)

- 위치를 표시할 때 `playerPos`의 x, y, z를 같은 좌표로 설정합니다. 방향(`look`)과 사이트의 표시 상태는 건드리지
  않으므로 사이트는 자기 원을 그리지 않습니다.
- 지도 상태 기록이 층 데이터 표(층 이름마다 `num`과 `visible`을 가진 표)도 기록하고, 브리지는 표에 높이 범위나
  구역이 채워질 때까지 0.1초 간격으로 최대 30초 기다렸다가 좌표를 설정합니다. 표를 기록하지 못한 판에서는
  기다리지 않고 바로 설정합니다.

  ```js
  const levels = levelSet();
  if (levels && Object.keys(levels).length < 2) { siteSync = null; levelState = 'single-level'; return; }
  if (levels && !levelsReady(levels) && Date.now() < request.deadline) {
      levelState = 'waiting';
      siteSyncTimer = setTimeout(flushSitePosition, LEVEL_POLL);
      return;
  }
  siteSync = null;
  playerPos.x = anchor.position.x;
  playerPos.y = anchor.position.y;
  playerPos.z = anchor.position.z;
  ```

- 층(`selectedLevel`)은 직접 설정하지 않습니다. 사이트의 층 선택 상태에서 계산되는 값이라 직접 설정하면 Levels
  패널과 마커 필터가 어긋납니다. 반면 `playerPos`는 사이트의 입력 처리기가 그대로 변경하는 입력 상태라, 같은
  프록시로 설정하면 사이트의 watcher가 나머지를 다시 계산합니다.
- 같은 좌표를 다시 설정하면 사이트가 반응하지 않으므로, 같은 스크린샷을 다시 맡겨도 사용자가 손으로 고른 층은
  그대로 남습니다. 좌표가 다른 새 스크린샷이 오면 사이트가 다시 고릅니다.
- 진행 상태는 `tanukiPilot.levelSync()`로 봅니다. `waiting`은 층 데이터를 기다리는 중, `synced`는 좌표를 설정한
  상태, `single-level`은 층이 하나인 맵, `no-level-data`는 층 데이터 표를 기록하지 못한 경우입니다.

검토했지만 고르지 않은 방법은 다음과 같습니다.

| 방법 | 고르지 않은 이유 |
|---|---|
| "Where am i" 처리기를 다시 호출 | 같은 칸을 변경하는 일만 하는데, 처리기를 찾는 경로(렌더 트리의 props)에 다시 묶임 |
| 리소스의 층 데이터로 앱이 층을 정하고 사이트의 층을 변경 | 사이트의 층 데이터와 리소스가 어긋날 수 있고, 층을 전환하는 입구(Levels 패널, Alt 휠)는 화면 배치에 따라 없거나 한 칸씩만 움직임 |
| 일정 시간 뒤에 좌표를 다시 설정 | 그 사이 사용자가 고른 층을 덮어쓰고, 층 데이터가 그보다 늦으면 여전히 놓침 |

**확인**

- `verify-map-recovery.mjs`에 세 사례를 추가했습니다. 층 데이터가 늦게 도착해도 도착한 뒤 층이 바뀌는지, 같은
  스크린샷을 다시 보내도 사용자가 고른 층이 남는지, 층이 하나인 맵은 건드리지 않고 층 데이터 표가 없어도 위치
  표시를 붙잡지 않는지 봅니다. 층 좌표 전달(`syncSitePosition`) 호출을 삭제한 브리지로 같은 검사를 실행하면 이
  세 사례와 `playerPos` 확인 사례가 실패합니다(14/18).
- 실제 사이트에서 층이 여럿인 11개 맵마다 기본 층이 아닌 위치를 맵을 열자마자 보냈고, 11개 모두 그 층으로
  바뀌었습니다. 기대 층은 사이트가 지금 가진 층 데이터에 Local의 판정 식을 적용해 구했고, 앱 리소스로 구한
  층과도 같았습니다.

### 6. Online: 지도 맞춤이 맵 상자를 계산할 요소를 잃음

**문제**: 맞춤 스크립트(`map-keep-visible.js`)는 확대와 이동이 걸리는 `.map-wrap` 요소의 상자를 맵 좌표 공간으로
삼고, 지형 범위를 그 상자에 대한 비율로 저장했습니다. 지금 사이트는 지도를 캔버스로만 그려 `.map-wrap`이
없습니다. 상자를 구하지 못하면 맞춤과 이동 제한이 동작하지 않습니다. 화면 이동(`panBy`)도 브리지의 `getMap()`이
지도 상태를 돌려줘야 동작합니다.

**수정** (`map-keep-visible.js`, 판 10): 맵 상자를 지도 상태의 panzoom 변환(`x`, `y`, `zoom`)과 회전을 반영한 맵
크기(`viewSize`)로 계산합니다. 사이트가 캔버스에 맵을 그리는 변환과 같습니다. 회전 여부는 지도 상태의
`viewRotation`으로 판단하고, 지도 상태는 브리지의 `getMap()`으로 얻습니다(3번의 기록).

```js
var left = view.left + container.clientLeft + map.x;
var top = view.top + container.clientTop + map.y;
return {
    container: container,
    view: view,
    box: { left: left, top: top, width: map.viewSize.width * map.zoom, height: map.viewSize.height * map.zoom },
    turn: String(map.viewRotation || 0)
};
```

**확인**: `verify-map-keep-visible.mjs`의 재현 페이지를 지금 사이트처럼 지도와 내 위치를 캔버스로만 그리는 구조로
변경했고, 9개 사례가 통과합니다. 실제 사이트 12개 맵에서 채움 비율이 0.799~0.961로 맞춤 범위(0.75~0.98) 안에
들었고, 최소 배율에서도 지형이 창보다 큰 Icebreaker는 최소 배율(채움 1.808)에서 멈췄습니다. 지형 밖에서 5번
확대한 뒤에도 지형이 화면 가운데를 덮었습니다.

### 7. Online: UI 숨김이 앱의 내 위치 표시를 가릴 위험

**문제**: 상단바의 "UI 요소 숨기기"는 지도에서 남길 층을 목록(`MAP_LAYER_SELECTORS`)으로 적고 나머지를 숨깁니다.
앱이 새로 그리는 `.tanuki-position`이 목록에 없으면 UI 숨김을 켤 때 함께 숨겨집니다.

**수정** (`web-elements-control.js`): 목록에 `.tanuki-position`을 추가했습니다. `.map-wrap`과 `.squad-layer`에는 이전
판의 요소라는 설명을 주석에 더했습니다.

**확인**: 실제 사이트 검사는 UI 숨김을 켠 뒤 원과 삼각형이 보이는지 판정합니다(12/12).

### 8. 앱: 위치 표시 호출과 스크립트 복구

**수정** (`WebBrowserViewModel.cs`, `PilotBridge.js.cs`, `MapMarkers.js.cs`)

- 로드 시작(`OnFrameLoadStart`)에 지도 상태 기록을 상태 보고보다 먼저 주입합니다.

  ```csharp
  e.Frame.ExecuteJavaScriptAsync(MapStateCapture.INIT_SCRIPT);
  e.Frame.ExecuteJavaScriptAsync(PageHealth.INIT_SCRIPT);
  ```

- Online도 Local처럼 동기 호출 한 번으로 표시 결과(bool)를 받습니다. 사이트의 입력 경로를 기다릴 일이 없어져
  `PilotBridge.SendScreenshot`(Promise 대기)을 `ShowScreenshot`으로 교체했고, 사용하는 곳이 없어진
  `ExecuteScriptAsync`의 `awaitPromise` 인자를 삭제했습니다.

  ```csharp
  var response = await ExecuteScriptAsync(local ? LocalViewer.ShowScreenshot(pending.Filename)
      : PilotBridge.ShowScreenshot(pending.Filename), documentVersion: version);
  ```

- 상태 확인문을 새 API에 맞췄습니다. `PilotBridge.IS_INSTALLED_SCRIPT`는 브리지 판 7의 `showScreenshot`,
  `isRendered`, `status`, `hasPosition`, `revealPosition`을, `MapMarkers.ENSURE_READY_SCRIPT`는 `window.tanukiMarker`
  판 7의 `ensure`와 `show`를 확인합니다. 마커 스크립트를 복구하면 로그에 `[PilotBridge] Marker script restored`를
  남깁니다.

**확인**: `verify-map-recovery.mjs`의 "C# 상태 확인문이 배포하는 JavaScript API를 받아들임" 사례가 통과합니다.
빌드 오류는 0개입니다.

### 9. Local: 층 판정 식을 Online 검사와 함께 사용하도록 분리

**수정** (`viewer/coords.js`, `viewer/main.js`): 미니맵 안에 있던 층 판정(`levelAt`, `inZone`, `inHeight`)을
`viewer/coords.js`의 `levelAtPosition(levels, x, y, height)`로 옮겼습니다. 실제 사이트 검사가 같은 식으로 사이트의
층 데이터에서 기대 층을 구합니다. 미니맵의 동작은 그대로입니다. `viewer/markers.js`와 `viewer/style.css`는 Online과
같은 모양을 사용한다는 주석만 갱신했습니다.

**확인**: `verify-viewer.mjs` 73개 항목이 통과합니다(12개 맵의 높이 기반 층 선택 포함).

### 10. 검사 도구: 재현과 판정이 사이트의 입력 경로를 전제함

**수정**

- `verify-map-recovery.mjs`: 사이트의 입력 경로를 재현하던 페이지를, 사이트에서 빌리는 것(컨테이너와 좌표 변환을
  가진 지도 상태, 층 데이터 표와 층 선택)만 재현한 페이지로 다시 작성했습니다. 18개 사례로 위치와 방향, 두
  모드의 파일명 해석 대조, 핑, 층, 지도 교체와 복구, 실패 상태 이름을 봅니다. `--scripts <폴더>`로 다른 판의 주입
  스크립트와 비교합니다.
- `verify-online.mjs`: 앱과 같은 스크립트를 같은 순서로 실제 사이트에 주입하고 맵마다 다음을 판정합니다.
  - 원: 사이트 좌표 변환으로 구한 자리와 1px 안, 사이트가 같은 파일명으로 그린 자기 원과 1.5px 안. 사이트 원은
    검사만 "Where am i" 입력으로 그리게 하고 앱은 이 입력을 사용하지 않습니다. 사이트가 입력을 없애면 이 비교만
    건너뜁니다
  - 방향: 사이트가 방향 화살표를 그리는 식과 3도 안. 앱은 이 식이 아니라 벡터 투영을 사용하므로 서로 다른 경로의
    대조입니다. 지도를 90도 돌린 뒤에도 봅니다
  - 층, 핑과 재전송, 맞춤, 휠 확대 뒤 지형
- `verify-map-keep-visible.mjs`: 재현 페이지를 캔버스만 있는 지금 사이트 구조로 변경(6번)

### 11. 문서: 위치 표시 설명이 삭제한 입력 경로를 기준으로 함

**수정**

- AGENTS.md "스크린샷 위치 추적": Online이 사이트에서 빌리는 것, 층을 사이트가 고르는 방식, 핑 규칙, 지도 상태
  기록, 진단 순서(`levelSync()`의 상태 포함)
- PROJECT.md: 구조도(`MapStateCapture`, 브리지와 내 위치 표시의 관계), 위치 표시 절, 페이지 후처리 순서, 용어집
- TESTING.md: 1번 이름("Online 위치 표시")과 확인 항목, 9번 실제 사이트 검사 항목, 12번 1항(앱 확인에 모양, 핑,
  층 전환 추가)
- README(한국어, 영어, 일본어): 실시간 좌표 동기화 절의 지금 구조와 이번 사이트 변경
- [Pilot 연동과 위치 전달 경로](20260817-pilot-bridge.md): 캔버스 판의 원인, 앱이 그리게 된 경위와 대가, 층을
  사이트가 고르게 한 방법, 지금 구조, 의존 지점과 로그 표
- [임베디드 웹페이지 제어 레퍼런스](20260818-embedded-site-control.md): 이번에 검증한 기법을 다른 프로젝트에서도
  사용할 수 있게 정리했습니다.
  - 5.7 핵심 표시는 직접 그리고 투영만 빌리기
  - 5.8 만들어진 뒤 닿지 않는 상태를 만들어지는 순간에 기록하기
  - 5.9 입력 상태와 파생 상태의 구별
  - 6절 실패 모드: 다시 보내는 입력은 조용해야 함, 기능은 상태를 지켜보는 곳에 있을 수 있음, 데이터가 준비되기
    전의 입력은 버려질 수 있음, 수정 전 판으로 검사 실패 확인
  - 8절 사이트 변경에 취약한 지점
- [로컬 뷰어 설계](20260821-local-viewer-design.md): 층 판정 식의 위치

## 검증 결과

| 검사 | 결과 | 보는 것 |
|---|---|---|
| `verify-online.mjs` | 12/12 맵 통과 | 실제 사이트의 원 자리, 방향, 층, 핑과 재전송, 맞춤, 휠 확대 뒤 지형, 지도 회전 |
| `verify-map-recovery.mjs` | 18/18 통과 | Online 위치 표시 (재현 페이지) |
| `verify-map-keep-visible.mjs` | 9/9 통과 | 맞춤, 끌기와 휠 제한, 숨은 캔버스, 최소 배율에서 위치 유지 |
| `verify-viewer.mjs` | 73/73 통과 | Local 미니맵 12개 맵과 안내 언어 |
| `verify-localization.mjs` | 7/7 통과 | 번역 키와 자리 표시자, 미니맵 번역 |
| `resource-bundle.mjs check` | 통과 | 리소스 계약 |
| `test-resource-pipeline.mjs` | 7/7 통과 | 잘못된 지도 문서와 파일의 거절 |
| `dotnet build` | 오류 0 | 경고는 이번에 수정하지 않은 파일의 nullable 경고뿐 |

실제 사이트 검사에서 사이트가 고른 층과 기대 층은 층이 여럿인 11개 맵에서 모두 같았습니다. Labyrinth는 층이
하나라 층 판정에서 제외됩니다. 실제 WPF 앱은 프로젝트 지침에 따라 실행하지 않았습니다.

## 남은 일

- Debug 빌드 실행 확인(TESTING.md 12번 1항)
  - 스크린샷을 찍으면 Local과 같은 모양의 원과 방향 삼각형이 갱신되고, 앱 로그에 `[PilotBridge] Position rendered`가
    남는지
  - 새 스크린샷이 없는 동안 원이 커졌다 줄어들지 않는지
  - 층이 여럿인 맵(Labs, Reserve 등)에서 다른 층에 올라가거나 내려가 찍으면 지도가 그 층으로 바뀌는지. 바뀌지
    않으면 CDP로 `tanukiPilot.levelSync()`의 `state`를 확인
- 결정 필요
  - Online 위치 표시가 계속 실패할 때(사이트가 지도 상태를 변경해 `map-unavailable`이 이어지는 경우 등) 앱
    화면에 알리고 Local 전환을 권할지. 지금은 앱 로그에만 남습니다.
