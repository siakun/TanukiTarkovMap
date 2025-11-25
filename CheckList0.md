# PIP 기능 잔여물 제거 체크리스트

## 프로젝트 배경

### 기존 PIP 기능 (Fork 프로젝트)
- **별도의 PIP 윈도우**: 투명한 소형 윈도우를 별도로 띄워 미니맵 표시
- **복잡한 구조**: PipWindow.xaml, PipController.cs (774줄), WindowTransparency 등

### 현재 프로젝트 구조
- **단일 윈도우**: 하나의 윈도우에서 Normal 모드와 Compact 모드 전환
- **핀 기능**: TopMost 설정으로 항상 위에 표시
- **UI 요소 숨기기**: JavaScript로 웹뷰의 UI 패널 숨김/표시

### 문제점
- **혼란스러운 용어**: 실제로는 "Compact Mode"인데 "PIP Mode"로 명명
- **사용되지 않는 코드**: 구 PIP 기능의 잔여물 다수 존재
- **유지보수 어려움**: 코드 목적이 불명확

---

## Phase 2: 리팩토링 (용어 및 구조 변경) - ❌ 미완료

### 5. MainWindowViewModel.cs - 사용하지 않는 `_pipService` 필드 제거
**파일**: `src/TanukiTarkovMap/ViewModels/MainWindowViewModel.cs`

**현재 상태**:
- Line 13: `private readonly PipService _pipService;` 필드 존재
- Line 116: 생성자에서 `pipService` 매개변수 주입
- Line 118: `_pipService = pipService;` 할당
- **문제점**: ViewModel 내부에서 `_pipService`를 전혀 사용하지 않음

**제거 작업**:
- [ ] `_pipService` 필드 삭제 (Line 13)
- [ ] 생성자에서 `pipService` 매개변수 제거 (Line 116)
- [ ] 기본 생성자에서 `new PipService()` 제거 (Line 114)

---

### 6. MainWindow.xaml.cs - ViewModel 생성자 수정
**파일**: `src/TanukiTarkovMap/Views/MainWindow.xaml.cs`

**현재 상태**:
- Line 37: `private PipService _pipService;` (View에서 직접 사용)
- Line 53: `_viewModel = new MainWindowViewModel(_pipService, _windowBoundsService);`
- View에서는 `_pipService`를 Line 225, 240, 258, 279, 514에서 직접 사용

**수정 작업**:
- [ ] ViewModel 생성자 호출 시 `_pipService` 전달 제거
- [ ] `_pipService`는 View 내부에서만 유지 (현재 구조 유지)

---

### 7. PipService.cs → WebViewUIService.cs 이름 변경 (선택사항)
**파일**: `src/TanukiTarkovMap/Models/Services/PipService.cs` (96줄)

**현재 역할**:
- WebView2에서 JavaScript 실행하여 UI 요소 숨김/복원
- 실제로는 "PIP 모드"가 아니라 "UI 가시성 제어" 서비스

**리팩토링 계획**:
- [ ] 클래스명 변경: `PipService` → `WebViewUIService`
- [ ] 메서드명 변경:
  - `ApplyPipModeJavaScriptAsync()` → `ApplyUIVisibilityAsync()`
  - `RestoreNormalModeJavaScriptAsync()` → `RestoreUIElementsAsync()`
- [ ] 모든 참조 위치 업데이트

---

### 8. MapInfo.cs - 잘못된 주석 수정
**파일**: `src/TanukiTarkovMap/Models/Data/MapInfo.cs`
**라인**: 26

**현재 코드**:
```csharp
/// <summary>
/// 맵 식별자 (예: "sandbox_high_preset", "factory_day_preset")
/// tarkov-market.com 내부에서 사용하는 맵 ID
/// PIP 모드에서 맵 스케일링에 사용됨  ← 잘못된 주석
/// </summary>
public string MapId { get; set; }
```

**수정 작업**:
- [ ] 주석 마지막 줄 삭제: "PIP 모드에서 맵 스케일링에 사용됨"
- [ ] 현재는 맵 스케일링을 하지 않으므로 오해의 소지

---

## Phase 3: 전면 리팩토링 (선택사항) - ❌ 미완료

### 9. WindowStateManager.cs - 변수명 변경
**파일**: `src/TanukiTarkovMap/Models/Services/WindowStateManager.cs` (145줄)

