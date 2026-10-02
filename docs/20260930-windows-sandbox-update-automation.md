# Windows Sandbox에서 앱 업데이트를 자동으로 검증하기

<!--
INTENT
업데이트 실제 설치 검증을 사람 손 없이 돌리려다 막혔던 지점과, 결과를 무엇으로 판정하는지를 다음 검증과
다른 앱에서 재사용하도록 남긴다. 절차 자체는 업데이트 실제 설치 검증 문서가 맡고, 이 문서는 자동화할 때의
원리와 함정, 판정 근거만 다룬다. 원인을 가려내지 못한 관찰은 가설로 구분해 적는다.
-->

Velopack으로 배포하는 데스크톱 앱의 설치, 자동 업데이트, 버전 전환을 Windows Sandbox 안에서 자동으로
돌리다 막힐 때 보는 문서입니다. 핵심 원리는 세 가지입니다. 호스트에서 `wsb exec -r ExistingLogin`으로
Sandbox의 로그인 세션에 명령을 보냅니다. 결과는 공유 폴더의 파일로 돌려받습니다. 통과 여부는 앱 화면이
아니라 설치 매니페스트, 패키지 폴더, Velopack 로그로 판정합니다. Windows 11의 Windows Sandbox와 `wsb`
명령줄 도구가 있고, 앱의 창을 UI 자동화(UIA)로 조작할 수 있는 경우에 적용합니다.

사람이 따라 하는 절차와 시나리오는 [업데이트 실제 설치 검증](20260930-update-install-verification.md)에 있습니다.

## 배포와 같은 조합인지 먼저 확인

설치 폴더의 Update.exe는 앱이 참조한 Velopack 라이브러리가 아니라, 릴리스를 패키징한 vpk에서 옵니다.
delta 적용과 업데이트 적용은 Update.exe가 하므로, 검증할 조합은 "앱 안의 Velopack 버전"과 "패키징한
vpk 버전"의 짝입니다.

CI가 `dotnet tool install -g vpk`처럼 버전 없이 설치하면 릴리스마다 그 시점의 최신 vpk가 들어와 이 짝이
검증 없이 바뀝니다. 실제로 쓰인 버전은 릴리스 CI 로그에서 두 줄로 확인합니다.

- `Tool 'vpk' (version 'x.y.z') was successfully installed.`: 패키징한 vpk 버전
- `Velopack library version is lower than vpk version (...)`: 앱의 Velopack과 vpk 버전이 다를 때 vpk가 남기는 경고

버전은 저장소의 .NET 도구 매니페스트(`.config/dotnet-tools.json`)에 고정하고, CI와 로컬 스크립트는
`dotnet tool restore` 뒤 `dotnet vpk`로 실행합니다. 테스트 릴리스도 같은 매니페스트로 만들어야 검증 결과가
실제 배포를 대표합니다.

## 명령을 보내는 방법

### wsb로 시작해 로그인 세션에서 실행

```powershell
$id = (wsb start --config (Get-Content test.wsb -Raw) --raw | ConvertFrom-Json).Id
wsb connect --id $id          # 원격 세션 창을 열어 사용자 로그인을 시작한다
wsb exec --id $id -c "powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\Shared\step.ps1" -r ExistingLogin
```

`wsb exec`는 종료 코드만 돌려주므로, 스크립트는 결과를 공유 폴더(`.wsb`의 `MappedFolder`)에 파일로 저장하고
호스트가 그 파일을 읽습니다. `-r ExistingLogin`으로 띄운 프로세스가 연 창은 Sandbox 화면에 보이고 UIA로도
찾을 수 있었습니다. `wsb exec --help`의 설명대로 활성 사용자 세션이 없으면 명령이 실패하므로, 공유 폴더에
표시 파일을 만드는 짧은 명령을 반복해 세션이 준비됐는지부터 확인합니다.

