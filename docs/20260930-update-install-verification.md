# 업데이트 실제 설치 검증

<!--
INTENT
업데이트 경로는 배포하는 순간 굳는다. 업데이트 코드가 깨진 채로 나가면 그 버전을 받은 사용자는
다음 버전으로 넘어갈 수 없고, 고친 버전을 올려도 닿지 않는다. 그래서 업데이트에 닿는 변경은
릴리스 전에 실제로 설치한 앱에서 업데이트가 도는지 확인한다. Siakun.AutoUpdate의 단위 테스트는
Velopack의 다운로드, 패치, 적용을 흉내만 내므로 이 검증을 대신하지 못한다.
-->

실제로 설치한 앱에서 자동 업데이트, 버전 전환, 포터블 판 업데이트가 도는지 확인하는 절차입니다.
테스트 빌드는 운영 저장소가 아닌 테스트 저장소에서 업데이트를 받고, 앱은 Windows Sandbox에서 실행합니다.

## 언제 하는가

다음 중 하나를 바꾼 뒤 첫 릴리스 태그를 올리기 전에 합니다.

- `Siakun.AutoUpdate` 패키지 버전
- `.config/dotnet-tools.json`의 vpk 버전 (패키지에 들어가는 Update.exe가 함께 바뀝니다)
- `UpdateServiceFactory`의 설정 연결이나 `App`의 종료 경로

옛 버전에서 새 버전으로 넘어가는 첫 업데이트는 옛 버전에 든 업데이트 코드가 수행합니다.
vpk 버전을 바꾸지 않았다면 이 경로는 이전 릴리스 사이의 업데이트와 같습니다. vpk 버전을 바꿨다면
운영 저장소의 최신 버전을 설치한 뒤 새 버전으로 넘어가는 경로도 따로 확인합니다.

## 준비

