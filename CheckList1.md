# TanukiTarkovMap MVVM 리팩토링 체크리스트

## 프로젝트 개요
- **목표**: 코드 비하인드 제거 및 순수 MVVM 패턴 구현
- **접근법**: 비즈니스 로직 → ViewModel → Service 순차 이동
- **원칙**: KISS, YAGNI, 실용주의

---

## 📊 현재 상태 분석 (2025-01-20)

### 코드 비하인드 비즈니스 로직 현황
- **MainWindow.xaml.cs**: 594줄 중 **157줄 비즈니스 로직** (31%)
- **SettingsPage.xaml.cs**: 548줄 중 **365줄 비즈니스 로직** (67%)
- **총계**: 1,142줄 중 **522줄 비즈니스 로직** (48%)

### 주요 문제점
- ⚠️ 탭 관리 시스템이 코드 비하인드에 존재 (104줄)
- ⚠️ 설정 관리 로직이 코드 비하인드에 존재 (80줄)
- ⚠️ Hotkey 관리가 코드 비하인드에 존재 (198줄)
- ⚠️ WebView2 이벤트 처리가 코드 비하인드에 존재 (40줄)
- ⚠️ Map 설정 UI 동적 생성이 코드 비하인드에 존재 (140줄)
- ⚠️ Map 이름 매핑 로직이 코드 비하인드에 존재 (30줄)

---

## 🎯 Phase 1: 코드 비하인드 제거 (최우선 - 현재 진행)

### Step 1: Supporting Services 생성

#### 1.1 TabManagementService 생성
- [ ] `TabManagementService` 클래스 생성
- [ ] 메서드 구현
  - [ ] `AddTab(string url)` - 새 탭 추가
  - [ ] `RemoveTab(string tabId)` - 탭 제거
  - [ ] `GetActiveTab()` - 현재 활성 탭 조회
  - [ ] `GetAllTabs()` - 모든 탭 조회
- [ ] `_tabCounter` 관리 로직 이동
- [ ] WebView2 컬렉션 관리
- [ ] MainWindow.xaml.cs에서 104줄 제거

#### 1.2 WebViewService 생성
- [ ] `WebViewService` 클래스 생성
- [ ] 메서드 구현
  - [ ] `InitializeWebView2(object webView)` - WebView2 초기화
  - [ ] `ConfigureWebView2Settings(object webView)` - 설정 구성
  - [ ] `ExtractPageTitle(object webView)` - 페이지 타이틀 추출
  - [ ] `ProcessPageTitle(string title)` - 타이틀 가공 ("Tarkov Pilot" → "Tarkov Client")
  - [ ] `ParseWebMessage(string message)` - 메시지 파싱 ("map:" 프로토콜)
- [ ] MainWindow.xaml.cs에서 40줄 제거

#### 1.3 HotkeyService 생성
- [ ] `HotkeyService` 클래스 생성
- [ ] 메서드 구현
  - [ ] `RegisterHotkey(string key, Action callback)` - Hotkey 등록
  - [ ] `UnregisterHotkey(string key)` - Hotkey 해제
  - [ ] `ReloadHotkeys()` - 설정 재로드
  - [ ] `ValidateHotkey(string key)` - Hotkey 유효성 검사
- [ ] 기존 `HotkeyManager` 통합
- [ ] MainWindow.xaml.cs에서 48줄 제거

#### 1.4 MapConfiguration 생성 (또는 MapNameMappingService)
- [ ] `MapConfiguration` 정적 클래스 생성
- [ ] 상수 정의
  - [ ] `DisplayToInternal` Dictionary
  - [ ] `InternalToDisplay` Dictionary
  - [ ] `AllDisplayNames` Array
- [ ] SettingsPage.xaml.cs에서 30줄 제거

#### 1.5 KeyParsingService 생성
- [ ] `KeyParsingService` 클래스 생성
- [ ] 메서드 구현
  - [ ] `ParseKeyInput(Key key, ModifierKeys modifiers)` - 키 입력 파싱
  - [ ] `GetKeyString(Key key, ModifierKeys modifiers)` - 키 문자열 생성
  - [ ] `GetMainKeyString(Key key)` - 주 키 문자열 생성
  - [ ] `ValidateHotkeyKey(Key key)` - 키 유효성 검사
- [ ] SettingsPage.xaml.cs에서 150줄 제거

---

### Step 2: ViewModel 강화

#### 2.1 MainWindowViewModel 확장
- [ ] **Tab 관리 프로퍼티 추가**
  - [ ] `ObservableCollection<TabViewModel> Tabs`
  - [ ] `int SelectedTabIndex`
  - [ ] `TabViewModel CurrentTab`

- [ ] **Tab 관리 Commands 추가**
  - [ ] `AddNewTabCommand` 구현
  - [ ] `RemoveTabCommand` 구현
  - [ ] `SwitchTabCommand` 구현

- [ ] **WebView2 이벤트 처리 이동**
  - [ ] `HandleNavigationCompleted(object webView)` 메서드
  - [ ] `HandleWebMessageReceived(string message)` 메서드
  - [ ] Map 이름 추출 로직 이동

