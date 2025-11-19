# 런타임 테스트 체크리스트

**테스트 날짜**: 2025-01-19
**목적**: Phase 1 MVVM 리팩토링 후 기능 검증

---

## 📋 테스트 환경 설정

### 준비사항
- [ ] 로그 파일 위치 확인: `C:\Users\User\AppData\Local\TarkovClient\logs\`
- [ ] 이전 로그 파일 백업 또는 삭제
- [ ] Tarkov 게임 클라이언트 준비 (맵 변경 테스트용)
- [ ] 스크린샷 폴더 확인

---

## 🧪 테스트 시나리오

### 1. 애플리케이션 시작 테스트

**목적**: 초기화 및 이벤트 구독이 올바르게 동작하는지 확인

**테스트 단계**:
1. [ ] 애플리케이션 실행
2. [ ] 로그에서 다음 메시지 확인:
   - `[MapEventService] Instance created`
   - `[MainWindowViewModel] Subscribing to MapEventService events`
   - `[MainWindowViewModel] Successfully subscribed to MapEventService events`

**예상 결과**:
- ✅ 애플리케이션이 정상적으로 시작됨
- ✅ MapEventService 인스턴스 생성 확인
- ✅ MainWindowViewModel이 이벤트에 구독됨

**실제 결과**:
```
[여기에 로그 복사]
```

**상태**: [ ] 성공 / [ ] 실패

**비고**:

---

### 2. PIP 모드 토글 테스트 (핫키)

**목적**: 핫키(F11)로 PIP 모드 전환이 올바르게 동작하는지 확인

**테스트 단계**:
1. [ ] F11 키 눌러 PIP 모드 활성화
2. [ ] 로그에서 다음 메시지 확인:
   - `[MainWindowViewModel] TogglePipMode called. Current state: False`
   - `PIP Mode changed to: True`
3. [ ] 창 크기가 작아지고 테두리가 제거되는지 확인
4. [ ] 다시 F11 키 눌러 PIP 모드 비활성화
5. [ ] 로그에서 복원 메시지 확인

**예상 결과**:
- ✅ F11 키로 PIP 모드 ON/OFF 토글됨
- ✅ 창 스타일 변경 (WindowStyle.None ↔ SingleBorderWindow)
- ✅ 창 크기 변경 (PIP 크기 ↔ Normal 크기)
- ✅ Topmost 설정 변경

**실제 결과**:
```
[여기에 로그 및 동작 설명 복사]
```

**상태**: [ ] 성공 / [ ] 실패

**비고**:

---

### 3. 맵 변경 이벤트 테스트

**목적**: Tarkov 게임에서 맵이 변경될 때 이벤트가 전파되는지 확인

**테스트 단계**:
1. [ ] Tarkov 게임 실행
2. [ ] 레이드 시작 (맵 선택)
3. [ ] 로그에서 다음 메시지 순서 확인:
   - `[MapEventService] OnMapChanged called: [맵이름]`
   - `[MapEventService] MapChanged event has 1 subscriber(s)`
   - `[MapEventService] MapChanged event invoked for map: [맵이름]`
   - `[MainWindowViewModel] MapEvent received: [맵이름]`
   - `[MainWindowViewModel] ChangeMapCommand executed for: [맵이름]`

**예상 결과**:
- ✅ LogsWatcher가 맵 변경 감지
- ✅ MapEventService로 이벤트 전파
- ✅ MainWindowViewModel이 이벤트 수신
- ✅ CurrentMap 프로퍼티 업데이트

**실제 결과**:
```
[여기에 로그 복사]
```

**상태**: [ ] 성공 / [ ] 실패

**비고**:

---

### 4. 맵 변경 + PIP 자동 활성화 테스트

**목적**: 맵 변경 시 PIP 설정에 따라 자동 활성화되는지 확인

**전제조건**: 설정에서 `PipEnabled = true`

**테스트 단계**:
1. [ ] PIP 모드 OFF 상태 확인
2. [ ] Tarkov에서 맵 변경
3. [ ] 로그에서 다음 메시지 확인:
   - `[MainWindowViewModel] MapEvent received: [맵이름]`
   - `[MainWindowViewModel] Current IsPipMode: False` (맵 변경 전)
   - PIP 모드 활성화 로그
4. [ ] 자동으로 PIP 모드로 전환되는지 확인

**예상 결과**:
- ✅ 맵 변경 감지
- ✅ PIP 모드 자동 활성화
- ✅ JavaScript가 WebView2에 적용됨

**실제 결과**:
```
[여기에 로그 복사]
```

**상태**: [ ] 성공 / [ ] 실패

**비고**:

---

### 5. 스크린샷 이벤트 테스트

**목적**: 스크린샷 생성 시 PIP 모드가 활성화되는지 확인

**전제조건**: 설정에서 `PipEnabled = true`

**테스트 단계**:
1. [ ] PIP 모드 OFF 상태 확인
2. [ ] Tarkov에서 스크린샷 촬영 (Print Screen)
3. [ ] 로그에서 다음 메시지 확인:
   - `[MapEventService] OnScreenshotTaken called`
   - `[MapEventService] ScreenshotTaken event has 1 subscriber(s)`
   - `[MapEventService] ScreenshotTaken event invoked`
   - `[MainWindowViewModel] Screenshot event received`
   - `[MainWindowViewModel] Executing TogglePipModeCommand (PIP mode OFF -> ON)`
4. [ ] PIP 모드로 전환되는지 확인

**예상 결과**:
- ✅ ScreenshotsWatcher가 스크린샷 감지
- ✅ MapEventService로 이벤트 전파
- ✅ MainWindowViewModel이 이벤트 수신
- ✅ PIP 모드 자동 활성화

**실제 결과**:
```
[여기에 로그 복사]
```

**상태**: [ ] 성공 / [ ] 실패

**비고**:

---

### 6. PipService JavaScript 적용 테스트

**목적**: PIP 모드에서 WebView2에 JavaScript가 올바르게 적용되는지 확인

**테스트 단계**:
1. [ ] PIP 모드 활성화
2. [ ] 로그에서 다음 메시지 순서 확인:
   - `[PipService] ApplyPipModeJavaScriptAsync called for map: [맵이름]`
   - `[PipService] Step 1: Removing existing PIP overlay`
   - `[PipService] Step 2: Applying map scaling`
   - `[PipService] Transform matrix: [행렬값]`
   - `[PipService] Step 3: Removing UI elements`
   - `[PipService] Successfully applied PIP mode JavaScript for map: [맵이름]`
3. [ ] WebView2에서 UI 요소가 제거되는지 확인
4. [ ] 맵이 스케일링되는지 확인

**예상 결과**:
- ✅ JavaScript 실행 성공
- ✅ UI 요소 제거 (header, footer, panels)
- ✅ 맵 스케일링 적용

**실제 결과**:
```
[여기에 로그 및 스크린샷 첨부]
```

**상태**: [ ] 성공 / [ ] 실패

**비고**:

---

### 7. PIP 모드 해제 시 복원 테스트

**목적**: PIP 모드 해제 시 원래 상태로 복원되는지 확인

**테스트 단계**:
1. [ ] PIP 모드 활성화된 상태
2. [ ] F11 키로 PIP 모드 해제
3. [ ] 로그에서 다음 메시지 확인:
   - `[PipService] RestoreNormalModeJavaScriptAsync called`
   - `[PipService] Restoring removed UI elements`
   - `[PipService] Successfully restored normal mode JavaScript`
4. [ ] 창 크기 및 스타일이 복원되는지 확인
5. [ ] WebView2의 UI 요소가 다시 표시되는지 확인

**예상 결과**:
- ✅ 창 크기/위치 복원
- ✅ WindowStyle 복원
- ✅ UI 요소 복원

**실제 결과**:
```
[여기에 로그 복사]
```

**상태**: [ ] 성공 / [ ] 실패

**비고**:

---

### 8. 설정 저장/로드 테스트

**목적**: PIP 모드에서 창 크기/위치가 저장되고 복원되는지 확인

**테스트 단계**:
1. [ ] PIP 모드 활성화
2. [ ] 창 크기 조절
3. [ ] 창 위치 이동
4. [ ] 로그에서 저장 메시지 확인 (Debounce 후):
   - `Saved PIP settings for [맵이름]: [너비]x[높이] at ([left], [top])`
5. [ ] 애플리케이션 종료
6. [ ] 애플리케이션 재시작
7. [ ] 동일한 맵에서 PIP 모드 활성화
8. [ ] 저장된 크기/위치로 복원되는지 확인

**예상 결과**:
- ✅ 설정 자동 저장 (500ms Debounce)
- ✅ 맵별 설정 저장
- ✅ 재시작 후 설정 복원

**실제 결과**:
```
[여기에 로그 및 설정 파일 경로 복사]
```

**상태**: [ ] 성공 / [ ] 실패

**비고**:

---

## 🔍 추가 검증 항목

### 에러 처리
- [ ] WebView2가 초기화되지 않은 상태에서 PIP 모드 전환 시 에러 없이 처리되는지
- [ ] 잘못된 맵 이름이 전달될 때 에러 없이 처리되는지
- [ ] JavaScript 실행 실패 시 로그에 에러 메시지가 기록되는지

### 메모리 및 성능
- [ ] 이벤트 구독 해제가 올바르게 되는지 (메모리 누수 방지)
- [ ] 애플리케이션 종료 시 리소스 정리가 올바른지
- [ ] 반복적인 PIP 모드 토글 시 성능 저하가 없는지

---

## 📊 테스트 요약

**전체 테스트**: 8개
**성공**: ___ 개
**실패**: ___ 개

### 발견된 문제점

1.
2.
3.

### 권장 사항

1.
2.
3.

---

## ✅ 최종 승인

**테스트 담당자**: _________________
**승인 날짜**: _________________

**Phase 2 진행 가능 여부**: [ ] Yes / [ ] No

**사유**:
