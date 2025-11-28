# TanukiTarkovMap 프로젝트 설계 문서

## 개요

Escape from Tarkov 게임을 위한 인터랙티브 맵 뷰어 애플리케이션.
CefSharp를 통해 tarkov-market.com의 맵을 표시하며, 게임 로그 감시를 통한 자동 맵 전환 기능을 제공.

---

## 기술 스택

| 항목 | 기술/라이브러리 |
|------|-----------------|
| UI Framework | WPF (Windows Presentation Foundation) |
| Target Framework | .NET 8.0 |
| 웹뷰 | CefSharp.Wpf.NETCore |
| DI/IoC | Microsoft.Extensions.DependencyInjection |
| MVVM | CommunityToolkit.Mvvm |
| JSON | Newtonsoft.Json |
| 시스템 트레이 | Hardcodet.NotifyIcon.Wpf |

---

## 윈도우 모드 설계

### 모드 개요

| 모드 | 설명 | TopMost |
|------|------|---------|
| **Normal 모드** | 일반 크기 윈도우 (기본값: 1000x700) | 핀 설정에 따름 |
| **Compact 모드** | 소형 윈도우 (기본값: 300x250) | 항상 활성화 |

### 모드 전환 흐름

```
Normal 모드 ←→ Compact 모드
     ↑              ↑
     │              │
  핀 토글      자동 TopMost
```

### 핵심 속성 (MainWindowViewModel)

```csharp
// 모드 상태
bool IsCompactMode        // Compact 모드 활성화 여부
bool IsAlwaysOnTop        // 핀 모드 (Normal에서 TopMost)
bool IsTopmost            // 실제 TopMost 상태 (바인딩용)

// 핫키 설정
bool HotkeyEnabled        // 핫키 활성화 여부
string HotkeyKey          // 핫키 키 (기본: F11)

// UI 설정
bool HideWebElements      // 웹 UI 요소 숨김 여부
```

### 창 크기/위치 저장 구조

```
WindowStateManager
├── NormalModeRect      // Normal 모드 위치/크기
└── CompactModeRect     // Compact 모드 위치/크기 (모든 맵 공유)
```

---

## 프로젝트 구조

```
src/TanukiTarkovMap/
├── Models/
│   ├── Data/           # 데이터 모델 (MapInfo, Settings 등)
│   ├── FileSystem/     # 파일 시스템 감시 (LogsWatcher, ScreenshotsWatcher)
│   ├── JavaScript/     # WebView2 JavaScript 통합
│   ├── Services/       # 비즈니스 로직 서비스
│   └── Utils/          # 유틸리티 (Logger, HotkeyManager 등)
├── ViewModels/         # MVVM ViewModel
├── Views/              # WPF XAML 뷰
├── Converters/         # WPF Value Converters
└── Resources/          # XAML 리소스 (스타일)
```

---

## 서비스 아키텍처

### ServiceLocator 패턴

모든 서비스는 `ServiceLocator`를 통해 DI 컨테이너로 관리됨.

```csharp
// 서비스 접근
ServiceLocator.WebViewUIService
ServiceLocator.WindowBoundsService
ServiceLocator.MapEventService
ServiceLocator.WindowStateManager
```

### 주요 서비스

| 서비스 | 역할 |
|--------|------|
| `WebViewUIService` | WebView2 UI 요소 가시성 제어 |
| `WindowBoundsService` | 창 경계 체크 및 화면 내 위치 보정 |
| `WindowStateManager` | Normal/Compact 모드별 창 상태 저장/복원 |
| `MapEventService` | 맵 변경 및 스크린샷 이벤트 발행 |
| `Settings` | 애플리케이션 설정 로드/저장 (JSON) |

### 서비스 생성자 규칙

```csharp
// internal 생성자로 외부 new 방지
internal ServiceName() { }

// ServiceLocator에서 Factory 패턴으로 생성
services.AddSingleton(_ => new ServiceName());
```

---

## 이벤트 흐름

### 맵 자동 전환

```
타르코프 로그 파일 변경
       ↓
  LogsWatcher 감지
       ↓
  MapEventService.RaiseMapChanged()
       ↓
  MainWindowViewModel.OnMapEventReceived()
       ↓
  ChangeMapCommand 실행
       ↓
  WebView2 URL 변경
```

### 스크린샷 감지

```
타르코프 스크린샷 생성
       ↓
  ScreenshotsWatcher 감지
       ↓
  MapEventService.RaiseScreenshotTaken()
       ↓
  MainWindowViewModel.OnScreenshotEventReceived()
       ↓
  Compact 모드 자동 활성화
```

---

## 설정 파일 구조

`settings.json` 위치: 실행 파일과 동일 디렉토리