- [ ] **Hotkey 관리 추가**
  - [ ] `LoadHotkeySettings()` 메서드
  - [ ] `UpdateHotkeySettings()` 메서드
  - [ ] `ReloadHotkeysCommand` 구현

- [ ] **Window 위치 관리 통합**
  - [ ] `UpdateWindowPosition(double left, double top)` 메서드
  - [ ] Position clamping 로직 이동
  - [ ] DPI 계산 로직 이동

#### 2.2 SettingsViewModel 완전 구현
- [ ] **Map 설정 프로퍼티 추가**
  - [ ] `ObservableCollection<MapSettingViewModel> MapSettings`
  - [ ] `bool GlobalPipEnabled` (기존 `PipEnabled`)
  - [ ] UI 상태 프로퍼티들

- [ ] **Map 설정 Commands 구현**
  - [ ] `ToggleMapEnabledCommand`
  - [ ] `GlobalPipEnabledChangedCommand`

- [ ] **Hotkey Input 처리 추가**
  - [ ] `bool IsHotkeyInputMode` 프로퍼티
  - [ ] `string CurrentHotkeyInput` 프로퍼티
  - [ ] `StartHotkeyInputCommand` 구현
  - [ ] `StopHotkeyInputCommand` 구현
  - [ ] `ProcessKeyInputCommand` 구현

- [ ] **Settings 관리 메서드**
  - [ ] `LoadSettingsFromEnv()` 메서드
  - [ ] `SaveSettingsToEnv()` 메서드
  - [ ] `ValidateSettings()` 메서드

#### 2.3 TabViewModel 생성
- [ ] `TabViewModel` 클래스 생성
- [ ] 프로퍼티 정의
  - [ ] `string TabId`
  - [ ] `string TabTitle`
  - [ ] `string TabUrl`
  - [ ] `bool IsActive`
  - [ ] `object WebView` (WebView2 인스턴스)
- [ ] Commands 구현
  - [ ] `CloseCommand`
  - [ ] `ActivateCommand`

#### 2.4 MapSettingViewModel 생성
- [ ] `MapSettingViewModel` 클래스 생성
- [ ] 프로퍼티 정의
  - [ ] `string MapName` (표시 이름)
  - [ ] `string MapInternalName`
  - [ ] `bool Enabled`
  - [ ] `bool IsEditable` (PIP 활성화 여부에 따라)
  - [ ] `double Opacity` (UI 투명도)

---

### Step 3: 코드 비하인드 리팩토링

#### 3.1 MainWindow.xaml.cs 리팩토링
- [ ] **Tab 관리 코드 제거** (104줄)
  - [ ] `_tabCounter` 필드 제거
  - [ ] `_tabWebViews` 필드 제거
  - [ ] `InitializeTabs()` 제거
  - [ ] `AddNewTab()` 제거
  - [ ] `InitializeWebView2()` 제거
  - [ ] `ConfigureWebView2Settings()` 제거
  - [ ] `NewTab_Click()` 제거 → ViewModel Command 바인딩
  - [ ] `CloseTab_Click()` 제거 → ViewModel Command 바인딩

- [ ] **Hotkey 관리 코드 제거** (48줄)
  - [ ] `_hotkeyManager` 필드 제거
  - [ ] `InitializeHotkeyManager()` 제거
  - [ ] `UpdateHotkeySettings()` 제거
  - [ ] `MainWindow_PreviewKeyDown()` 제거 → ViewModel 메서드 호출로 변경

- [ ] **WebView2 이벤트 처리 제거** (40줄)
  - [ ] `WebView_NavigationCompleted()` 로직 → ViewModel로 이동
  - [ ] `CoreWebView2_WebMessageReceived()` 파싱 → ViewModel로 이동
  - [ ] 이벤트 핸들러는 ViewModel 메서드만 호출하도록 변경

- [ ] **Window 위치 관리 간소화** (48줄)
  - [ ] `MainWindow_LocationChanged()` 로직 → ViewModel로 이동
  - [ ] Clamping 로직 제거
  - [ ] 이벤트 핸들러는 ViewModel 프로퍼티만 업데이트

- [ ] **Service 인스턴스화 제거** (29줄)
  - [ ] `_windowBoundsService` 제거 → ViewModel 주입
  - [ ] `_pipService` 제거 → ViewModel 주입
  - [ ] 생성자 간소화

- [ ] **최종 코드 비하인드 목표** (50-80줄)
  - [ ] 생성자 (DataContext 설정)
  - [ ] `MainWindow_Loaded()` (ViewModel.Initialize() 호출)
  - [ ] `MainWindow_Closed()` (리소스 정리)
  - [ ] `Window_MouseLeftButtonDown()` (PIP 드래그)
  - [ ] `Settings_Click()` (설정 창 표시)

#### 3.2 SettingsPage.xaml.cs 리팩토링
- [ ] **Map 설정 UI 생성 코드 제거** (140줄)
  - [ ] `CreateMapSettingsUI()` 제거 → XAML ItemsControl 바인딩
  - [ ] `UpdateMapSettingsState()` 제거 → ViewModel 프로퍼티
  - [ ] `GlobalPipEnabled_Changed()` 제거 → ViewModel Command
  - [ ] `MapEnabled_Changed()` 제거 → ViewModel Command
  - [ ] 동적 UI 생성 → XAML DataTemplate으로 대체

