# 로컬 맵 뷰어 재구성 설계와 인수인계

<!--
이 문서는 Claude가 조사와 설계를 마치고 Codex에게 구현을 넘기며 씁니다. 결정된 사항과 이미
확인한 사실을 적어, 받는 쪽이 같은 조사를 되풀이하지 않게 하는 것이 목적입니다.
2026-08-21에 기록했습니다.
-->

## 목표

지금 로컬 모드는 tarkov-market이 컴파일한 사이트를 통째로 사본으로 두고 요청을 가로채 돌려줍니다.
동작하지만 사본이 압축된 번들 덩어리라 무엇이 들어 있는지 사람이 읽을 수 없고, 갱신하면 `git diff`가
의미를 주지 못하며, 사이트가 구조를 바꾸면 통째로 다시 받는 것 말고는 방법이 없습니다.

이것을 우리가 읽고 고칠 수 있는 형태로 재구성합니다.

- 맵 데이터는 `resources/` 아래 폴더별로 두고, 갱신은 그 폴더의 파일만 갈아끼우면 끝나게 합니다
- 뷰어는 우리가 만든 HTML/CSS/JS로 그 데이터를 읽어 그립니다
- 사이트에서 최신을 받아오는 수집 도구와, 받은 것이 온전한지 보는 검사 도구를 함께 둡니다

## 이미 확인한 사실

조사는 끝나 있습니다. 아래는 실측으로 확인한 내용이니 다시 확인할 필요가 없습니다.
모든 원본은 저장소의 `archive/` 안에 있으므로 네트워크 없이도 추출을 시작할 수 있습니다.

### 1. 맵 지형은 완결된 SVG 하나입니다

브라우저에서 `document.querySelector('svg.svg-map').outerHTML`이 그대로 지형 전체입니다.
shoreline 기준 254,965바이트이고, 자식이 레이어별로 id를 갖습니다.

```
defs, #wrapper, #map-bg, #water, #swamp, #mines, #roads, #roads-space,
#fence, #rocks, #grid, #buildings, #basement, #main, #level2, #level3, #border
```

`#basement`, `#main`, `#level2`, `#level3`이 층 구분이므로 레벨 전환은 이 그룹의 표시만 바꾸면 됩니다.
`#wrapper`는 캔버스 전체를 덮는 배경이라 그림 범위 계산에서 제외해야 합니다(뷰박스의 98% 이상을
덮는 자식이 배경입니다).

사본 안 어느 blob이 어느 맵의 지형인지는 내용으로 찾습니다. 예를 들어 shoreline은 뷰박스가
`0 0 3700 3100`이라 `3700 3100` 문자열을 담은 js가 그 맵의 지형 조각입니다.

### 2. 맵 설정은 평범한 객체 리터럴입니다

사본의 js 조각 하나(`xOffset` 문자열을 담은 파일, 525KB)에 맵별 설정이 들어 있습니다.

```js
{
  "ground-zero": { size: {width:2800, height:3100}, zoom: 1, minZoom: 0.2, maxZoom: 10,
                   transform: { rotate: 90, xOffset: 1600, yOffset: 1300,
                                invertX: false, invertY: false, ratio: 2 } },
  factory:      { size: {width:3600, height:3600}, zoom: 0.7, minZoom: 0.12, maxZoom: 10,
                   transform: { rotate: 0, xOffset: 1800, yOffset: 1850,
                                invertX: false, invertY: false, ratio: 10 } },
  ...
}
```

12개 맵 전부 이 형태입니다. 정규식으로 뽑거나 브라우저에서 평가해 JSON으로 저장하면 됩니다.

### 3. 좌표 변환은 스무 줄 남짓입니다

게임 좌표를 맵 좌표로 옮기는 함수가 사본에 있습니다(최소화된 이름은 빌드마다 바뀌므로 이름이 아니라
규칙을 근거로 삼습니다).

