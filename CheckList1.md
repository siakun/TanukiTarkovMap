# PIP 모드 창 위치 관리 리팩토링 체크리스트

## ✅ 완료된 작업

### Phase 0: 코드 정리
- [x] Env.cs 기능을 App.xaml.cs에 통합
- [x] 프로젝트 전체에서 Env 참조를 App으로 변경 (13개 파일)
- [x] Env.cs 파일 삭제
- [x] 빌드 검증 완료

---

### Phase 4: WindowStateManager 모듈화 (완료)

#### 4.1 WindowStateManager.cs 생성
**파일:** `Models\Services\WindowStateManager.cs`

- [x] **WindowStateManager 서비스 생성**
  - Normal 모드 Rect 저장: `_normalModeRect`
  - PIP 모드 Rect 저장: `Dictionary<string, Rect> _pipModeRects` (맵별)
  - `LoadFromSettings()` 메서드
  - `SaveToSettings()` 메서드
  - `UpdateAndSave()` 메서드
  - `GetPipModeRect()` 메서드
  - `UpdateNormalModeRect()` 메서드
  - `UpdatePipModeRect()` 메서드

#### 4.2 MainWindowViewModel.cs - WindowStateManager 통합
**파일:** `ViewModels\MainWindowViewModel.cs`

- [x] **_windowStateManager 필드 추가**
- [x] **생성자에서 WindowStateManager 초기화**
- [x] **LoadSettings()에서 WindowStateManager 사용**
- [x] **OnWindowBoundsChanged()에서 WindowStateManager.UpdateAndSave() 사용**
- [x] **OnPipModeChanged() 리팩토링** - WindowStateManager로 저장
- [x] **EnterPipMode() 리팩토링** - WindowStateManager에서 로드
- [x] **ExitPipMode() 리팩토링** - WindowStateManager에서 로드
- [x] **OnMapChanged() 리팩토링** - WindowStateManager 사용
- [x] **SaveSettings() 커맨드 리팩토링** - WindowStateManager 사용

#### 4.3 레거시 메서드 정리
**파일:** `ViewModels\MainWindowViewModel.cs`

- [x] **LoadMapSettings() 제거** (더 이상 사용되지 않음)
- [x] **SaveNormalSettings() 제거** (WindowStateManager로 대체)
- [x] **SavePipSettings() 제거** (WindowStateManager로 대체)

#### 4.4 빌드 검증
- [x] 프로젝트 빌드 성공 확인
- [x] 기존 nullable 경고만 존재 (새로운 에러 없음)

---

## 🔄 진행 예정 작업

### Phase 1: 창 위치 이벤트 기반 저장 시스템 구축

#### 1.1 MainWindow.xaml.cs - 이벤트 발생 로직 추가
**파일:** `Views\MainWindow.xaml.cs`

- [x] **WindowBoundsChanged 이벤트 정의**
  ```csharp
  // 창 위치/크기 변경 이벤트 (Rect 파라미터 사용)
  public event EventHandler<WindowBoundsChangedEventArgs>? WindowBoundsChanged;

  public class WindowBoundsChangedEventArgs : EventArgs
  {
      public Rect Bounds { get; set; }
      public bool IsPipMode { get; set; }
  }
  ```

- [x] **MainWindow_LocationChanged 메서드 수정**
  - 위치 변경 시 즉시 이벤트 발생
  - Rect 객체로 Left, Top, Width, Height 전달
  - PIP 모드 여부 함께 전달
  ```csharp
  private void MainWindow_LocationChanged(object sender, EventArgs e)
  {
      if (_isClampingLocation) return;

      // ... 기존 clamping 로직 ...

      // ✅ 이벤트 발생 (즉각 저장)
      WindowBoundsChanged?.Invoke(this, new WindowBoundsChangedEventArgs
      {
          Bounds = new Rect(this.Left, this.Top, this.Width, this.Height),
          IsPipMode = _viewModel.IsPipMode
      });
  }
  ```

- [x] **MainWindow_SizeChanged 메서드 추가/수정**
  - 크기 변경 시에도 동일하게 이벤트 발생

- [x] **MainWindow 생성자에서 이벤트 구독**
  ```csharp
  public MainWindow()
  {
      // ...

      // ViewModel에 이벤트 연결
      this.WindowBoundsChanged += _viewModel.OnWindowBoundsChanged;
  }
  ```

#### 1.2 MainWindowViewModel.cs - 타이머 제거 및 이벤트 핸들러 추가
**파일:** `ViewModels\MainWindowViewModel.cs`

- [x] **_saveTimer 관련 코드 제거**
  - Line 55: `_saveTimer` 필드 선언 삭제
  - Lines 360-379: `ScheduleSaveSettings()` 메서드 삭제
  - Lines 145-159: PropertyChanged에서 `ScheduleSaveSettings()` 호출 제거

