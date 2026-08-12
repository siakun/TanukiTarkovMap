# TanukiTarkovMap 프로젝트 지침

## 프로젝트 정보
- **구조**: WPF MVVM 패턴
- **타겟 프레임워크**: .NET 8.0
- **주요 기술**: WPF, CefSharp (CefSharp.Wpf.NETCore)
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

## 브랜치 전략: GitHub Flow

이 저장소는 GitHub Flow를 씁니다. main은 항상 배포 가능한 상태로 두고, 모든 작업은 main에서 딴 작업 브랜치에서 합니다.

전역 규칙의 develop 브랜치 운용은 이 저장소에 적용하지 않습니다. 이 절이 우선합니다. "기본 브랜치 직접 커밋 금지"는 그대로 지키되, 커밋할 곳은 develop이 아니라 그 작업의 브랜치입니다.

작업마다 main에서 브랜치를 따고, 끝나면 PR로 main에 합친 뒤 브랜치를 지웁니다. 오래 사는 브랜치를 두지 않습니다.

커밋 메시지 컨벤션, 작성자 표기, 푸시 정책은 전역 규칙을 그대로 따릅니다. push는 사용자가 직접 합니다.

## 빌드 방법
```bash
cd src && dotnet build
```

## CefSharp 렌더링 디버깅 (CDP)

Debug 빌드는 CDP(Chrome DevTools Protocol) 원격 디버깅 포트 9222를 연다
(`App.xaml.cs`의 `InitializeCef()`, `#if DEBUG` 한정. Release 빌드는 열지 않는다).
이 포트로 CefSharp가 렌더링한 페이지의 DOM, JavaScript, 스크린샷을 앱 밖에서 조회할 수 있다.
주입 스크립트(`Models/JavaScript/Scripts/*.js`)가 실제로 적용됐는지 검증할 때 쓴다.

### 절차
1. 사용자에게 Debug 빌드 앱 실행을 요청한다 (Claude는 직접 실행하지 않는다)
2. `node tools/cdp-debug.mjs targets`로 연결을 확인한다
3. 아래 명령으로 검사한다 (Node 22+ 내장 기능만 사용, 의존성 설치 불필요)

```bash
node tools/cdp-debug.mjs targets                  # 디버깅 가능한 페이지 목록
node tools/cdp-debug.mjs eval "document.title"    # 페이지 컨텍스트에서 JS 실행 (Promise는 await)
node tools/cdp-debug.mjs html ".panel_left"       # 선택자의 outerHTML 출력 (생략 시 문서 전체)
node tools/cdp-debug.mjs screenshot               # 렌더링 화면 PNG 캡처 (저장 경로 출력)
```

### 참고
- 스크린샷으로 저장된 PNG를 Read 도구로 읽으면 렌더링 결과를 시각적으로 확인할 수 있다
- F12는 사람용 DevTools 창(`ShowDevTools()`)을 연다. Claude는 읽을 수 없으므로 위 CDP 방식을 쓴다
- 포트는 localhost 전용으로만 열린다. 포트 변경 시 스크립트는 `CDP_PORT` 환경변수로 맞춘다
- 포트 9222는 앱 전용이다. 재현 실험용 별도 Chrome을 띄울 때는 반드시 다른 포트를 쓴다
  (예: `--remote-debugging-port=9223` + `CDP_PORT=9223`).
  앱이 9222를 점유한 상태에서 같은 포트로 Chrome을 띄우면 Chrome은 바인딩에 실패해도 조용히 떠 있고,
  CDP 명령이 전부 실행 중인 앱으로 흘러 들어가 사용자가 보는 화면을 조작하게 된다 (실제 사고 사례)

## 체크리스트/문서 관리 원칙

- 완료된 작업 항목은 체크리스트에서 제거할 것
- 리스트가 제거될 때, 챕터 숫자 존재 시 오름차순으로 되도록 맞춰야 함
- 히스토리 기록보다 현재 남은 작업에 집중
- 불필요한 정보는 즉시 정리하여 문서를 간결하게 유지

### PROJECT.md 동기화 (필수)

PROJECT.md는 코드와 함께 유기적으로 갱신해야 하는 살아있는 설계 문서다.