`.wsb`의 `LogonCommand`로 띄운 에이전트가 공유 폴더에 놓인 명령 스크립트를 차례로 실행하는 방식도
시도했습니다. 그 에이전트가 실행한 Setup은 설치 없이 끝났고, 실행 방식이 원인인지는 가려내지 못했습니다.
이 증상의 진단은 [아래 절](#setup이-종료-코드-0으로-끝났는데-설치되지-않을-때)에 있습니다. 설치와 창 조작처럼
화면이 필요한 단계는 창이 보이는 것을 확인한 `wsb exec -r ExistingLogin`으로 보냅니다.

### 오래 걸리는 단계의 백그라운드 실행

전체 패키지를 받는 버전 전환처럼 몇 분 걸리는 단계는 `wsb exec`가 끝나기를 붙잡고 기다리지 않습니다.
세션 안에서 `Start-Process powershell.exe -WindowStyle Hidden`으로 띄우고, 스크립트가 끝에 완료 표시 파일을
만들면 호스트가 짧은 간격으로 그 파일을 확인합니다.

가상 GPU를 켠 채 몇 분짜리 `wsb exec`로 CefSharp 앱의 업데이트를 기다리던 중 Sandbox가 "가상 머신 또는
컨테이너가 예기치 않게 종료되었습니다(0x80370106)"로 끝난 적이 있습니다. `.wsb`에 `<vGPU>Disable</vGPU>`를
추가하고 긴 단계를 백그라운드 실행으로 전환한 뒤에는 다시 나타나지 않았습니다. 두 변경 중 무엇이
원인을 없앴는지는 가려내지 못했으므로, 둘 다 적용해 둡니다.

### 화면 캡처와 원격 세션 창

Sandbox 안에서 `Graphics.CopyFromScreen`으로 화면을 캡처하던 단계가 "핸들이 잘못되었습니다"로 실패한 적이
있습니다. 그때 호스트에는 `wsb connect`가 연 원격 세션 창(`WindowsSandboxRemoteSession` 프로세스)이 없었고,
`wsb connect`로 다시 연결한 뒤 같은 캡처가 성공했습니다. 캡처나 실제 마우스 입력이 필요한 단계를 보내기 전에
이 프로세스가 떠 있는지 확인하고, 없으면 다시 연결합니다. `wsb connect`는 창이 열려 있는 동안 끝나지 않으므로
호스트에서는 별도 프로세스로 띄웁니다.

다시 연결한 뒤에는 세션의 DPI 배율이 바뀔 수 있습니다(150%이던 세션이 재연결 후 100%로 바뀐 것을 확인했습니다).
좌표는 미리 저장해 두지 말고, DPI를 인식하는 프로세스(`SetProcessDPIAware`)에서 조작 직전에 UIA의
`BoundingRectangle`로 다시 읽습니다.

## Setup이 종료 코드 0으로 끝났는데 설치되지 않을 때

**증상**: 사람 없이 Setup을 실행했더니 몇 분 뒤 종료 코드 0으로 끝났지만 `%LOCALAPPDATA%\<앱 ID>`가
만들어지지 않았습니다.

**진단**: Velopack Setup에 `--log <경로>`를 주면 설치 전 확인 결과가 남습니다. 같은 Setup을 로그를 켜고
실행하면 `Package Runtime Dependencies: vcredist143-x64`와 `Visual C++ 2022 Redist (x64) is missing.`이
남고, 화면에는 사전 구성 요소를 설치할지 묻는 확인 창이 떠서 답을 기다렸습니다. 이 창에서 취소하면
`User cancelled pre-requisite installation.` 뒤에 설치 없이 끝납니다. 사람 없이 실행한 Setup이 설치 없이
끝난 것도 이 창에 답이 없었기 때문으로 보지만, 그 실행은 로그를 남기지 않아 확인하지 못했습니다. 종료
코드는 성공 여부를 알려 주지 않으므로, 설치 폴더와 Setup 로그의 `Installation completed successfully!`로
판정합니다.

**해결**: 사전 구성 요소를 먼저 조용히 설치하면 확인 창이 뜨지 않습니다.

```powershell
Start-Process vc_redist.x64.exe -ArgumentList '/install', '/quiet', '/norestart' -Wait
# 설치 확인: HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64 키의 Installed 값이 1
```

포터블 zip은 이 설치 단계가 없으므로 포터블 시나리오에서도 먼저 필요합니다. 설치 파일은 호스트에서 한 번
받아 공유 폴더에 두면 Sandbox마다 다시 받지 않습니다.

## 트레이 메뉴로 정상 종료시키기

`ShutdownMode="OnExplicitShutdown"`인 WPF 앱은 창을 닫아도 끝나지 않고 트레이 메뉴의 종료로만 끝납니다.
받아 둔 업데이트는 이 정상 종료 경로에서 적용되므로, 프로세스를 강제로 끝내면 적용 단계를 검증할 수
없습니다.

통하지 않은 방법: Hardcodet.NotifyIcon의 숨은 메시지 창(`WPFTaskbarIcon_` 클래스)에 트레이 콜백 메시지
(`WM_USER`, lParam에 `WM_RBUTTONUP`, `WM_CONTEXTMENU`, `WM_RBUTTONDOWN`)를 보내 보았지만 컨텍스트 메뉴가
열리지 않았습니다. 이유는 조사하지 않았습니다.

통한 방법은 사람과 같은 경로입니다.

1. 작업 표시줄(`Shell_TrayWnd`)에서 이름이 "숨겨진 아이콘 표시"인 단추를 누릅니다.
2. 작업 표시줄 프로세스(explorer)의 최상위 창들에서, 이름이 트레이 아이콘 툴팁과 같은 단추를 찾습니다.
3. 그 단추의 중앙을 실제 마우스 입력(`SetCursorPos`와 `mouse_event`)으로 우클릭합니다.
4. 앱 프로세스의 창에서 이름이 "종료"인 `MenuItem`을 UIA `InvokePattern`으로 누릅니다.

숨겨진 아이콘 영역이 닫혀 있으면 아이콘 단추는 UIA에서 찾히지만 `BoundingRectangle`이 비어 있어
좌표가 `NaN`이 됩니다. 좌표가 유효하지 않으면 영역부터 엽니다. 앱은 한 번에 하나만 실행되므로, 다른
설치본을 시험하기 전에 먼저 실행 중인 인스턴스를 이 방법으로 종료해야 새 인스턴스가 뜹니다.

## 결과 판정 근거

화면은 조작에만 사용하고, 통과 여부는 파일과 로그로 판정합니다.

| 확인할 것 | 근거 |
|---|---|
| 설치된 버전 | 설치 폴더 `current\sq.version`의 `<version>` |
| 업데이트를 다 받았는지 | 앱의 업데이트 준비 표시가 UIA에 나타남, `packages` 폴더에 새 버전 full 생성 |
| delta로 받았는지 | `velopack.log`의 `Downloading delta`, `Applying <개수> patches to <기준>-full.nupkg`, `Successfully applied <개수> delta patches` |
| full로 받았는지 | `velopack.log`의 `Downloading full release`, `Verifying package checksum...` |
| 적용 경로 | `Running: ...\Update.exe --silent apply --package ... --waitPid <PID>`. 종료 경로면 `--norestart`가 붙고, 즉시 적용이면 적용 뒤 앱이 새 PID로 다시 뜸 |
| 설정이 저장됐는지 | `%APPDATA%\<앱>\settings.json`의 값 |

`packages` 폴더의 full 크기로도 delta와 full을 구분할 수 있습니다. delta 적용은 기준 패키지를 풀어 패치한
뒤 다시 압축해 nupkg를 만들므로, delta로 조립한 full은 게시된 full과 크기가 다릅니다. 게시된 full을 그대로
받았으면 크기가 같습니다.

Update.exe는 업데이트를 적용할 때 설치 폴더의 `current` 폴더를 통째로 교체합니다. 앱이 로그를 실행 파일
옆(`current` 아래)에 저장하면 적용과 함께 사라지므로, 적용 전에 공유 폴더로 복사해 둡니다. 설치 폴더 루트의
`velopack.log`는 적용 뒤에도 남습니다.

## 한계

- UIA 조작은 컨트롤 이름에 기댑니다. 화면 문구("선택한 버전 설치", "종료" 등)를 변경하면 자동화도 함께 수정해야
  합니다. Sandbox의 Windows PowerShell 5.1은 BOM 없는 스크립트를 시스템 코드 페이지로 읽으므로, 한글 문자열이
  든 스크립트는 UTF-8 BOM으로 저장합니다.
- 이 방식은 사전 구성 요소를 미리 설치하므로, 실제 사용자가 처음 설치할 때 보는 확인 창의 흐름은 검증하지
  않습니다.
- 가상 GPU와 Sandbox 중단의 관계는 확인하지 못했습니다.