- 테스트 저장소 [siakun/TanukiTarkovMap-UpdateTest](https://github.com/siakun/TanukiTarkovMap-UpdateTest)
  - 공개 저장소여야 합니다. 앱은 인증 없이 GitHub Releases를 읽습니다.
- Windows Sandbox
  - "Windows 기능 켜기/끄기"에서 켭니다.
  - 테스트 빌드는 운영과 같은 앱 ID로 설치되므로, 이 PC에서 직접 설치하면 실제 설치본과
    `%APPDATA%\TanukiTarkovMap\settings.json`을 덮어씁니다. 다운그레이드 시나리오는 자동 업데이트를
    끄므로 실제 앱의 업데이트까지 멈춥니다.
- `gh` CLI 로그인 (계정 `siakun`)

## 테스트 릴리스 만들기

테스트 빌드는 `-p:UpdateRepositoryUrl`로 업데이트 저장소만 바꾼 운영 빌드입니다. 값을 주지 않은
빌드(CI 릴리스, `build.bat`)는 운영 저장소를 봅니다.

```powershell
pwsh tools/publish-update-test-release.ps1 -Version 0.99.1
pwsh tools/publish-update-test-release.ps1 -Version 0.99.2
pwsh tools/publish-update-test-release.ps1 -Version 0.99.3
pwsh tools/publish-update-test-release.ps1 -Version 0.99.4-beta.1
```

반드시 이 순서로 올립니다. 스크립트는 올리기 직전 테스트 저장소의 최신 정식 버전을 받아 delta를
만들므로, 운영처럼 각 delta가 바로 앞 정식 버전을 기준으로 만들어집니다. 베타의 delta는 운영과 같이
그 시점의 최신 정식 버전을 기준으로 만들어집니다.

테스트 저장소에는 업데이트가 읽는 full, delta 패키지와 `releases.win.json`만 올라갑니다. Setup.exe와
Portable.zip은 `artifacts/update-test/<버전>/releases`에 남습니다.

새로 검증을 시작할 때는 테스트 저장소의 릴리스와 태그를 모두 지우고 같은 버전부터 다시 올립니다.

```powershell
gh release list --repo siakun/TanukiTarkovMap-UpdateTest --json tagName --jq '.[].tagName' |
    ForEach-Object { gh release delete $_ --repo siakun/TanukiTarkovMap-UpdateTest --cleanup-tag --yes }
```

## Sandbox에서 실행하기

스크립트가 만든 `artifacts/update-test/update-test.wsb`를 열면 Sandbox가 뜨고, Sandbox 안의
`C:\UpdateTest`가 `artifacts/update-test`와 연결됩니다. Sandbox는 닫으면 초기화되므로 시나리오마다 새로 엽니다.

사람 대신 스크립트로 조작할 때의 명령 전달 방법과 막히는 지점, 결과 판정 근거는
[Windows Sandbox에서 앱 업데이트를 자동으로 검증하기](20260930-windows-sandbox-update-automation.md)에 있습니다.

- 설치: `C:\UpdateTest\<버전>\releases\TanukiTarkovMap-Setup-<버전>-x64.exe`를 실행합니다. VC++ 재배포
  패키지가 없으면 Setup이 "추가 구성 요소가 필요합니다" 확인 창을 띄우고 답을 기다리며, 확인을 누르면
  내려받아 설치하므로 Sandbox의 인터넷 연결이 필요합니다. 아무도 답하지 않으면 Setup은 설치하지 않은 채
  끝납니다. 창을 거치지 않으려면
  [VC++ 재배포 패키지](https://aka.ms/vs/17/release/vc_redist.x64.exe)를 `/install /quiet /norestart`로
  먼저 설치합니다.
- 포터블: Portable.zip에는 VC++ 재배포 패키지 설치 단계가 없으므로 위와 같이 먼저 설치한 뒤 실행합니다.
  같은 Sandbox에서 설치본에 이어 포터블을 시험하면 두 앱이 `settings.json`을 함께 씁니다. 한 번에 하나만
  실행되므로 설치본을 트레이에서 종료하고, 앞 시나리오가 바꾼 자동 업데이트와 베타 설정을 되돌린 뒤
  포터블을 실행합니다.
- 앱 종료: 트레이 아이콘의 "종료"로 끝냅니다. 이 정상 종료 경로에서 받아 둔 업데이트가 적용됩니다.
- 버전 확인: 설정 화면의 앱 버전과 버전 선택 목록의 "현재" 표시로 봅니다.
- 로그: 앱 로그는 설치 폴더의 `current\Logs`에 쌓이는데, 업데이트를 적용하면 `current`가 통째로 바뀌어
  사라집니다. 업데이트를 적용하기 전에 `%LOCALAPPDATA%\TanukiTarkovMap\current\Logs`를
  `C:\UpdateTest\logs`로 복사해 둡니다. delta와 full 중 무엇을 받았는지는
  `%LOCALAPPDATA%\TanukiTarkovMap\velopack.log`에 남습니다. 포터블은 푼 폴더의 `current\Logs`와
  `velopack.log`입니다.

## 시나리오

자동 업데이트는 앱을 시작한 뒤 백그라운드에서 받고, 다 받으면 상단바에 업데이트 표시가 나타납니다.

| 시나리오 | 시작 | 조작 | 확인할 것 |
|---|---|---|---|
| 여러 단계 delta 자동 업데이트 | 0.99.1 설치 | 업데이트 표시가 뜨면 트레이에서 종료, 다시 실행 | 0.99.3으로 바뀜. `velopack.log`에 delta 두 개 적용 |
| 한 단계 delta와 즉시 적용 | 0.99.2 설치 | 업데이트 표시를 눌러 즉시 재시작 | 앱이 스스로 다시 떠 0.99.3. `velopack.log`에 delta 한 개 적용 |
| 베타 자동 업데이트 | 0.99.3 설치 | 실험적 기능에서 베타 버전 받기를 켜고 앱을 다시 실행 | 업데이트 표시 후 종료, 다시 실행하면 0.99.4-beta.1 |
| 다운그레이드 | 0.99.3 설치 | 버전 선택에서 0.99.1을 골라 설치 | 진행률 표시 후 앱이 스스로 다시 떠 0.99.1. 자동 업데이트가 꺼져 있고, 다시 실행해도 올라가지 않음 |
| 상향 버전 전환 | 0.99.1 설치 | 버전 선택에서 0.99.2를 골라 설치 | 앱이 스스로 다시 떠 0.99.2. 최신이 아니므로 자동 업데이트가 꺼짐 |
| 포터블 자동 업데이트 | 0.99.1 Portable.zip을 `C:\Portable`에 풀고 실행 | 업데이트 표시가 뜨면 트레이에서 종료, 다시 실행 | 0.99.3으로 바뀜. 포터블에는 기준 패키지가 없어 첫 업데이트는 full |
| 포터블 버전 전환 | 위에서 0.99.3이 된 포터블 | 버전 선택에서 0.99.2를 골라 설치 | 앱이 스스로 다시 떠 0.99.2 |

delta로 받았는지는 `velopack.log`의 `Applying <개수> patches` 줄로, full로 받았는지는
`Downloading full release` 줄로 구분합니다. `packages` 폴더의 full 크기로도 알 수 있습니다. delta로 조립한
full은 게시된 full과 크기가 다르고, 그대로 받은 full은 같습니다.

실패하면 그 시점의 앱 로그와 `velopack.log`를 `C:\UpdateTest\logs`에 모아 둡니다. 로그의
`[UpdateService]`, `[GitHubReleaseCatalog]` 줄이 라이브러리가 남긴 기록입니다.
