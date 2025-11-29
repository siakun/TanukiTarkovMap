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