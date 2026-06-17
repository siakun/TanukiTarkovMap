# TanukiTarkovMap

<div align="center">
<img src="src/TanukiTarkovMap/Resources/icon.png" alt="TanukiTarkovMap" width="120" />

**Escape from Tarkov 인게임 GPS 스크린샷 좌표를 맵 위에 표시하는 데스크톱 오버레이 클라이언트**

</div>

게임 위에 항상 떠 있는 창으로 [tarkov-market.com](https://tarkov-market.com/pilot)의 인터랙티브 맵을 띄우고, 인게임 스크린샷에 기록된 좌표로 현재 위치를 실시간 표시합니다. 알트탭 없이 단축키 한 번으로 맵을 확인할 수 있습니다.

<!-- 스크린샷/GIF를 docs/ 폴더에 추가한 뒤 아래 주석을 해제하세요 -->
<!-- ![실행 화면](docs/screenshot.png) -->

---

## 프로젝트 배경

Escape from Tarkov은 인게임에서 스크린샷을 찍으면 파일명에 플레이어의 월드 좌표(X, Y, Z)와 카메라 회전값이 함께 기록됩니다. 개발사 Battlestate Games가 버그 리포트용으로 공식 제공하는 기능이며, 게임 메모리나 프로세스에 접근하지 않고 디스크에 저장된 파일만 읽습니다.

```
2025-12-20[02-09]-420.18, 1.00, 319.01-0.00089, -0.99307, -0.00012, -0.11748_15.11 (0).png
└ 날짜/시각 ┘ └ 위치 X, Y, Z ┘ └ 카메라 회전(쿼터니언) ┘
```

[tarkov-market.com](https://tarkov-market.com/pilot)의 Pilot 페이지는 이 좌표를 받아 맵 위에 현재 위치를 표시합니다. 다만 브라우저로 여는 웹앱은 구조적 한계가 있습니다. 게임 위에 항상 고정(Always-on-Top)하거나, 창을 반투명하게 만들거나, 전역 단축키로 토글하는 것처럼 운영체제 창을 직접 제어하는 일을 할 수 없습니다. 데스크톱 클라이언트가 필요한 이유가 여기에 있습니다.

이런 도구는 이미 여럿 있습니다. 원조는 tarkov-market.com이 공식 감지하는 [ggdiam/TarkovPilot](https://github.com/ggdiam/TarkovPilot)(.NET Framework, 트레이 전용 헬퍼)이고, 한국 커뮤니티의 [byeong1/Tarkov-Client](https://github.com/byeong1/Tarkov-Client)는 이를 Edge WebView2로 감싸 창 안에 띄웠습니다. 이 프로젝트는 byeong1 버전의 아이디어에서 출발했지만, 웹뷰 엔진을 CefSharp로 교체하고 창 제어, 좌표 동기화, 빌드 파이프라인을 처음부터 다시 설계했습니다.

---

## 주요 기능

| 기능 | 설명 |
|------|------|
| 맵 오버레이 | 게임 위에 항상 표시되는 인터랙티브 맵 (Always-on-Top) |
| 실시간 위치 추적 | 인게임 스크린샷을 찍으면 맵에 현재 위치를 자동 표시 |
| 자동 맵 전환 | 게임 로그를 감지해 입장한 맵으로 자동 전환 |
| 전역 단축키 토글 | 단축키(기본 `F11`) 한 번으로 맵 창 표시/숨김, 조합키와 특수키 지원 |
| 투명도 조절 | 상단 바가 숨겨지면 창이 반투명해져 게임 시야 방해를 최소화 |
| UI 정리 | 웹페이지의 불필요한 요소를 제거해 맵만 표시 |
| Goons 트래커 | Goons가 출몰 중인 맵 정보 표시 |
| 자동 업데이트 | 새 버전 출시 시 Velopack으로 자동 업데이트 |

---

## 기술적 구현

이 프로젝트의 핵심은 "웹앱이 못 하는 일을 네이티브 클라이언트가 대신하는 것"입니다. 각 기능을 어떤 문제 때문에, 어떤 방식으로 풀었는지 정리합니다.

### 1. Edge WebView2에서 CefSharp로 포팅

출발점이 된 도구는 Windows에 내장된 Edge WebView2를 썼지만, 이 프로젝트는 웹뷰 엔진을 CefSharp(Chromium Embedded Framework)로 교체했습니다. WebView2는 런타임이 사용자 환경에 의존하는 반면, CefSharp는 Chromium을 앱과 함께 배포해 환경에 독립적이고 렌더 프로세스, 스크립트 주입, 줌 같은 동작을 더 직접 제어할 수 있습니다. 페이지 로드 시점(`FrameLoadEnd`)에 맞춰 커스터마이징 스크립트를 주입하고, `JavascriptMessageReceived`로 웹에서 앱으로 오는 메시지를 받는 방식으로 통합했습니다.

### 2. P/Invoke(user32.dll)로 네이티브 창 제어

웹앱이 할 수 없는 OS 창 제어를 P/Invoke로 직접 구현했습니다.

- **Always-on-Top**: `SetWindowPos`에 `HWND_TOPMOST`를 적용해 게임 위에 고정
- **반투명 창**: `GetWindowLong`/`SetWindowLong`으로 `WS_EX_LAYERED` 스타일을 켜고 `SetLayeredWindowAttributes(LWA_ALPHA)`로 알파값 조절
- **전역 단축키**: 저수준 키보드 훅(`SetWindowsHookEx`)으로 게임이 포커스를 가진 상태에서도 토글 동작

모든 Win32 호출은 `PInvoke.cs` 한 곳에 모아 선언하고, `WindowTopmost`와 `WindowTransparency` 같은 의도 단위 래퍼로 감싸 호출부가 플래그를 직접 다루지 않도록 했습니다.

### 3. 실시간 좌표 동기화 (FileSystemWatcher + 인프로세스 WebSocket)

스크린샷 폴더를 `FileSystemWatcher`로 실시간 감시하다가 새 파일이 생기면, 파일명을 WebSocket으로 임베디드 웹 클라이언트에 전달합니다. 좌표 파싱과 맵 표시는 tarkov-market Pilot 클라이언트가 담당합니다. WebSocket 서버는 별도 프로세스가 아니라 앱 안에서 ASP.NET Core Kestrel로 직접 호스팅하며, 포트 `5123`은 tarkov-market.com이 로컬 헬퍼 앱을 감지하는 규약과 호환됩니다. 원조 TarkovPilot이 별도 트레이 헬퍼로 WebSocket을 띄우던 것과 달리, 같은 규약을 최신 ASP.NET Core 기반 인프로세스 서버로 다시 구현한 셈입니다. 게임 로그도 함께 감시(`LogsWatcher`)해 플레이어가 입장한 맵을 자동으로 전환합니다.

### 4. CefSharp와 JavaScript 양방향 통신

웹 UI를 앱에 맞게 다듬는 로직은 JavaScript로 주입합니다. `.js` 파일을 Embedded Resource로 묶어 `JavaScriptLoader`로 읽고, 페이지 로드 후 `EvaluateScriptAsync`로 실행합니다(헤더와 푸터 제거, 패널 토글, 위치 마커에 방향 표시 추가 등). 반대로 웹에서 일어난 사건(맵 변경, 연결 상태)은 `postMessage`로 보내 `JavascriptMessageReceived`에서 받고, CommunityToolkit.Mvvm의 `WeakReferenceMessenger`로 ViewModel에 전달합니다. C#과 JS의 경계를 메시지로 느슨하게 연결했습니다.

### 5. MVVM 아키텍처와 DI

코드비하인드(`*.xaml.cs`)에는 로직을 두지 않는다는 원칙을 지켰습니다. UI 인터랙션은 Microsoft.Xaml.Behaviors 기반 Behavior로 분리하고(창 드래그, 트레이 최소화, 단축키 입력 캡처 등), 데이터와 비즈니스 로직은 ViewModel과 Service에 둡니다. 서비스는 Microsoft.Extensions.DependencyInjection으로 등록하고 `ServiceLocator`로 접근하며, ViewModel 사이 통신은 직접 참조 대신 Messenger로 처리해 결합도를 낮췄습니다.

### 6. 릴리스 자동화 (GitHub Actions + Velopack)

버전 태그(`v1.0.0` 또는 `0.1.0` 형태)를 push하면 GitHub Actions가 self-contained로 publish하고, Velopack(`vpk`)으로 설치 파일과 포터블 zip을 패키징해 GitHub Release에 자동 업로드합니다. 사용자 쪽에서는 앱 시작 시 Velopack `UpdateManager`가 새 버전을 확인하고 조용히 받아 다음 실행에 적용합니다. 빌드부터 배포, 자동 업데이트까지 태그 하나로 이어집니다.

---

## 기술 스택

| 구분 | 사용 기술 |
|------|-----------|
| 언어, 런타임 | C#, .NET 8.0 |
| UI | WPF, MVVM (CommunityToolkit.Mvvm), Microsoft.Xaml.Behaviors |
| 웹뷰 | CefSharp.Wpf.NETCore (Chromium) |
| 네이티브 제어 | P/Invoke (user32.dll) |
| 통신 | ASP.NET Core Kestrel WebSocket (port 5123) |
| DI | Microsoft.Extensions.DependencyInjection |
| 트레이 | Hardcodet.NotifyIcon.Wpf |
| 배포, 자동 업데이트 | Velopack, GitHub Actions |

---

## 설치

1. [Releases 페이지](https://github.com/siakun/TanukiTarkovMap/releases/latest)에서 최신 설치 파일(`TanukiTarkovMap-Setup-x64.exe`)을 내려받습니다.
2. 실행해 설치하면 이후 업데이트는 자동으로 적용됩니다.

> 요구 사항: Windows 10/11 (x64)

---

## 사용법

1. TanukiTarkovMap을 실행합니다.
2. 게임을 시작하면 입장한 맵으로 자동 전환됩니다.
3. 게임 중 단축키(기본 `F11`, 설정에서 변경 가능)로 맵 창을 켜고 끕니다.
4. 인게임에서 스크린샷을 찍으면 맵 위에 현재 위치가 표시됩니다.

설정 창에서 단축키, 투명도, UI 표시 여부를 바꿀 수 있습니다.

---

## 빌드 (개발자용)

```bash
cd src
dotnet build
```

아키텍처와 설계 문서는 [`PROJECT.md`](PROJECT.md)에 정리되어 있습니다.

---

## 라이선스

[MIT License](LICENSE)

> 이 프로젝트는 Battlestate Games 및 Tarkov Market과 제휴 관계가 없는 비공식 도구입니다. 게임 메모리나 프로세스에 접근하지 않고, 게임이 디스크에 남기는 파일(스크린샷, 로그)만 읽습니다.