- [x] **OnWindowBoundsChanged 이벤트 핸들러 추가**
  ```csharp
  /// <summary>
  /// View에서 창 위치/크기 변경 이벤트를 받아 즉시 저장
  /// (모듈화 고려: 나중에 별도 서비스로 분리 가능)
  /// </summary>
  public void OnWindowBoundsChanged(object? sender, WindowBoundsChangedEventArgs e)
  {
      Logger.SimpleLog($"[OnWindowBoundsChanged] Bounds={e.Bounds}, IsPipMode={e.IsPipMode}");

      // ViewModel 속성 업데이트 (PropertyChanged 발생 방지하도록 직접 설정)
      _windowLeft = e.Bounds.Left;
      _windowTop = e.Bounds.Top;
      _windowWidth = e.Bounds.Width;
      _windowHeight = e.Bounds.Height;

      // 즉시 저장 (타이머 없음)
      if (e.IsPipMode)
      {
          SavePipSettings();
      }
      else
      {
          SaveNormalSettings();
      }
  }
  ```

- [x] **PropertyChanged 핸들러 정리**
  - WindowLeft, WindowTop, WindowWidth, WindowHeight의 PropertyChanged 핸들러에서 저장 로직 제거
  - 이제 View의 이벤트로만 저장

#### 1.3 SaveNormalSettings/SavePipSettings 메서드 확인
**파일:** `ViewModels\MainWindowViewModel.cs`

- [x] **SaveNormalSettings() 검토** (Lines 326-339)
  - 현재 로직 유지
  - 즉시 저장되는지 확인

- [x] **SavePipSettings() 검토** (Lines 309-324)
  - 현재 로직 유지
  - 즉시 저장되는지 확인

---

### Phase 2: PIP 모드 진입 시 창 위치 검증

#### 2.1 MainWindow.xaml.cs - HandlePipModeChanged 메서드 수정
**파일:** `Views\MainWindow.xaml.cs` (Lines 135-168)

- [x] **PIP 모드 진입 시 EnsureWindowWithinScreen 호출**
  ```csharp
  private async Task HandlePipModeChanged()
  {
      if (_viewModel.IsPipMode)
      {
          var windowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
          _windowBoundsService.SavePipModeScreen(windowHandle);

          // ✅ 창 위치를 화면 내부로 보정
          var dpiInfo = VisualTreeHelper.GetDpi(this);
          var validatedPosition = _windowBoundsService.EnsureWindowWithinScreen(
              _viewModel.WindowLeft,
              _viewModel.WindowTop,
              _viewModel.WindowWidth,
              _viewModel.WindowHeight,
              dpiInfo.DpiScaleX,
              dpiInfo.DpiScaleY
          );

          // 검증된 위치 반영
          _viewModel.WindowLeft = validatedPosition.X;
          _viewModel.WindowTop = validatedPosition.Y;

          Logger.SimpleLog($"[PIP Entry] Position validated: {validatedPosition}");

          // ... 기존 JavaScript 적용 로직 ...
      }
      else
      {
          // ... 기존 PIP 종료 로직 ...
      }
  }
  ```

#### 2.2 OnPipModeChanged 메서드 수정
**파일:** `ViewModels\MainWindowViewModel.cs` (Lines 191-203)

- [x] **모드 전환 전 즉시 저장 로직 추가**
  ```csharp
  private void OnPipModeChanged()
  {
      Logger.SimpleLog($"PIP Mode changed to: {IsPipMode}");

      if (IsPipMode)
      {
          // 일반 모드 위치 즉시 저장 (이벤트 발생 전에 저장)
          SaveNormalSettings();
          EnterPipMode();
      }
      else
      {
          // PIP 모드 위치 즉시 저장
          SavePipSettings();
          ExitPipMode();
      }
  }
  ```

---

### Phase 3: 테스트 및 검증

#### 3.1 빌드 테스트
- [x] 프로젝트 빌드 성공 확인
- [x] 경고 메시지 확인 (기존 nullable 경고만 존재)

#### 3.2 기능 테스트

**테스트 1: 즉각 저장 테스트**
- [ ] 일반 모드에서 창 위치 변경
- [ ] **즉시** F11 누르기 (< 50ms)
- [ ] F11 다시 눌러 일반 모드 복귀
- [ ] **기대 결과:** 새로운 위치로 복귀 ✅

**테스트 2: PIP 모드 드래그 즉시 저장 테스트**
- [ ] F11로 PIP 모드 진입
- [ ] 창을 새 위치로 드래그
- [ ] **즉시** F11 누르기
- [ ] F11 다시 눌러 PIP 모드 재진입
- [ ] **기대 결과:** 드래그한 위치에 PIP 창 표시 ✅

**테스트 3: 화면 경계 검증 테스트**
- [ ] settings.json에서 Left/Top을 -1로 설정
- [ ] F11로 PIP 모드 진입
- [ ] **기대 결과:** 창이 화면 내부에 위치 ✅
- [ ] settings.json 확인: Left/Top이 유효한 값으로 저장됨

**테스트 4: 연속 드래그 테스트**
- [ ] 창을 연속으로 빠르게 드래그
- [ ] 각 위치 변경마다 즉시 저장되는지 로그 확인
- [ ] **기대 결과:** 모든 위치 변경이 즉시 저장됨 (타이머 딜레이 없음)