- 클래스/서비스의 추가, 개명, 삭제 같은 구조 변경 시 PROJECT.md의 다이어그램, 표, 코드 샘플을 같은 작업 안에서 함께 수정한다
- 개명/삭제 후에는 옛 이름을 저장소 전체에서 검색해 잔여 참조를 제거한다

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

# Clean Code
The assistant writes self-documenting variable names that convey full meaning without requiring context inspection. Each variable name clearly expresses its purpose in one or two words, eliminating the need to examine surrounding code.

When naming variables, the assistant chooses words that maximize semantic clarity over brevity. If a more precise word exists that better captures the variable's purpose, the assistant uses it instead of generic terms.

The assistant follows these principles:
- Include essential context in the variable name itself (use 'userEmail' not 'email', 'productPrice' not 'price')
- Limit names to two meaningful words when possible, combining them for clarity
- Select words that precisely convey the variable's role and content
- For booleans, use descriptive states that indicate the condition being tracked

Examples of meaningful naming:
- Use 'paymentComplete' not 'complete' or 'isPaymentProcessingFinished'
- Use 'stockAvailable' not 'available' or 'hasStock'
- Use 'userLoggedIn' not 'loggedIn' or 'isUserCurrentlyLoggedIn'
- Use 'configLoaded' not 'loaded' or 'hasConfigurationBeenLoaded'
- Use 'sessionExpired' not 'expired' or 'isSessionStillValid'

The assistant prioritizes semantic richness, ensuring each variable name tells its complete story independently while maintaining readability through concise, meaningful word choices.


# Class Documentation Standards
Claude must create comprehensive documentation headers for every class that enable understanding the entire implementation without reading the code. Claude follows these documentation standards to ensure consistency across sessions and prevent repeated design failures.

## Required Documentation Structure
Every class must have a documentation header using this exact format:

```csharp
...
using MyUsingNamespace;

/**
[ClassName] - [One-line core responsibility]

Purpose: [Specific problem this code solves and why it exists]
Architecture: [Overall structure and how it integrates with the system]

Core Functionality:
- [Feature name]: [Detailed behavior, when triggered, expected outcomes]
- [Feature name]: [Detailed behavior, when triggered, expected outcomes]

State Management:
- [field/property name]: [Purpose, valid values, state transitions]
- [field/property name]: [Purpose, valid values, state transitions]

Method Flow:
  [Entry point] → [Processing steps] → [State changes]
  [Branches, callbacks, event flows with clear conditions]

Key Methods:
- MethodName(params): [What it does, when called, what it returns]
- MethodName(params): [What it does, when called, what it returns]

Dependencies:
- [ClassName]: [How they interact, what data flows between them]

Design Rationale: [Why this approach over alternatives]

Historical Context: [Past attempts and why they failed - with dates/versions]
Known Limitations: [Current constraints and potential solutions to explore]

[Include these sections when relevant:]
Edge Cases: [Special situations and how they're handled]
Critical Warnings: [DO NOT instructions with specific consequences]
Technical Debt: [Priority-ranked improvements needed]
Innovation Opportunities: [Concrete suggestions for future improvements]

Last Updated: [Date] | Unity [Version] | By [Context]
*/
namespace MyNamespace
{
...
```

## Documentation Guidelines
Claude follows these principles when creating documentation:
1. **Write for complete understanding**: If someone cannot recreate the class structure from the comment alone, the documentation is incomplete.
2. **Include concrete details**: Use actual method names, field names, parameter types, and specific error messages. Avoid vague descriptions.
3. **Document both current state and history**: Explain what exists now AND what was tried before. This prevents repeating past failures.
4. **Balance guidance with innovation**: Known limitations should be presented as challenges to overcome, not permanent restrictions. Include "this might be outdated" warnings where appropriate.
5. **Focus on why over what**: Code shows what happens. Documentation explains why it happens that way and what alternatives were considered.

## Specific Requirements
If Claude encounters unusual code patterns, Claude documents why they exist. Examples:
- Multiple null checks → Document the timing issue they solve
- Seemingly redundant code → Explain what breaks when removed
- Non-standard approaches → Justify why standard patterns failed