```js
gamePosToMapPos = (x, y, t) => {
  let p = [x, y];
  if (t.rotate) p = rotate(p, t.rotate);   // 회전
  return { x: round(applyX(p[0], t)), y: round(applyY(p[1], t)) };
};
// 방향 각도 보정: 화면각 = 게임각 + (270 - t.rotate)
// 맵 좌표 -> 화면 좌표: screen = viewOrigin + mapPos * zoom
```

`applyX`, `applyY`는 `xOffset`, `yOffset`, `ratio`, `invertX`, `invertY`를 쓰는 1차식입니다.
정확한 식은 사본에서 확인해 재구현하고, **반드시 사이트와 대조 검증**합니다(검증 방법은 아래).

### 4. 마커와 퀘스트 데이터는 페이지 payload에 실려 옵니다

API(`/api/be/quests/all`, `/api/be/markers/list`)의 사본 응답은 "변경 없음" 껍데기입니다. 실제 데이터는
서버가 렌더한 HTML 안에 들어 있습니다. 저장소가 완전히 빈 새 프로필로 사본만 열어도 좌측 목록에
추출구 8개, 스폰 26개, 퀘스트 90개가 나오는 것으로 확인했습니다.

따라서 추출은 API가 아니라 **페이지를 열어 그 안의 데이터를 읽는 방식**이어야 합니다.
가장 확실한 경로는 렌더된 페이지에서 사이트가 이미 만들어 둔 마커 목록을 읽어 JSON으로 내보내는 것입니다.
정확한 접근 경로는 1단계 스파이크에서 확정합니다.

## 결정된 사항

사용자와 합의한 내용입니다. 바꾸려면 먼저 사용자에게 확인하십시오.

| 항목 | 결정 |
|---|---|
| 뷰어 기술 | 생 JS(ES 모듈) + SVG. 빌드 단계 없음. 릴리스에 npm이 끼지 않게 함 |
| 1차 범위 | 지형, 레벨 전환, 추출구와 스폰 마커, 내 위치 표시. 퀘스트 필터와 검색은 그다음 |
| 기존 로컬 모드 | 3단계까지 그대로 둠. 새 뷰어가 안정된 뒤 4단계에서 교체 |
| 출처 표기 | 데이터 출처(tarkov-market, HighTek 레이어)를 뷰어 어딘가에 남김. 맵 위에 겹치지 않는 자리로 |

UI가 커져 프레임워크가 필요해지면 Preact + htm을 import 한 줄로 붙이는 쪽을 씁니다. 빌드 도입은
그때 다시 논의합니다.

## 리소스 구조

```
resources/
  manifest.json              스키마 판, 수집 시각, 맵 목록
  maps/
    shoreline/
      map.svg                지형 (레이어 id 유지)
      meta.json              size, zoom, minZoom, maxZoom, transform, levels
      markers.json           추출구/스폰/퀘스트 마커
    streets/ ...
  assets/
    icons/*.svg
    fonts/*.woff2
viewer/
  index.html
  main.js                    진입점, 맵 선택과 초기화
  map-view.js                SVG 로드, 팬/줌, 레벨 전환
  markers.js                 마커 그리기와 필터
  coords.js                  좌표 변환 (게임 <-> 맵 <-> 화면)
  style.css
```

원칙은 하나입니다. **`resources/` 아래에는 데이터만 두고 코드를 섞지 않습니다.** 갱신이 이 폴더를
갈아끼우는 일이 되어야 뷰어 코드를 건드리지 않고 최신을 따라갈 수 있습니다.

`meta.json`에는 스키마 판 번호를 둡니다. 뷰어는 아는 판만 읽고 모르는 필드는 무시합니다.

## 도구

저장소에 이미 같은 성격의 도구가 셋 있습니다. 그 형태를 따르십시오
(의존성 없음, Node 22 내장 fetch/WebSocket만, `--json` 없이 사람이 읽는 출력, 실패 시 종료 코드 1).

- `tools/cdp-debug.mjs` 실행 중인 앱에 붙어 DOM과 스크린샷 조회
- `tools/archive-maps.mjs` 실제 브라우저로 페이지를 열어 응답 저장 (수집 도구의 본보기)
- `tools/verify-archive.mjs` 네트워크를 막고 사본만으로 맵이 뜨는지 검사 (검사 도구의 본보기)

