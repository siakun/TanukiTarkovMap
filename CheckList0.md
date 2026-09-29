# TanukiTarkovMap 체크리스트

## 프로젝트 개요
- **프레임워크**: .NET 8.0 WPF + CefSharp
- **패턴**: 순수 MVVM (CommunityToolkit.Mvvm, Microsoft.Xaml.Behaviors.Wpf)
- **원칙**: KISS, YAGNI, 실용주의

---

## 📋 남은 작업

### 업데이트 (다음 릴리스 태그 전 필수)

- [ ] Siakun.AutoUpdate 교체 후 실제 설치 검증
  - 대상: 한 단계와 여러 단계 delta 자동 업데이트, 베타 수신 자동 업데이트, 다운그레이드와 상향 버전 전환, 포터블 판 업데이트
  - 검증용 릴리스는 실제 사용자가 받지 않도록 별도 저장소에 올리고, 업데이트 주소를 그 저장소로 바꾼 빌드로 확인
  - 설치 폴더와 settings.json을 실제 설치본과 공유하지 않도록 Windows Sandbox 같은 격리 환경에서 실행

### 테스트 (낮은 우선순위)

- [ ] Service 단위 테스트 작성
- [ ] ViewModel 단위 테스트 작성
