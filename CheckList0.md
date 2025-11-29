# 창 위치 관리 리팩토링 체크리스트

## ✅ 완료된 작업

### 1. 코드 정리
- [x] Env.cs 기능을 App.xaml.cs에 통합
- [x] Env.cs 파일 삭제

### 2. WindowStateManager 모듈화
- [x] `Models\Services\WindowStateManager.cs` 생성
- [x] Normal 모드 Rect 관리 (`NormalModeRect`)
- [x] `LoadFromSettings()`, `SaveToSettings()`, `UpdateNormalModeRect()` 구현

### 3. 이벤트 기반 저장 시스템
- [x] `WindowBoundsChangedEventArgs` 클래스 정의
- [x] `WindowStateBehavior` 구현 (LocationChanged, SizeChanged 처리)
- [x] `_saveTimer` 및 `ScheduleSaveSettings()` 제거
- [x] `OnWindowBoundsChanged()` 이벤트 핸들러로 즉시 저장

### 4. MainWindowViewModel 통합
- [x] `_windowStateManager` DI 주입
- [x] `LoadSettings()`에서 WindowStateManager 사용
- [x] `SaveSettings()` 커맨드에서 WindowStateManager 사용

### 5. MVVM 패턴 준수
- [x] View의 Code-behind에서 로직 제거
- [x] `WindowStateBehavior`로 창 상태 이벤트 처리
- [x] ViewModel에서 비즈니스 로직 처리

---

## 📊 구현 결과

| 항목 | 구현 파일 |
|------|----------|
| 창 상태 관리 | `Models\Services\WindowStateManager.cs` |
| 창 이벤트 처리 | `Behaviors\WindowStateBehavior.cs` |
| 앱 전역 상태 | `App.xaml.cs` |
| ViewModel | `ViewModels\MainWindowViewModel.cs` |

---

## 🎯 달성된 목표

1. **즉각 반응성**: 창 위치 변경 시 즉시 저장 (타이머 제거)
2. **MVVM 준수**: View → Behavior → ViewModel 흐름
3. **모듈화**: WindowStateManager로 창 상태 관리 분리
4. **코드 정리**: Env.cs 제거, App.xaml.cs 통합