**테스트 5: 멀티 모니터 테스트**
- [ ] 보조 모니터에서 PIP 모드 진입
- [ ] 창이 모니터 경계 근처에 위치하도록 설정
- [ ] **기대 결과:** 창이 작업 영역 내부로 조정 ✅

**테스트 6: DPI 스케일링 테스트**
- [ ] 시스템 DPI 설정 변경 (125%, 150%)
- [ ] PIP 모드 진입 및 위치 저장
- [ ] **기대 결과:** DPI 스케일링에 맞게 올바른 위치 저장

**테스트 7: 맵별 PIP 위치 기억 테스트**
- [ ] Map A에서 PIP 모드 진입, 위치 1로 이동, F11로 종료
- [ ] Map B로 전환, PIP 모드 진입, 위치 2로 이동, F11로 종료
- [ ] Map A로 전환, PIP 모드 재진입
- [ ] **기대 결과:** Map A의 위치 1로 PIP 창 표시

---

### Phase 4: 성능 및 파일 I/O 최적화 (선택사항)

#### 4.1 파일 I/O 빈도 확인
- [ ] 로그를 통해 settings.json 저장 빈도 확인
- [ ] 창 드래그 중 과도한 파일 쓰기 발생 여부 확인

#### 4.2 디바운싱 재도입 (필요 시)
- [ ] 만약 파일 I/O가 과도하다면 (초당 10회 이상):
  - View에서 짧은 디바운싱 추가 (50-100ms)
  - 하지만 모드 전환 시에는 즉시 발생
  ```csharp
  private DispatcherTimer _boundsChangedDebouncer;

  private void MainWindow_LocationChanged(object sender, EventArgs e)
  {
      // 짧은 디바운싱 (드래그 중 과도한 이벤트 방지)
      _boundsChangedDebouncer?.Stop();
      _boundsChangedDebouncer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
      _boundsChangedDebouncer.Tick += (s, args) =>
      {
          _boundsChangedDebouncer.Stop();
          WindowBoundsChanged?.Invoke(this, new WindowBoundsChangedEventArgs { ... });
      };
      _boundsChangedDebouncer.Start();
  }
  ```

---

### Phase 5: 코드 정리 및 문서화

#### 5.1 주석 및 로그 정리
- [ ] 각 메서드에 XML 주석 추가
- [ ] 불필요한 디버그 로그 제거
- [ ] 중요한 위치에만 로그 유지

#### 5.2 코드 리뷰
- [ ] MVVM 패턴 준수 확인
- [ ] View → ViewModel 단방향 이벤트 흐름 확인
- [ ] ViewModel의 모듈화 가능성 확인

---

## 📊 예상 코드 변경 요약

### 추가
- MainWindow.xaml.cs: `WindowBoundsChangedEventArgs` 클래스
- MainWindow.xaml.cs: `WindowBoundsChanged` 이벤트
- MainWindowViewModel.cs: `OnWindowBoundsChanged` 이벤트 핸들러

### 수정
- MainWindow.xaml.cs: `MainWindow_LocationChanged` (이벤트 발생 추가)
- MainWindow.xaml.cs: `HandlePipModeChanged` (위치 검증 추가)
- MainWindowViewModel.cs: `OnPipModeChanged` (즉시 저장 추가)
- MainWindowViewModel.cs: PropertyChanged 핸들러 (저장 로직 제거)

### 삭제
- MainWindowViewModel.cs: `_saveTimer` 필드
- MainWindowViewModel.cs: `ScheduleSaveSettings` 메서드

---

## 🎯 최종 목표

1. **즉각 반응성:** 창 위치 변경 시 타이머 딜레이 없이 즉시 저장
2. **모드 전환 안정성:** F11을 빠르게 눌러도 위치 손실 없음
3. **화면 경계 준수:** PIP 모드 진입 시 항상 화면 내부에 위치
4. **MVVM 준수:** View에서 이벤트 발생, ViewModel에서 비즈니스 로직 처리
5. **모듈화 가능성:** ViewModel의 위치 관리 로직을 나중에 별도 서비스로 분리 가능

---

## 🔍 문제 분석 요약

### 문제 1: PIP 모드 진입 시 창이 화면 밖으로 나감
- **원인:** `EnsureWindowWithinScreen()` 메서드가 존재하지만 PIP 모드 진입 시 호출되지 않음
- **해결:** `HandlePipModeChanged()`에서 `EnsureWindowWithinScreen()` 호출 추가

### 문제 2: 창 위치 저장 지연 (500ms 타이머)
- **원인:** `_saveTimer`의 500ms 디바운스로 인해 모드 전환 시 위치가 저장되지 않음
- **증상:**
  - 창 이동 후 빠르게 F11을 누르면 이전 위치로 되돌아감
  - PIP 모드에서 창을 이동해도 초기 위치로 리셋됨
- **해결:** 타이머 방식을 이벤트 기반 즉각 저장으로 변경
