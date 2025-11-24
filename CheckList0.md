# PIP 기능 잔여물 제거 체크리스트

## 📋 프로젝트 배경

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

## 🔴 즉시 제거 가능 (사용되지 않는 코드)

### 1. JavaScriptConstants.cs - CREATE_PIP_OVERLAY_SCRIPT
**파일**: `src/TanukiTarkovMap/Models/Constants/JavaScriptConstants.cs`
**라인**: 311-495 (약 185줄)

**현재 상태**:
```csharp
public const string CREATE_PIP_OVERLAY_SCRIPT = @"...";
```

**참조 위치**: 없음 (사용되지 않음)

**제거 작업**:
- [ ] 상수 정의 전체 삭제
- [ ] 관련 주석 삭제
- [ ] 빌드 검증

**예상 효과**: 185줄 감소

---

### 2. MapTransformCalculator.cs - 파일 전체
**파일**: `src/TanukiTarkovMap/Models/Utils/MapTransformCalculator.cs`

**현재 역할**: 맵별 Transform Matrix 계산 (PiP 모드 스케일링용)

**문제점**:
- 현재 프로젝트에서는 맵 스케일링을 사용하지 않음
- `PipService.GetMapTransform()`에서만 참조되지만, 이 메서드 자체가 호출되지 않음

**참조 검증 필요**:
- [ ] 프로젝트 전체에서 `MapTransformCalculator` 검색
- [ ] 참조가 없으면 파일 전체 삭제

**예상 효과**: 약 100-200줄 감소

---

### 3. PipService.cs - GetMapTransform() 메서드
**파일**: `src/TanukiTarkovMap/Models/Services/PipService.cs`
**라인**: 100-134 (35줄)

**현재 코드**:
```csharp
public string GetMapTransform(string mapId)
{
    // 맵별 CSS transform 반환
    // ...
}
```

**문제점**:
- 메서드가 호출되지 않음
- 현재는 창 크기만 조절하고 맵 스케일링은 하지 않음

**제거 작업**:
- [ ] 프로젝트 전체에서 `GetMapTransform` 호출 검색
- [ ] 호출이 없으면 메서드 삭제
- [ ] 관련 주석 삭제

**예상 효과**: 35줄 감소

---

### 4. DataTypes.cs - MapSetting.Transform 속성
**파일**: `src/TanukiTarkovMap/Models/Data/DataTypes.cs`
**라인**: 7

**현재 코드**:
```csharp
public class MapSetting
{
    public string Transform { get; set; } = "";  // ← 사용되지 않음
    public double Width { get; set; } = 300;
    public double Height { get; set; } = 250;
    public double Left { get; set; } = -1;
    public double Top { get; set; } = -1;
}
```

**문제점**:
- `Transform` 속성이 사용되지 않음
- `WindowStateManager`에서 "default" 키로 위치/크기만 저장

**제거 작업**:
- [ ] 프로젝트 전체에서 `Transform` 속성 사용 검색
- [ ] `GetMapTransform()`에서만 참조되면 함께 삭제
- [ ] settings.json 호환성 확인 (JSON 역직렬화 시 무시됨)

**예상 효과**: 1줄 감소 (의미상 중요)

---

## 🟡 리팩토링 권장 (용어 및 구조 변경)

### 5. PipService.cs → WebViewUIService.cs 이름 변경
**파일**: `src/TanukiTarkovMap/Models/Services/PipService.cs` (136줄)

**현재 역할**:
- WebView2에서 JavaScript 실행하여 UI 요소 숨김/복원
- 실제로는 "PIP 모드"가 아니라 "UI 가시성 제어" 서비스

**리팩토링 계획**:
- [ ] 클래스명 변경: `PipService` → `WebViewUIService`
- [ ] 메서드명 변경:
  - `ApplyPipModeJavaScriptAsync()` → `ApplyUIVisibilityAsync()`
  - `RestoreNormalModeJavaScriptAsync()` → `RestoreUIElementsAsync()`
- [ ] 주석 수정: "PIP 모드" → "UI 요소 숨기기"
- [ ] 모든 참조 위치 업데이트:
  - `MainWindow.xaml.cs:37, 49, 225, 240`
  - `MainWindowViewModel.cs:13, 114, 116-118`

**예상 효과**: 코드 목적 명확화, 유지보수성 향상

---

