# 프로젝트 리팩토링 체크리스트

## 기능 테스트 (수동 확인 필요)
- [ ] Normal 모드 ↔ Compact 모드 전환 정상 작동
- [ ] UI 요소 숨기기 체크박스 정상 작동
- [ ] 창 크기/위치 저장/복원 정상 작동
- [ ] 핀(TopMost) 기능 정상 작동
- [ ] 단축키 기능 정상 작동
- [ ] Pilot 연결 감지 및 맵 자동 이동 정상 작동

---

## WebView2 → CefSharp 마이그레이션

### 1단계: CefSharp 도입
- [x] NuGet 패키지 추가 (CefSharp.Wpf.NETCore)
- [x] Microsoft.Web.WebView2 패키지 제거
- [x] CEF 초기화 코드 추가 (App.xaml.cs - InitializeCef())

### 2단계: WebBrowserUserControl 생성
- [x] `Views/WebBrowserUserControl.xaml` 생성
- [x] `Views/WebBrowserUserControl.xaml.cs` - 브라우저 생성 및 이벤트 연결
- [x] `ViewModels/WebBrowserViewModel.cs` 생성
- [x] CefSharp 브라우저 래핑 및 MVVM 바인딩

### 3단계: MainWindow에서 WebView 로직 분리
- [x] WebBrowserUserControl을 MainWindow에 배치
- [x] MainWindow.xaml.cs에서 WebView 관련 코드 제거
  - [x] `InitializeWebView()` → WebBrowserViewModel.SetBrowser()
  - [x] `ConfigureWebView2Settings()` → CefSettings (App.xaml.cs)
  - [x] `WebView_NavigationCompleted()` → WebBrowserViewModel.OnFrameLoadEnd()
  - [x] `CoreWebView2_WebMessageReceived()` → WebBrowserViewModel.OnJavascriptMessageReceived()

### 4단계: Handle 메서드들 리팩토링
- [x] `HandleCompactModeChanged()` → MainWindow에서 ViewModel 연동
- [x] `HandleMapChanged()` → WebBrowserViewModel.NavigateToMap()
- [x] `HandleHideWebElementsChanged()` → WebBrowserViewModel.HideWebElements 속성
- [x] `HandleZoomLevelChanged()` → WebBrowserViewModel.ZoomLevel 속성
- [x] `HandleSelectedMapChanged()` → WebBrowserViewModel.NavigateToMap()

### 5단계: JavaScript 통신 재구현
- [x] CefSharp.PostMessage 사용 (window.chrome.webview.postMessage 대체)
- [x] JavascriptMessageReceived 이벤트로 메시지 수신
- [x] 기존 JavaScript 스크립트 CefSharp 호환으로 수정
  - [x] connection-detector.js
  - [x] page-layout.js
  - [x] web-elements-control.js

### 6단계: 정리
- [x] 불필요한 WebView2 관련 코드 제거 (csproj에서 패키지 제거됨)
- [ ] 주석의 WebView2 언급을 CefSharp으로 업데이트 (선택 사항)

---

## JavaScript 스크립트 리팩토링

### web-elements-control.js 구조 개선
- [x] .js 파일에 IIFE 패턴으로 함수 정의 (window 객체에 등록)
- [x] .js.cs 파일에서 JavaScriptLoader.Load()로 초기화 스크립트 로드
- [x] 함수 호출은 간단한 상수로 정의 ("window.hideHeader();" 등)
- [x] WebViewUIService에서 INIT_SCRIPT 먼저 실행 후 함수 호출

### UI 요소 숨김 로직 개선
- [x] 헤더/푸터는 항상 숨김 (복원 불가)
- [x] 패널만 토글 대상 (UI 요소 숨기기 체크박스)
- [x] resize 이벤트 발생으로 레이아웃 재계산

---

## Code-behind 제거 리팩토링 (이전 완료)

### ✅ 완료된 항목

**이벤트 핸들러 → Behavior 분리**
- [x] `TitleBar_MouseLeftButtonDown` → WindowDragBehavior
- [x] `Window_MouseLeftButtonDown` → CompactModeDragBehavior
- [x] `MainWindow_Activated/Deactivated` → TopBarAnimationBehavior
- [x] `MainWindow_MouseEnter/MouseLeave` → TopBarAnimationBehavior
- [x] `AnimateTopBar()` → TopBarAnimationBehavior

**버튼 클릭 핸들러 → Command/Behavior 바인딩**
- [x] `Settings_Click` → ToggleSettingsCommand
- [x] `CloseSettings_Click` → CloseSettingsCommand
- [x] `PinToggle_Click` → TogglePinModeCommand
- [x] `Minimize_Click` → WindowControlBehavior
- [x] `MaximizeRestore_Click` → WindowControlBehavior
- [x] `Close_Click` → WindowControlBehavior

### 생성된 Behavior 파일들
- `Behaviors/WindowDragBehavior.cs` - 타이틀바 드래그 + 더블클릭 최대화
- `Behaviors/CompactModeDragBehavior.cs` - Compact 모드 창 드래그
- `Behaviors/TopBarAnimationBehavior.cs` - TopBar 자동 숨김/표시 애니메이션
- `Behaviors/WindowControlBehavior.cs` - 창 제어 (최소화/최대화/닫기)
- `Behaviors/HotkeyInputBehavior.cs` - 핫키 입력 캡처

---

## 남은 작업

### MainWindow.xaml.cs 추가 정리 (선택 사항)
- [ ] `HandleCompactModeChanged()` → Behavior 또는 서비스로 이동
- [ ] `ShowWindowFromTray()` / `HideWindowToTray()` → 서비스로 이동
- [ ] `MainWindow_PreviewKeyDown()` → Behavior로 이동
- [ ] `UpdateHotkeySettings()` → ViewModel로 이동

### 문서 업데이트
- [x] PROJECT.md에 CefSharp 기술 스택 반영
- [x] PROJECT.md에 UI 요소 숨기기 로직 문서화
- [x] PROJECT.md에 JavaScript 스크립트 구조 문서화

---

## 용어 정리

- **Compact 모드**: 단일 윈도우의 작은 크기 모드
- **핀 모드**: TopMost 설정 (항상 위에 표시)
- **UI 요소 숨기기**: JavaScript로 웹 UI 패널 제거 (헤더/푸터 제외)