```json
{
  "NormalLeft": 100,
  "NormalTop": 100,
  "NormalWidth": 1000,
  "NormalHeight": 700,
  "HotkeyEnabled": true,
  "HotkeyKey": "F11",
  "IsAlwaysOnTop": false,
  "MapSettings": {
    "default": {
      "Left": 1600,
      "Top": 800,
      "Width": 300,
      "Height": 250
    }
  }
}
```

---

## UI 요소 숨기기 로직

### 개념

tarkov-market.com 웹페이지의 UI 요소들을 JavaScript로 제어하여 맵만 깔끔하게 표시.

### 요소 분류

| 요소 | 숨김 조건 | 복원 가능 |
|------|-----------|-----------|
| **헤더 (header)** | 항상 숨김 | X |
| **푸터 (footer-wrap)** | 항상 숨김 | X |
| **좌측 패널 (panel_left)** | 체크 시 숨김 | O |
| **우측 패널 (panel_right)** | 체크 시 숨김 | O |
| **상단 패널 (panel_top)** | 체크 시 숨김 | O |

### 동작 방식

```
페이지 로드 완료
       ↓
INIT_SCRIPT 실행 (함수들을 window 객체에 등록)
       ↓
헤더/푸터 항상 숨김 (window.hideHeader(), window.hideFooter())
       ↓
"UI 요소 숨기기" 체크 여부 확인
       ↓
  ┌─ 체크됨: 패널들도 숨김 (window.hidePanelLeft() 등)
  └─ 해제됨: 패널들 복원 (window.restorePanels())
       ↓
resize 이벤트 발생 → SVG 맵 레이아웃 재계산
```

### 핵심 원칙

1. **헤더/푸터는 항상 숨김**: 맵 이동, 체크 해제와 무관하게 절대 표시하지 않음
2. **패널만 토글 대상**: "UI 요소 숨기기" 체크박스는 좌/우/상단 패널에만 적용
3. **레이아웃 재계산**: 요소 숨김 후 `window.dispatchEvent(new Event('resize'))` 호출로 검은 영역 방지

### JavaScript 스크립트 구조

프로젝트의 JavaScript는 다음 패턴으로 관리됩니다:

```
Models/JavaScript/
├── Scripts/                      # 실제 JavaScript 파일 (Embedded Resource)
│   ├── web-elements-control.js   # UI 요소 제어 함수 정의
│   ├── page-layout.js            # 마진/패딩 제거
│   ├── connection-detector.js    # 연결 상태 감지
│   └── ...
├── WebElementsControl.js.cs      # C# 래퍼 (함수 호출용 상수)
├── PageLayout.js.cs              # C# 래퍼
├── ConnectionDetector.js.cs      # C# 래퍼
└── JavaScriptLoader.cs           # Embedded Resource 로더
```

**동작 원리:**
1. `.js` 파일: IIFE 패턴으로 함수들을 `window` 객체에 등록
2. `.js.cs` 파일: `JavaScriptLoader.Load()`로 스크립트 로드 + 함수 호출 상수 정의
3. `WebViewUIService`: 초기화 스크립트 → 함수 호출 순서로 실행

**예시 (WebElementsControl):**
```csharp
// 1. 함수 등록 (INIT_SCRIPT)
await browser.EvaluateScriptAsync(WebElementsControl.INIT_SCRIPT);

// 2. 함수 호출
await browser.EvaluateScriptAsync(WebElementsControl.HIDE_HEADER);  // "window.hideHeader();"
```

### 관련 파일

- `Scripts/web-elements-control.js`: JavaScript 함수 정의 (IIFE)
- `WebElementsControl.js.cs`: C# 래퍼 클래스 (INIT_SCRIPT, HIDE_* 상수)
- `WebViewUIService.cs`: 브라우저에 스크립트 실행 서비스
- `WebBrowserViewModel.cs`: 페이지 로드 시 `ApplyUIVisibilityAsync()` 호출
- `JavaScriptLoader.cs`: Embedded Resource에서 .js 파일 로드

---

## 용어 정리

| 용어 | 설명 |
|------|------|
| **Normal 모드** | 일반 크기의 윈도우 상태 |
| **Compact 모드** | 소형 윈도우 상태 (게임 중 오버레이용) |
| **핀 모드** | TopMost 설정 (항상 위에 표시) |
| **UI 요소 숨김** | JavaScript로 웹페이지 패널 제거 (헤더/푸터 제외) |

---

## 히스토리

- **2025-11-28**: PIP 용어를 Compact로 전면 리팩토링
- 기존 Fork 프로젝트의 별도 PIP 윈도우 방식에서 단일 윈도우 모드 전환 방식으로 변경