### 6. WindowStateManager.cs - 용어 변경
**파일**: `src/TanukiTarkovMap/Models/Services/WindowStateManager.cs` (145줄)

**문제점**:
- "PIP 모드" 용어 사용 (실제로는 "Compact 모드" 또는 "Mini 모드")
- 주석과 변수명에 혼란 야기

**리팩토링 계획**:
- [ ] 주석 수정: "PIP 모드" → "Compact 모드"
- [ ] 변수명 변경 (선택사항):
  - `_pipModeRect` → `_compactModeRect`
  - `GetPipModeRect()` → `GetCompactModeRect()`
  - `UpdatePipModeRect()` → `UpdateCompactModeRect()`
- [ ] 모든 참조 위치 업데이트 (MainWindowViewModel.cs 전체)

**예상 효과**: 코드 의도 명확화

---

### 7. MainWindowViewModel.cs - 용어 일괄 변경
**파일**: `src/TanukiTarkovMap/ViewModels/MainWindowViewModel.cs`

**문제점**:
- `IsPipMode`, `PipHideWebElements`, `PipHotkeyEnabled` 등 "Pip" 접두사
- 주석에서 "PIP 모드" 용어 반복 사용
- `_pipService` 필드를 주입받지만 **실제로 사용하지 않음**

**리팩토링 계획**:

#### 7.1 사용하지 않는 필드 제거
- [ ] `_pipService` 필드 삭제 (Line 13)
- [ ] 생성자에서 `pipService` 매개변수 제거 (Line 116)
- [ ] 기본 생성자에서 `new PipService()` 제거 (Line 114)

#### 7.2 속성명 변경 (선택사항)
- [ ] `IsPipMode` → `IsCompactMode`
- [ ] `PipHideWebElements` → `HideWebElements`
- [ ] `PipHotkeyEnabled` → `HotkeyEnabled`
- [ ] `PipHotkeyKey` → `HotkeyKey`
- [ ] Region 주석 변경: `#region PIP Mode Properties` → `#region Compact Mode Properties`

#### 7.3 주석 수정
- [ ] Line 38, 58, 110, 148, 168-177, 242, 294, 318-346 등
- [ ] "PIP 모드" → "Compact 모드"

**예상 효과**: 일관성 있는 코드베이스

---

### 8. MainWindow.xaml - UI 주석 수정
**파일**: `src/TanukiTarkovMap/Views/MainWindow.xaml`
**라인**: 147

**현재 코드**:
```xaml
<!-- PIP 모드 UI 요소 숨기기 체크박스 -->
<CheckBox x:Name="PipHideWebElementsCheckBox"
          IsChecked="{Binding PipHideWebElements, Mode=TwoWay}"
          ...
```

**리팩토링 계획**:
- [ ] 주석 수정: "PIP 모드 UI 요소 숨기기" → "UI 요소 숨기기"
- [ ] 바인딩 속성명 변경 (7.2에서 변경 시): `PipHideWebElements` → `HideWebElements`

**예상 효과**: UI와 코드 일관성

---

### 9. MainWindow.xaml.cs - 리팩토링
**파일**: `src/TanukiTarkovMap/Views/MainWindow.xaml.cs`

**문제점**:
- `_pipService` 필드를 선언하고 ViewModel에 주입하지만, ViewModel에서 사용하지 않음
- 실제로는 View에서만 직접 사용 (Line 225, 240)

**리팩토링 계획**:

#### Option A: ViewModel에서 제거, View에서만 유지
- [ ] `MainWindowViewModel` 생성자에서 `pipService` 매개변수 제거
- [ ] `MainWindow.xaml.cs:53`에서 ViewModel 생성 시 `_pipService` 전달하지 않음
- [ ] `_pipService`는 View 내부에서만 사용

#### Option B: View에서 제거, ViewModel로 이동
- [ ] `_pipService` 사용을 ViewModel 메서드로 래핑
- [ ] View에서는 ViewModel 메서드만 호출
- [ ] MVVM 패턴 강화

**권장**: Option A (현재 구조 유지, 단순화)

**예상 효과**: 불필요한 의존성 제거

---

### 10. JavaScriptConstants.cs - 주석 수정
**파일**: `src/TanukiTarkovMap/Models/Constants/JavaScriptConstants.cs`

