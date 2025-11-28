# 프로젝트 리팩토링 체크리스트

## 기능 테스트 (수동 확인 필요)
- [ ] Normal 모드 ↔ Compact 모드 전환 정상 작동
- [ ] UI 요소 숨기기 체크박스 정상 작동
- [ ] 창 크기/위치 저장/복원 정상 작동
- [ ] 핀(TopMost) 기능 정상 작동
- [ ] 단축키 기능 정상 작동

---

## WebView2 → CefSharp 마이그레이션

### 1단계: CefSharp 도입
- [ ] NuGet 패키지 추가 (CefSharp.Wpf)
- [ ] Microsoft.Web.WebView2 패키지 제거
- [ ] CEF 초기화 코드 추가 (App.xaml.cs)

### 2단계: WebBrowserUserControl 생성
- [ ] `Views/WebBrowserUserControl.xaml` 생성
- [ ] `Views/WebBrowserUserControl.xaml.cs` - InitializeComponent만 포함
- [ ] `ViewModels/WebBrowserViewModel.cs` 생성
- [ ] CefSharp 브라우저 래핑 및 MVVM 바인딩

### 3단계: MainWindow에서 WebView 로직 분리
- [ ] WebBrowserUserControl을 MainWindow에 배치
- [ ] MainWindow.xaml.cs에서 WebView 관련 코드 제거
  - [ ] `InitializeWebView()` → WebBrowserViewModel
  - [ ] `ConfigureWebView2Settings()` → WebBrowserViewModel
  - [ ] `WebView_NavigationCompleted()` → WebBrowserViewModel
  - [ ] `CoreWebView2_WebMessageReceived()` → WebBrowserViewModel
  - [ ] `ApplyWebViewClipping()` → UserControl 또는 Behavior
  - [ ] `TriggerWebViewResize()` → UserControl 또는 제거

### 4단계: Handle 메서드들 리팩토링
- [ ] `HandleCompactModeChanged()` → ViewModel 간 메시징 또는 서비스
- [ ] `HandleMapChanged()` → WebBrowserViewModel
- [ ] `HandleHideWebElementsChanged()` → WebBrowserViewModel
- [ ] `HandleZoomLevelChanged()` → WebBrowserViewModel
- [ ] `HandleSelectedMapChanged()` → WebBrowserViewModel

### 5단계: JavaScript 통신 재구현
- [ ] CefSharp IJavascriptCallback 또는 EvaluateScriptAsync 사용
- [ ] 기존 JavaScript 스크립트 호환성 확인

### 6단계: 정리
- [ ] 불필요한 WebView2 관련 코드 제거
- [ ] MainWindow.xaml.cs 최소화 (InitializeComponent 수준)

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

## 용어 정리

- **Compact 모드**: 단일 윈도우의 작은 크기 모드
- **핀 모드**: TopMost 설정 (항상 위에 표시)
- **UI 요소 숨기기**: JavaScript로 웹 UI 패널 제거
