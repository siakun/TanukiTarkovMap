# PIP 기능 잔여물 제거 체크리스트

## 프로젝트 배경

### 기존 PIP 기능 (Fork 프로젝트)
- **별도의 PIP 윈도우**: 투명한 소형 윈도우를 별도로 띄워 미니맵 표시
- **복잡한 구조**: PipWindow.xaml, PipController.cs (774줄), WindowTransparency 등

### 현재 프로젝트 구조
- **단일 윈도우**: 하나의 윈도우에서 Normal 모드와 Compact 모드 전환
- **핀 기능**: TopMost 설정으로 항상 위에 표시
- **UI 요소 숨기기**: JavaScript로 웹뷰의 UI 패널 숨김/표시

---

## 작업 완료

### Phase 3: 전면 리팩토링 - ✅ 완료 (2025-11-28)

#### WindowStateManager.cs
- [x] 변수명 변경: `_pipModeRect` → `_compactModeRect`
- [x] 메서드명 변경: `GetPipModeRect()` → `GetCompactModeRect()`
- [x] 메서드명 변경: `UpdatePipModeRect()` → `UpdateCompactModeRect()`

#### MainWindowViewModel.cs
- [x] `IsPipMode` → `IsCompactMode`
- [x] `PipHotkeyEnabled` → `HotkeyEnabled`
- [x] `PipHotkeyKey` → `HotkeyKey`
- [x] `PipHideWebElements` → `HideWebElements`
- [x] Region 주석 변경: `#region PIP Mode Properties` → `#region Compact Mode Properties`
- [x] 관련 메서드명 변경: `EnterPipMode()` → `EnterCompactMode()`, `ExitPipMode()` → `ExitCompactMode()`

#### MainWindow.xaml
- [x] 바인딩 속성명 업데이트 (`HideWebElements`)

#### MainWindow.xaml.cs
- [x] 모든 PIP 관련 참조를 Compact로 변경
- [x] `WindowBoundsChangedEventArgs.IsPipMode` → `IsCompactMode`

#### WindowBoundsService.cs
- [x] `_pipModeScreen` → `_compactModeScreen`
- [x] `SavePipModeScreen()` → `SaveCompactModeScreen()`
- [x] `ClearPipModeScreen()` → `ClearCompactModeScreen()`

---

## 검증 체크리스트

### 빌드 검증
- [x] 프로젝트 빌드 성공
- [x] 새로운 에러 없음 (기존 경고만 존재)

### 기능 테스트 (수동 확인 필요)
- [ ] Normal 모드 ↔ Compact 모드 전환 정상 작동
- [ ] UI 요소 숨기기 체크박스 정상 작동
- [ ] 창 크기/위치 저장/복원 정상 작동
- [ ] 핀(TopMost) 기능 정상 작동
- [ ] 단축키 기능 정상 작동

---

## 최종 목표

### 코드 품질 ✅
- 사용되지 않는 코드 0개
- 혼란스러운 용어 0개 (PIP → Compact 일관성)
- 일관성 있는 명명 규칙

### 유지보수성 ✅
- 코드 목적이 명확함
- 새로운 개발자가 이해하기 쉬움
- 향후 확장이 용이함

---

## 용어 정리

- **구 PIP 모드**: 별도의 투명 윈도우 (제거됨)
- **현재 Compact 모드**: 단일 윈도우의 작은 크기 모드
- **핀 모드**: TopMost 설정 (항상 위에 표시)
- **UI 요소 숨기기**: JavaScript로 웹 UI 패널 제거

---

## 검증일
- **2025-11-27**: Phase 1, 2 완료
- **2025-11-28**: Phase 3 완료