**현재 상태**:
- Line 14: `private Rect _pipModeRect;`
- Line 28: `public Rect GetPipModeRect()`
- Line 52: `public void UpdatePipModeRect(Rect rect)`

**리팩토링 계획**:
- [ ] 변수명 변경: `_pipModeRect` → `_compactModeRect`
- [ ] 메서드명 변경: `GetPipModeRect()` → `GetCompactModeRect()`
- [ ] 메서드명 변경: `UpdatePipModeRect()` → `UpdateCompactModeRect()`

---

### 10. MainWindowViewModel.cs - 속성명 변경
**파일**: `src/TanukiTarkovMap/ViewModels/MainWindowViewModel.cs`

**현재 상태**:
- Line 64: `public partial bool IsPipMode { get; set; }`
- Line 65: `public partial bool PipHotkeyEnabled { get; set; }`
- Line 66: `public partial string PipHotkeyKey { get; set; }`
- Line 67: `public partial bool PipHideWebElements { get; set; }`
- Line 62: `#region PIP Mode Properties`

**리팩토링 계획**:
- [ ] `IsPipMode` → `IsCompactMode`
- [ ] `PipHotkeyEnabled` → `HotkeyEnabled`
- [ ] `PipHotkeyKey` → `HotkeyKey`
- [ ] `PipHideWebElements` → `HideWebElements`
- [ ] Region 주석 변경: `#region PIP Mode Properties` → `#region Compact Mode Properties`

---

## 작업 요약

### Phase 1: 즉시 제거 (Quick Wins) - ✅ 완료
- [x] JavaScriptConstants.cs - CREATE_PIP_OVERLAY_SCRIPT 삭제
- [x] MapTransformCalculator.cs - 파일 삭제
- [x] PipService.cs - GetMapTransform() 메서드 삭제
- [x] DataTypes.cs - MapSetting.Transform 속성 삭제

### Phase 2: 리팩토링 (용어 정리) - ❌ 미완료
- [ ] MainWindowViewModel.cs - `_pipService` 필드 제거 (사용하지 않음)
- [ ] MainWindow.xaml.cs - ViewModel 생성 시 `_pipService` 전달 제거
- [ ] MapInfo.cs - 잘못된 주석 수정
- [ ] PipService.cs → WebViewUIService.cs 이름 변경 (선택사항)

### Phase 3: 전면 리팩토링 (선택사항) - ❌ 미완료
- [ ] WindowStateManager.cs - 변수명 변경 (_pipModeRect → _compactModeRect)
- [ ] MainWindowViewModel.cs - 속성명 변경 (IsPipMode → IsCompactMode)
- [ ] MainWindow.xaml - 바인딩 속성명 업데이트
- [ ] 모든 참조 위치 업데이트
- [ ] 전체 빌드 및 회귀 테스트

---

## 검증 체크리스트

### 빌드 검증
- [ ] 프로젝트 빌드 성공
- [ ] 새로운 에러/경고 없음

### 기능 테스트
- [ ] Normal 모드 ↔ Compact 모드 전환 정상 작동
- [ ] UI 요소 숨기기 체크박스 정상 작동
- [ ] 창 크기/위치 저장/복원 정상 작동
- [ ] 핀(TopMost) 기능 정상 작동
- [ ] 단축키 기능 정상 작동

---

## 최종 목표

### 코드 품질
- 사용되지 않는 코드 0개
- 혼란스러운 용어 0개
- 일관성 있는 명명 규칙

### 유지보수성
- 코드 목적이 명확함
- 새로운 개발자가 이해하기 쉬움
- 향후 확장이 용이함

---

## 참고 사항

### 용어 정리
- **구 PIP 모드**: 별도의 투명 윈도우 (제거됨)
- **현재 Compact 모드**: 단일 윈도우의 작은 크기 모드
- **핀 모드**: TopMost 설정 (항상 위에 표시)
- **UI 요소 숨기기**: JavaScript로 웹 UI 패널 제거

### 커밋 히스토리
- `d211047` (Nov 21) - "SettingsPage PIP 모드 제거 및 로직제거"
- `3c3472c` (Nov 20) - "PIP 모드 제거, 핀 모드로 변경"

### 검증일
- **2025-11-26**: Phase 1 완료 확인, Phase 2/3 미완료 확인
