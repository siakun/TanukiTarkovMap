# TanukiTarkovMap MVVM 리팩토링 체크리스트

## 프로젝트 개요
**목표**: WPF 순수 MVVM 패턴으로 점진적 리팩토링
**접근법**: Phase별 단계적 전환 (안정성 우선)

---

## ✅ Phase 1: PIP 기능 MVVM 전환 (완료)

### 1.1 ViewModel 구조 설계
- [x] `MainWindowViewModel` 생성
- [x] CommunityToolkit.Mvvm 활용 (`ObservableProperty`, `RelayCommand`)
- [x] PIP 모드 관련 프로퍼티 정의
  - [x] `IsPipMode`, `CurrentMap`
  - [x] `PipWidth`, `PipHeight`, `PipLeft`, `PipTop`
  - [x] Window 프로퍼티 (`WindowWidth`, `WindowHeight`, `WindowLeft`, `WindowTop`)
  - [x] UI Visibility 프로퍼티 (`TabSidebarVisibility`, `TabContainerMargin` 등)

### 1.2 Service 레이어 구축
- [x] `IPipService` 인터페이스 정의
- [x] `PipService` 구현
  - [x] View 참조 제거 (순수 비즈니스 로직)
  - [x] `ApplyPipModeJavaScriptAsync` 구현
  - [x] `RestoreNormalModeJavaScriptAsync` 구현
  - [x] `GetMapTransform` 구현

### 1.3 Commands 구현
- [x] `TogglePipModeCommand`
- [x] `ChangeMapCommand`
- [x] `SaveSettingsCommand`

### 1.4 View 레이어 정리
- [x] `MainWindow.xaml.cs` 코드비하인드 최소화
- [x] ViewModel과의 DataBinding 연결
- [x] PropertyChanged 이벤트 구독으로 UI 업데이트 처리
  - [x] `HandlePipModeChanged()`
  - [x] `HandleMapChanged()`

### 1.5 설정 저장/로드
- [x] `LoadSettings()` 구현
- [x] `SaveNormalSettings()` / `SavePipSettings()` 구현
- [x] Debounce 타이머로 설정 저장 최적화

### 1.6 이벤트 기반 통신 (NEW - 2025-01-19)
- [x] `IMapEventService` 인터페이스 설계
- [x] `MapEventService` 싱글톤 구현
- [x] `LogsWatcher`에서 `PipController` 제거 및 `MapEventService` 연결
- [x] `ScreenshotsWatcher`에서 `PipController` 제거 및 `MapEventService` 연결
- [x] `MainWindowViewModel`에서 `MapEventService` 이벤트 구독
- [x] `PipController.cs` 완전 제거 (768줄)
- [x] `PipWindow.xaml`, `PipWindow.xaml.cs`, `PipWindowViewModel.cs` 제거 (미사용 코드)
- [x] 빌드 테스트 성공

---

## ⏳ Phase 2: 나머지 기능 MVVM 전환 (진행 예정)

### 2.1 탭 시스템 MVVM 전환
- [ ] `TabViewModel` 생성
- [ ] 탭 컬렉션 관리 (`ObservableCollection<TabViewModel>`)
- [ ] Commands 구현
  - [ ] `AddNewTabCommand`
  - [ ] `CloseTabCommand`
- [ ] WebView2 관리를 Service로 분리
  - [ ] `IWebViewService` 인터페이스
  - [ ] `WebViewService` 구현

### 2.2 설정 페이지 MVVM 전환
- [x] `SettingsViewModel` 생성 (이미 존재)
- [ ] 설정 페이지 Commands 구현
  - [ ] `SaveSettingsCommand`
  - [ ] `CancelCommand`
  - [ ] `ResetCommand`
- [ ] MainWindow와의 연동 개선

### 2.3 HotkeyManager MVVM 통합
- [ ] `IHotkeyService` 인터페이스 설계
- [ ] `HotkeyService` 구현
- [ ] ViewModel에서 Hotkey 관리

---

## 🔄 Phase 3: 아키텍처 개선 (추후)

### 3.1 Dependency Injection 도입
- [ ] DI Container 선택 (Microsoft.Extensions.DependencyInjection 권장)
- [ ] Service 등록
- [ ] ViewModel 생성자 주입

### 3.2 Messenger 패턴 도입 (선택사항)
- [ ] CommunityToolkit.Mvvm.Messaging 활용
- [ ] ViewModel 간 통신 개선

### 3.3 유닛 테스트 작성
- [ ] ViewModel 테스트
- [ ] Service 테스트
- [ ] 비즈니스 로직 검증

---

## 📋 기술 부채 및 개선 사항

### 현재 상태
- ✅ PIP 기능은 MVVM 패턴 적용 완료
- ✅ PipController 제거 및 이벤트 기반 통신으로 전환 완료
- ✅ 순수 MVVM 아키텍처로 PIP 기능 구현 완료
- ⚠️ 탭 시스템은 여전히 코드비하인드에 의존
- ⚠️ WebView2 관리 로직이 View에 혼재
- ⚠️ HotkeyManager가 View에서 직접 관리됨

### 우선순위
1. **High**: 탭 시스템 MVVM 전환 (Phase 2.1)
2. **Medium**: WebView2 Service 분리 (Phase 2.1)
3. **Medium**: HotkeyManager Service 분리 (Phase 2.3)
4. **Low**: DI Container 도입 (Phase 3.1)
5. **Low**: 유닛 테스트 작성 (Phase 3.3)

---

## 🎯 다음 작업
**Phase 2.1 시작 준비**:
1. 탭 시스템 MVVM 전환 계획 수립
2. `TabViewModel` 설계
3. `IWebViewService` 인터페이스 설계

---

## 📝 참고사항
- **선호 원칙**: KISS, YAGNI, 실용주의
- **라이브러리**: CommunityToolkit.Mvvm 사용 중
- **프레임워크**: .NET 8.0 WPF
- **패턴**: 순수 MVVM (WinForm 사용 안 함)