- [ ] **Map 이름 매핑 제거** (30줄)
  - [ ] `_mapDisplayToInternal` 제거 → MapConfiguration 사용
  - [ ] `_mapInternalToDisplay` 제거 → MapConfiguration 사용
  - [ ] `_mapDisplayNames` 제거 → MapConfiguration 사용
  - [ ] Dictionary 초기화 코드 제거

- [ ] **Hotkey Input 처리 제거** (150줄)
  - [ ] `_isHotkeyInputMode` 제거 → ViewModel 프로퍼티
  - [ ] `PipHotkeyButton_Click()` 제거 → ViewModel Command
  - [ ] `PipHotkeyButton_LostFocus()` 제거 → ViewModel Command
  - [ ] `PipHotkeyButton_PreviewKeyDown()` 제거 → ViewModel Command
  - [ ] `PipHotkeyButton_KeyDown()` 제거 → ViewModel Command
  - [ ] `GetKeyString()` 제거 → KeyParsingService
  - [ ] `GetMainKeyString()` 제거 → KeyParsingService

- [ ] **Settings 관리 제거** (80줄)
  - [ ] `LoadSettings()` 제거 → ViewModel 메서드
  - [ ] `Save_Click()` 제거 → ViewModel Command
  - [ ] 직접 `Env` 호출 제거

- [ ] **최종 코드 비하인드 목표** (0-10줄)
  - [ ] 생성자 (InitializeComponent만)
  - [ ] 이상적으로는 **완전히 제거** 가능

---

## 🔄 Phase 2: Service 레이어 모듈화 (추후)

### 긴 로직 분리
- [ ] ViewModel에서 복잡한 로직을 Service로 추가 분리
- [ ] 각 Service 단위 테스트 작성
- [ ] Service 간 의존성 정리

---

## 📋 마이그레이션 우선순위

### 🔴 Priority 1 (즉시 시작 - Quick Wins)
1. **MapConfiguration 추출** (30분)
   - SettingsPage.xaml.cs의 Dictionary → 상수 클래스
   - 30줄 제거

2. **KeyParsingService 생성** (1시간)
   - SettingsPage.xaml.cs의 키 파싱 로직 이동
   - 150줄 제거

3. **SettingsViewModel Map 설정 바인딩** (2시간)
   - `CreateMapSettingsUI()` → XAML ItemsControl
   - 140줄 제거

4. **SettingsViewModel Save Command** (1시간)
   - `Save_Click()` → ViewModel Command
   - 80줄 제거

**Quick Wins 합계**: 약 5시간으로 **400줄 제거** (SettingsPage 거의 완료)

### 🟡 Priority 2 (다음 단계)
5. **TabManagementService 생성** (3시간)
   - Tab CRUD 로직 이동
   - 104줄 제거

6. **MainWindowViewModel Tab 관리** (3시간)
   - TabViewModel 생성
   - Commands 구현
   - 바인딩 설정

7. **WebViewService 생성** (2시간)
   - WebView2 이벤트 처리 이동
   - 40줄 제거

### 🟢 Priority 3 (마무리)
8. **HotkeyService 생성** (2시간)
   - HotkeyManager 통합
   - 48줄 제거

9. **Window 위치 관리 통합** (1시간)
   - LocationChanged 로직 이동
   - 48줄 제거

10. **코드 비하인드 최종 정리** (1시간)
    - 불필요한 코드 제거
    - 최소화 검증

---

## 📊 진행 상황 추적

### 현재 상태
- ✅ PIP 기능 MVVM 전환 완료
- ✅ 설정 페이지 ViewModel 생성 완료
- ❌ **코드 비하인드 522줄 비즈니스 로직 존재** ← **현재 작업 대상**

### 목표 상태
- 🎯 MainWindow.xaml.cs: 594줄 → 70줄 (88% 감소)
- 🎯 SettingsPage.xaml.cs: 548줄 → 10줄 (98% 감소)
- 🎯 총 비즈니스 로직: 522줄 → 0줄 (100% ViewModel/Service 이동)

---

## 📝 기술 스택
- **프레임워크**: .NET 8.0 WPF
- **패턴**: 순수 MVVM (코드 비하인드 최소화)
- **라이브러리**: CommunityToolkit.Mvvm
- **원칙**:
  - 비즈니스 로직은 ViewModel
  - 긴 로직은 Service
  - 인터페이스는 필요시에만 (YAGNI)
  - 코드 비하인드는 UI 연결만

---

## 🎯 다음 즉시 작업 (Quick Wins 시작)

1. `Models/Configuration/MapConfiguration.cs` 생성
2. `Models/Services/KeyParsingService.cs` 생성
3. `ViewModels/MapSettingViewModel.cs` 생성
4. `SettingsViewModel` 확장
5. `SettingsPage.xaml` ItemsControl 바인딩 추가
