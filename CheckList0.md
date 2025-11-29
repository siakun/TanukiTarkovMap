# TanukiTarkovMap MVVM 리팩토링 체크리스트

## 프로젝트 개요
- **목표**: 코드 비하인드 제거 및 순수 MVVM 패턴 구현
- **접근법**: 비즈니스 로직 → ViewModel → Service 순차 이동
- **원칙**: KISS, YAGNI, 실용주의

---

## 📊 현재 상태 (2025-11-29 업데이트)

### 리팩토링 완료율: **92%**

### 코드 비하인드 현황
| 파일 | 리팩토링 전 | 현재 | 감소율 |
|------|------------|------|--------|
| MainWindow.xaml.cs | 594줄 | 134줄 | 77% |
| SettingsPage.xaml.cs | 548줄 | 16줄 | 97% |
| **총계** | 1,142줄 | 150줄 | **87%** |

### 아키텍처 변경 사항
- ✅ WebView2 → CefSharp 마이그레이션 완료
- ✅ 탭 시스템 제거 (단일 브라우저 창 모델로 단순화)
- ✅ MVVM Community Toolkit Messenger 패턴 도입
- ✅ DI 컨테이너 (ServiceLocator) 구현

---

## ✅ 완료된 작업

### Services (7개 완료)
- [x] **HotkeyService** (101줄) - 글로벌 단축키 등록/해제
- [x] **WindowBoundsService** (159줄) - 창 위치 클램핑 및 경계 관리
- [x] **WindowStateManager** (63줄) - 창 상태 저장/복원
- [x] **Settings** (206줄) - JSON 설정 저장/로드
- [x] **BrowserUIService** (101줄) - 브라우저 UI 커스터마이징
- [x] **MapEventService** (76줄) - 맵 변경/스크린샷 이벤트
- [x] **ServiceLocator** (83줄) - DI 컨테이너

### ViewModels (3개 완료)
- [x] **MainWindowViewModel** (415줄)
  - 창 크기/위치 관리
  - 설정 프로퍼티 (HotkeyEnabled, HotkeyKey, HideWebElements)
  - 맵 선택 (SelectedMapInfo)
  - 명령어 (TogglePinMode, ChangeMap, SaveSettings, ToggleSettings)
  - Messenger 패턴 구현
- [x] **SettingsViewModel** (137줄)
  - 설정 프로퍼티 (GameFolder, ScreenshotsFolder, Hotkey 등)
  - 명령어 (Save, Cancel, BrowseFolder, ResetSettings)
  - 자동 저장 메커니즘
- [x] **WebBrowserViewModel** (370줄)
  - CefSharp 브라우저 통합
  - 이벤트 처리 (FrameLoadEnd, AddressChanged, JavascriptMessage)
  - Messenger 수신 (MapSelection, HideWebElements, ZoomLevel)

### Behaviors (6개 완료)
- [x] **HotkeyInputBehavior** - 단축키 입력 캡처 (KeyParsingService 대체)
- [x] **WindowStateBehavior** - 창 상태 변경 이벤트 전달
- [x] **TopBarAnimationBehavior** - 상단바 자동 숨김
- [x] **TrayWindowBehavior** - 트레이 최소화
- [x] **WindowControlBehavior** - 창 컨트롤 버튼
- [x] **WindowDragBehavior** - 창 드래그

### 코드 비하인드 리팩토링
- [x] SettingsPage.xaml.cs → 순수 MVVM (16줄, InitializeComponent만)
- [x] MainWindow.xaml.cs → 초기화 코드만 (134줄)
  - [x] 탭 관리 코드 제거 (아키텍처 변경으로 불필요)
  - [x] WebView2 이벤트 처리 → WebBrowserViewModel로 이동
  - [x] Window 위치 관리 → WindowBoundsService + WindowStateManager
  - [x] 설정 페이지 생성 → XAML 바인딩
  - [x] 트레이 관련 코드 → TrayWindowBehavior

---

## ❌ 불필요해진 작업 (아키텍처 변경)

### 탭 시스템 관련 (제거됨)
- ~~TabManagementService~~ → 단일 브라우저 창 모델로 변경
- ~~TabViewModel~~ → 불필요
- ~~Tab 관리 Commands~~ → 불필요

### WebView2 관련 (CefSharp으로 대체)
- ~~WebViewService~~ → WebBrowserViewModel에 통합

### Map 설정 관련 (단순화)
- ~~MapSettingViewModel~~ → 단일 맵 선택 모델로 단순화
- ~~Map 설정 UI 동적 생성~~ → 단순 맵 선택으로 변경

---

## 🔧 선택적 개선 사항 (낮은 우선순위)

### 추가 분리 가능한 항목
- [ ] **MapConfiguration 추출** (~30분)
  - 맵 이름 매핑을 별도 정적 클래스로 분리
  - 현재: 코드 내 하드코딩
  - 대상: `Models/Data/MapConfiguration.cs`

- [ ] **MainWindow 초기화 코드 ViewModel 이동** (~15분)
  - HotkeyService 초기화를 MainWindowViewModel로 이동
  - 예상 결과: 코드 비하인드 ~100줄로 감소

### 테스트 및 품질
- [ ] Service 단위 테스트 작성
- [ ] ViewModel 단위 테스트 작성

---

## 📝 기술 스택
- **프레임워크**: .NET 8.0 WPF
- **브라우저**: CefSharp
- **패턴**: 순수 MVVM (코드 비하인드 최소화)
- **라이브러리**:
  - CommunityToolkit.Mvvm (Observable, RelayCommand, Messenger)
  - Microsoft.Xaml.Behaviors.Wpf (Behavior 패턴)
- **원칙**:
  - 비즈니스 로직은 ViewModel
  - 긴 로직은 Service
  - UI 인터랙션은 Behavior
  - 인터페이스는 필요시에만 (YAGNI)
  - 코드 비하인드는 초기화만

---

## 📈 성과 요약

### 정량적 성과
- **코드 비하인드**: 1,142줄 → 150줄 (87% 감소)
- **비즈니스 로직**: 522줄 → ~20줄 (96% ViewModel/Service 이동)
- **새로 생성된 서비스**: 7개 (789줄)
- **새로 생성된 ViewModel**: 3개 (922줄)
- **Behavior 구현**: 6개

### 정성적 성과
- ✅ 순수 MVVM 패턴 준수
- ✅ 관심사 분리 (Separation of Concerns)
- ✅ 테스트 가능한 구조
- ✅ 유지보수성 향상
- ✅ 코드 재사용성 증가
