# TanukiTarkovMap 체크리스트

## 프로젝트 개요
- **프레임워크**: .NET 8.0 WPF + CefSharp
- **패턴**: 순수 MVVM (CommunityToolkit.Mvvm, Microsoft.Xaml.Behaviors.Wpf)
- **원칙**: KISS, YAGNI, 실용주의

---

## 📋 남은 작업

### 선택적 개선 (낮은 우선순위)

- [ ] **MapConfiguration 추출** (~30분)
  - 맵 이름 매핑을 별도 정적 클래스로 분리
  - 대상: `Models/Data/MapConfiguration.cs`

- [ ] **MainWindow 초기화 코드 ViewModel 이동** (~15분)
  - HotkeyService 초기화를 MainWindowViewModel로 이동
  - 예상 결과: 코드 비하인드 134줄 → ~100줄

### 테스트

- [ ] Service 단위 테스트 작성
- [ ] ViewModel 단위 테스트 작성