If Claude sees mixed responsibilities in a class, Claude marks it with a refactoring TODO but also documents why the current structure exists.
When modifying existing classes, Claude first reads the documentation to understand past failures, then updates it with any new learnings.
Claude includes ASCII diagrams for complex flows but keeps them readable and maintainable.
Claude references specific Unity versions, package versions, or environmental constraints that influenced design decisions.

## Innovation and Evolution
Claude treats existing documentation as valuable context, not unchangeable law. When Claude sees opportunities for improvement:
1. Claude acknowledges the historical context
2. Claude evaluates if current technology overcomes past limitations  
3. Claude documents both the attempt and the result
4. Claude updates the "Last Updated" timestamp

If documentation says "DO NOT use async/await - causes crashes", Claude considers: Was this written for Unity 2019? Might Unity 2023 handle it better? Claude documents the reasoning before attempting changes.

## Comment Style Rules
Claude uses these comment styles consistently:
- /** */ for class-level architectural documentation (no middle asterisks for token efficiency)
- /// for public API XML documentation
- // for inline implementation notes

Claude writes documentation that enables future Claude sessions to:
- Understand the complete design without reading implementation
- Avoid repeating past failures
- Identify opportunities for improvement
- Maintain consistent behavior across sessions

## Quality Checklist

Before finalizing documentation, Claude verifies:
- Could someone implement this class using only the documentation?
- Are all state transitions and edge cases covered?
- Is the interaction with other classes crystal clear?
- Does it explain both what exists and why it exists that way?
- Are past failures and current limitations honestly documented?
- Are innovation opportunities highlighted rather than discouraged?

This comprehensive documentation serves as the source of truth for intended behavior while encouraging thoughtful evolution of the codebase.


# Code Design Philosophy
## YAGNI (You Aren't Gonna Need It) Principle
**Core Question: "Is this complexity solving a problem I have NOW, or a problem I MIGHT have?"**

Always choose the simplest solution that works today. Add complexity only when proven necessary.

## 1. **Immediate Red Flags** 🚩

Look for these patterns that indicate over-engineering:

```csharp
// 🚩 RED FLAG: Empty wrapper
public class Manager {
    private readonly Implementation impl = new();
    public void DoSomething() => impl.DoSomething(); // Just forwarding
}

// ✅ BETTER: Direct implementation
public class Manager {
    public void DoSomething() {
        // Actual logic here
    }
}
```

## 2. Decision Framework

Before creating separate classes, answer ALL of these:
| Question                        | Good Answer                      | Bad Answer                           |
|---------------------------------|----------------------------------|--------------------------------------|
| Why are these separate?         | "Different access levels needed" | "Might need it later"                |
| What does each class do?        | "Class A does X, Class B does Y" | "Class A calls Class B"              |
| Can I merge them?               | "No, because [specific reason]"  | "Yes, but separation is 'cleaner'"   |
| Is this solving a real problem? | "Yes, it fixes [current issue]"  | "It might help with future features" |

## 3. When Separation IS Justified
Only separate when you have these ACTUAL (not theoretical) needs:
- Security: Public API must hide internal implementation
- Circular Dependencies: A depends on B, B depends on A
- Multiple Implementations: You have 2+ working implementations NOW
- Team Boundaries: Different teams own different parts

## 4. The Right Approach
1. Start Simple
  - One class, one file
  - All logic in one place
2. Split When Reality Demands
  - You hit an actual limitation
  - Document WHY in code: // Split because [specific reason]
3. Measure Complexity
  - 2 simple files > 1 complex file
  - 1 simple file > 2 complex files

## 5. Real Example
```
// ❌ OVER-ENGINEERED (What we had)
// File 1: UIStateManager.cs (60 lines)
// File 2: UIState.cs (110 lines)
// Problem: Manager just forwards calls to State

// ✅ SIMPLE (What we should have)
// File 1: UIStateManager.cs (140 lines)
// All functionality in one place, no forwarding
```

# Remember
- Clean Code ≠ More Files
- Good Design = Solves TODAY'S problems
- YAGNI = Default mindset
- Complexity = Last resort

If you can't explain the separation in ONE sentence, merge it.