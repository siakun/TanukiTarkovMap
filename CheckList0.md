# 프로젝트 리팩토링 체크리스트

## 기능 테스트 (수동 확인 필요)
- [ ] Normal 모드 ↔ Compact 모드 전환 정상 작동
- [ ] UI 요소 숨기기 체크박스 정상 작동
- [ ] 창 크기/위치 저장/복원 정상 작동
- [ ] 핀(TopMost) 기능 정상 작동
- [ ] 단축키 기능 정상 작동

---

## Code-behind 제거 리팩토링

### MainWindow.xaml.cs - 진행 중

#### ✅ 완료된 항목

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

#### 남은 항목 (검토 필요)

**비즈니스 로직 (현재 유지 - WebView2 의존성으로 인해)**
- [ ] `HandleCompactModeChanged()` - WebView2 JavaScript 호출 필요
- [ ] `HandleMapChanged()` - WebView2 네비게이션 필요
- [ ] `HandleHideWebElementsChanged()` - WebView2 JavaScript 호출 필요
- [ ] `HandleZoomLevelChanged()` - WebView2 ZoomFactor 설정 필요
- [ ] `HandleSelectedMapChanged()` - WebView2 네비게이션 필요
- [ ] `ShowWindowFromTray/HideWindowToTray` - Win32 API 직접 호출 필요

**WebView 관련 로직 (현재 유지 - WPF/WebView2 통합 제약)**
- [ ] `InitializeWebView()` - WebView 초기화
- [ ] `ConfigureWebView2Settings()` - WebView 설정
- [ ] `WebView_NavigationCompleted()` - 네비게이션 완료 처리
- [ ] `CoreWebView2_WebMessageReceived()` - 웹 메시지 수신 처리

**기타 (현재 유지)**
- [ ] `ApplyWebViewClipping()` - WebView2 클리핑 (WebView 생성 후 적용 필요)
- [ ] `TriggerWebViewResize()` - WebView 리사이즈 트리거

### 생성된 Behavior 파일들
- `Behaviors/WindowDragBehavior.cs` - 타이틀바 드래그 + 더블클릭 최대화
- `Behaviors/CompactModeDragBehavior.cs` - Compact 모드 창 드래그
- `Behaviors/TopBarAnimationBehavior.cs` - TopBar 자동 숨김/표시 애니메이션
- `Behaviors/WindowControlBehavior.cs` - 창 제어 (최소화/최대화/닫기)
- `Behaviors/HotkeyInputBehavior.cs` - 핫키 입력 캡처

### 참고
- `App.xaml.cs` - Application 수준 로직은 WPF 표준으로 허용
- `SettingsPage.xaml.cs` - ✅ 완료 (InitializeComponent만 포함)

---

## 용어 정리

- **Compact 모드**: 단일 윈도우의 작은 크기 모드
- **핀 모드**: TopMost 설정 (항상 위에 표시)
- **UI 요소 숨기기**: JavaScript로 웹 UI 패널 제거
