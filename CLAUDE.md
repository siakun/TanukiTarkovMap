# TanukiTarkovMap 프로젝트 지침

## 프로젝트 정보
- **구조**: WPF MVVM 패턴
- **타겟 프레임워크**: .NET 8.0
- **주요 기술**: WPF, WebView2
- **솔루션 경로**: `src/TanukiTarkovMap.sln`

## 프로젝트 선호사항
- WPF의 순수한 MVVM 디자인 패턴으로 개발하는것을 선호
- WinForm을 사용하지 않는것을 선호

# Claude Code가 준수해야 하는 사항
- 빌드는 하더라도 실행은 하지마세요.

## MVVM 패턴 및 Code-behind 금지 원칙

**Code-behind(*.xaml.cs)에 로직을 추가하지 마세요.**

- View의 Code-behind는 `InitializeComponent()` 호출만 포함해야 함
- 이벤트 핸들러, UI 조작 로직은 **절대** Code-behind에 작성하지 않음
- UI 인터랙션이 필요한 경우 `Microsoft.Xaml.Behaviors.Wpf`의 **Behavior**를 사용
- 데이터/비즈니스 로직은 **ViewModel**에서 처리
- Command 바인딩으로 버튼 클릭 등 처리

### 올바른 패턴
```
View (XAML)
  ├── DataContext → ViewModel (데이터 바인딩)
  └── Behaviors (UI 인터랙션)
```

### 기존 Code-behind 발견 시
Code-behind에 로직이 있는 파일을 발견하면:
1. 해당 내용을 사용자에게 보고
2. Behavior 또는 ViewModel로 리팩토링 제안
3. 수정 작업 항목으로 등록

### 참고 예시
- `Behaviors/HotkeyInputBehavior.cs` - 키 입력 캡처 Behavior
- `ViewModels/SettingsViewModel.cs` - 설정 페이지 ViewModel

## 빌드 방법
```bash
cd src && dotnet build
```

## 체크리스트/문서 관리 원칙

- 완료된 작업 항목은 체크리스트에서 제거할 것
- 리스트가 제거될 때, 챕터 숫자 존재 시 오름차순으로 되도록 맞춰야 함
- 히스토리 기록보다 현재 남은 작업에 집중
- 불필요한 정보는 즉시 정리하여 문서를 간결하게 유지

# 사용자 선호사항
- 중복되는 개념은 최대한 제외하고 싶음
- 유명한 좋은 한가지의 방법이 있다면 그것을 채용하고 싶음
- 항상 신중하고 가장 올바른 1가지의 답을 원함
- 내 의견은 항상 틀릴 수 있다고 가정함. 내 의견보다 더 좋은 올바른 답이 있다면 그것이 맞다고 수용하는 편
- 항상 공부하는 자세를 가지고 있으며, 내 의견보다 다른 좋은 유용하고 심플한 프로젝트 매니징 기법이 있으면 그것을 채용하는 편
- 중복되는 개념과 파일이 분산되어 복잡해지는 것을 싫어함
- 프로젝트의 큰 틀을 수정할 때 바로 수정하는 것이 아닌, 방향이 여러가지인 경우 Claude Code와 큰 선택지에 대한 의견을 충분히 토의하여 Plan을 구축하고 프로젝트 반영해야 함
- 사용자는 현재 프로젝트의 방향성상 일반적이고 레거시 보다는 최신의 업데이트가 많은 라이브러리(Microsoft 공식 또는 업계 표준급 라이브러리)를 선호

## 사용자가 추구하는 개발 원칙

- **KISS 원칙** (Keep It Simple, Stupid) - 단순함을 최우선으로
- **YAGNI 원칙** (You Aren't Gonna Need It) - 필요하지 않은 복잡성 제거
- **실용주의** - 이론보다 실제 프로젝트에서 검증된 방식 선호