**리팩토링 계획**:
- [ ] Line 309: "PiP 모드용 오버레이 생성" → 삭제 (1번에서 제거)
- [ ] Line 498: "PiP 모드용 컨트롤 제거" → "UI 오버레이 제거"

---

### 11. MapInfo.cs - 주석 수정
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

**리팩토링 계획**:
- [ ] 주석 마지막 줄 삭제: "PIP 모드에서 맵 스케일링에 사용됨"
- [ ] 현재는 맵 스케일링을 하지 않으므로 오해의 소지

---

## 📊 작업 우선순위

### 🔴 Phase 1: 즉시 제거 (Quick Wins)
**예상 시간**: 30분
**효과**: 약 220줄 감소, 사용되지 않는 코드 제거

1. [ ] JavaScriptConstants.cs - CREATE_PIP_OVERLAY_SCRIPT 삭제
2. [ ] MapTransformCalculator.cs - 파일 삭제 (참조 확인 후)
3. [ ] PipService.cs - GetMapTransform() 메서드 삭제
4. [ ] DataTypes.cs - MapSetting.Transform 속성 삭제
5. [ ] 빌드 검증

---

### 🟡 Phase 2: 리팩토링 (용어 정리)
**예상 시간**: 1-2시간
**효과**: 코드 명확성 대폭 향상

6. [ ] MainWindowViewModel.cs - `_pipService` 필드 제거 (사용하지 않음)
7. [ ] MainWindow.xaml.cs - ViewModel 생성 시 `_pipService` 전달 제거
8. [ ] PipService.cs → WebViewUIService.cs 이름 변경
9. [ ] 모든 주석에서 "PIP 모드" → "Compact 모드" or "UI 요소 숨기기"로 수정
10. [ ] 빌드 및 기능 테스트

---

### 🟢 Phase 3: 전면 리팩토링 (선택사항)
**예상 시간**: 2-3시간
**효과**: 완전한 일관성

11. [ ] WindowStateManager.cs - 변수명 변경 (_pipModeRect → _compactModeRect)
12. [ ] MainWindowViewModel.cs - 속성명 변경 (IsPipMode → IsCompactMode)
13. [ ] MainWindow.xaml - 바인딩 속성명 업데이트
14. [ ] 모든 참조 위치 업데이트
15. [ ] 전체 빌드 및 회귀 테스트

---

## ✅ 검증 체크리스트

### 빌드 검증
- [ ] 프로젝트 빌드 성공
- [ ] 새로운 에러/경고 없음
- [ ] 기존 경고만 존재

### 기능 테스트
- [ ] Normal 모드 ↔ Compact 모드 전환 정상 작동
- [ ] UI 요소 숨기기 체크박스 정상 작동
- [ ] 창 크기/위치 저장/복원 정상 작동
- [ ] 핀(TopMost) 기능 정상 작동
- [ ] 단축키 기능 정상 작동

### 코드 리뷰
- [ ] "PIP" 용어가 적절한 곳에만 사용됨 (또는 완전히 제거됨)
- [ ] 사용되지 않는 코드 없음
- [ ] 주석이 실제 동작과 일치함
- [ ] 변수/메서드명이 의도를 명확히 표현함

---

## 🎯 최종 목표

### 코드 품질
- ✅ 사용되지 않는 코드 0개
- ✅ 혼란스러운 용어 0개
- ✅ 일관성 있는 명명 규칙

### 유지보수성
- ✅ 코드 목적이 명확함
- ✅ 새로운 개발자가 이해하기 쉬움
- ✅ 향후 확장이 용이함

### 성능
- ✅ 불필요한 코드 로드 감소
- ✅ 빌드 크기 감소 (약 220줄)

---

## 📝 참고 사항

### 용어 정리
- **구 PIP 모드**: 별도의 투명 윈도우 (제거됨)
- **현재 Compact 모드**: 단일 윈도우의 작은 크기 모드
- **핀 모드**: TopMost 설정 (항상 위에 표시)
- **UI 요소 숨기기**: JavaScript로 웹 UI 패널 제거

### 커밋 히스토리
- `d211047` (Nov 21) - "SettingsPage PIP 모드 제거 및 로직제거"
- `3c3472c` (Nov 20) - "PIP 모드 제거, 핀 모드로 변경"

### 관련 이슈
- 사용자는 중복되는 개념을 싫어함
- 명확하고 간단한 코드 선호
- 최신 업데이트가 많은 표준 라이브러리 선호