새로 만들 것:

- `tools/fetch-resources.mjs` 사이트에서 최신을 받아 `resources/`를 갱신. `--maps lab,customs`로 일부만
- `tools/verify-resources.mjs` 받은 리소스로 뷰어가 실제로 뜨는지 검사

**포트 자리**: 9222는 실행 중인 앱, 9223은 재현용 브라우저, 9224는 archive-maps, 9225는 verify-archive가
씁니다. 새 도구는 9226 이후를 쓰십시오. 같은 포트를 쓰면 명령이 실행 중인 앱으로 흘러 사용자가 보는
화면을 조작하게 됩니다(실제 사고 사례).

## 단계와 완료 기준

### 1단계 스파이크: 맵 하나

산출물: `resources/maps/shoreline/`(map.svg, meta.json)와 최소 뷰어, 추출 도구의 뼈대.

완료 기준:
- 브라우저에서 `viewer/index.html?map=shoreline`을 열면 지형이 뜨고 휠 확대와 끌기가 됩니다
- 레벨 그룹(`#basement`, `#main`, `#level2`, `#level3`)을 켜고 끌 수 있습니다
- 게임 좌표를 넣으면 그 자리에 마커가 찍힙니다
- **좌표 검증**: 같은 게임 좌표를 사이트(온라인)와 우리 뷰어에 각각 넣어 맵 좌표가 일치하는지 대조한
  결과를 숫자로 보고합니다. 사이트 쪽 값은 실행 중인 페이지에서 얻을 수 있습니다

### 2단계: 데이터

산출물: `markers.json` 스키마와 추출 경로, 마커 그리기와 종류별 켜고 끄기.

완료 기준: 추출구와 스폰이 사이트와 같은 자리에 같은 개수로 뜹니다. 개수를 사이트의 좌측 목록 숫자와
대조해 보고합니다.

### 3단계: 전체와 도구

산출물: 12개 맵, `fetch-resources.mjs`, `verify-resources.mjs`.

완료 기준: 사본을 지우고 도구만으로 `resources/`를 다시 만들 수 있고, 검사 도구가 12개 맵 전부
통과합니다.

### 4단계: 앱 통합 (여기부터는 사용자 확인 후)

로컬 모드가 사이트 사본 대신 우리 뷰어를 열게 바꿉니다. 이때 `MapArchive`,
`ArchiveResourceRequestHandlerFactory`, `archive/`가 필요 없어집니다. 위치 표시는 `window.pilot` 대신
뷰어의 함수를 부릅니다(스크린샷 파일명 파싱은 앱에 이미 있습니다).

## 지켜야 할 것

- 저장소 루트의 `CLAUDE.md`를 먼저 읽으십시오. 커밋 메시지 컨벤션, 문체, MVVM과 문서 동기화 규칙이
  거기 있습니다
- **push하지 마십시오.** 커밋까지만 하고 사용자가 직접 올립니다
- **앱을 실행하지 마십시오.** 빌드는 해도 됩니다
- main에 직접 커밋하지 말고 작업 브랜치에서 진행하십시오
- 실측한 값은 근거와 함께 그 값을 쓰는 파일 안에 주석으로 적으십시오. 별도 문서에만 적힌 근거는 다음
  사람이 찾지 못합니다
- 사이트 구조를 다룰 때는 `docs/20260818-embedded-site-control.md`(임베디드 웹페이지 제어 레퍼런스)를
  참고하십시오. 주입, CDP 관측, 개입 기법과 이미 밟은 함정이 정리돼 있습니다

## 참고 문서

- `docs/20260818-offline-map.md` 지금 로컬 모드의 설계 근거와 한계
- `docs/20260818-embedded-site-control.md` 사이트를 다루는 기법 레퍼런스
- `archive/README.md` 사본을 다시 만드는 절차
- `PROJECT.md` 앱 구조와 스크립트 주입 흐름
