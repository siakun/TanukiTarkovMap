# 프로젝트 리팩토링 체크리스트

## MainWindow.xaml.cs 추가 정리
- [ ] `ShowWindowFromTray()` / `HideWindowToTray()` → 서비스로 이동
- [ ] `MainWindow_PreviewKeyDown()` → Behavior로 이동
- [ ] `UpdateHotkeySettings()` → ViewModel로 이동

---

## 기능 테스트 (수동 확인 필요)
- [ ] UI 요소 숨기기 체크박스 정상 작동
- [ ] 창 크기/위치 저장/복원 정상 작동
- [ ] 핀(TopMost) 기능 정상 작동
- [ ] 단축키 기능 정상 작동
- [ ] Pilot 연결 감지 및 맵 자동 이동 정상 작동

---

## 선택 사항
- [ ] 주석의 WebView2 언급을 CefSharp으로 업데이트
