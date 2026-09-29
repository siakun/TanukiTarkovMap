# TanukiTarkovMap 체크리스트

## 프로젝트 개요
- **프레임워크**: .NET 8.0 WPF + CefSharp
- **패턴**: 순수 MVVM (CommunityToolkit.Mvvm, Microsoft.Xaml.Behaviors.Wpf)
- **원칙**: KISS, YAGNI, 실용주의

---

## 📋 남은 작업

### 업데이트 (다음 릴리스 태그 전 필수)

- [ ] Siakun.AutoUpdate 교체와 vpk 1.2.0 고정 후 실제 설치 검증
  - 절차와 시나리오: [업데이트 실제 설치 검증](docs/20260930-update-install-verification.md)

### 테스트 (낮은 우선순위)

- [ ] Service 단위 테스트 작성
- [ ] ViewModel 단위 테스트 작성
