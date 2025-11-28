# 프로젝트 리팩토링 체크리스트

## MainWindow.xaml.cs Code-behind 정리

### 핫키 관련 ✅
- [x] `InitializeHotkeyManager()` → HotkeyService로 이동
- [x] `UpdateHotkeySettings()` → HotkeyService로 이동
- [x] `_hotkeyManager` 필드 → HotkeyService에서 관리

### 트레이 관련 ✅
- [x] `ShowWindowFromTray()` → TrayWindowBehavior로 이동
- [x] `HideWindowToTray()` → TrayWindowBehavior로 이동

### ViewModel 연결 로직 ✅
- [x] `ConnectWebBrowserViewModel()` → Messenger 패턴으로 대체
- [x] `ViewModel_PropertyChanged()` → Messenger 패턴으로 대체

### 창 상태 관리 ✅
- [x] `MainWindow_StateChanged()` → WindowStateBehavior로 이동
- [x] `MainWindow_LocationChanged()` → WindowStateBehavior로 이동
- [x] `MainWindow_SizeChanged()` → WindowStateBehavior로 이동

### 기타 ✅
- [x] `ApplyTopmostSettings()` → 제거 (이미 바인딩으로 처리됨)
- [x] `InitializeSettingsPage()` → XAML에서 직접 생성으로 변경

---

## 기능 테스트 (수동 확인 필요)
- [ ] UI 요소 숨기기 체크박스 정상 작동
- [ ] 창 크기/위치 저장/복원 정상 작동
- [ ] 핀(TopMost) 기능 정상 작동
- [ ] 단축키 기능 정상 작동
- [ ] Pilot 연결 감지 및 맵 자동 이동 정상 작동

---

## 선택 사항 ✅
- [x] 주석의 WebView2 언급을 CefSharp으로 업데이트
