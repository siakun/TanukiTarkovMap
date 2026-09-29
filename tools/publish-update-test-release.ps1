<#
.SYNOPSIS
업데이트 실제 설치 검증용 테스트 릴리스를 만들어 테스트 저장소에 올린다.

.DESCRIPTION
INTENT
운영 릴리스(.github/workflows/release.yml)와 같은 순서와 같은 vpk(.config/dotnet-tools.json)로 패키지를
만들되, 앱이 업데이트를 받을 저장소만 테스트 저장소로 바꾼다. 운영 저장소에 올리면 실제 사용자가 그 버전을
받으므로 운영 저장소 주소는 거부한다.

테스트 저장소에는 업데이트에 필요한 full, delta 패키지와 releases.win.json만 올린다. Setup.exe와
Portable.zip은 artifacts/update-test/<버전>/releases에 남겨 Windows Sandbox에 폴더로 연결해 쓴다.
설치 파일까지 올리면 버전마다 수백 MB를 더 올리지만 업데이트 경로는 그 파일을 쓰지 않는다.

검증 절차와 시나리오는 docs/20260930-update-install-verification.md에 있다.

.EXAMPLE
pwsh tools/publish-update-test-release.ps1 -Version 0.99.1
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $Repository = 'https://github.com/siakun/TanukiTarkovMap-UpdateTest'
)

$ErrorActionPreference = 'Stop'

$ProductionRepository = 'https://github.com/siakun/TanukiTarkovMap'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path $RepoRoot 'src/TanukiTarkovMap/TanukiTarkovMap.csproj'
$TestRoot = Join-Path $RepoRoot 'artifacts/update-test'
$WorkDir = Join-Path $TestRoot $Version
$PublishDir = Join-Path $WorkDir 'publish'
$ReleaseDir = Join-Path $WorkDir 'releases'
$Tag = "v$Version"

function Invoke-Checked {
    param([string] $Description, [scriptblock] $Command)
    Write-Host "== $Description"
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Description 실패 (종료 코드 $LASTEXITCODE)" }
}

# 1. 운영 저장소와 잘못된 버전, 이미 올린 태그를 막는다
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') {
    throw "버전은 SemVer 형식이어야 합니다: $Version"
}
$normalizedRepository = $Repository.TrimEnd('/') -replace '\.git$', ''
if ($normalizedRepository -ieq $ProductionRepository) {
    throw '운영 저장소에는 테스트 릴리스를 올릴 수 없습니다. 실제 사용자가 그 버전을 받습니다.'
}
if ($normalizedRepository -notmatch '^https://github\.com/([^/]+)/([^/]+)$') {
    throw "https://github.com/owner/repository 형식의 주소가 필요합니다: $Repository"
}
$OwnerRepository = "$($Matches[1])/$($Matches[2])"

$login = gh api user --jq .login
if ($LASTEXITCODE -ne 0) { throw 'gh CLI에 로그인되어 있지 않습니다.' }
if ($login -ne 'siakun') { throw "GitHub 계정이 siakun이 아닙니다: $login" }

gh release view $Tag --repo $OwnerRepository *> $null
if ($LASTEXITCODE -eq 0) {
    throw "$OwnerRepository 에 $Tag 릴리스가 이미 있습니다. 새 검증은 테스트 저장소의 릴리스를 지운 뒤 시작합니다."
}

Push-Location $RepoRoot
try {
    if (Test-Path $WorkDir) { Remove-Item -Recurse -Force $WorkDir }
    New-Item -ItemType Directory -Force -Path $PublishDir, $ReleaseDir | Out-Null

    # 2. 운영과 같은 publish에 버전과 업데이트 저장소만 바꿔 넣는다
    Invoke-Checked 'publish' {
        dotnet publish $ProjectPath -c Release -r win-x64 --self-contained true -o $PublishDir `
            "-p:Version=$Version" "-p:UpdateRepositoryUrl=$normalizedRepository"
    }

    # 3. 운영과 같은 vpk를 쓴다
    Invoke-Checked 'vpk 복원' { dotnet tool restore }

    # 4. delta 기준이 될 최신 정식 릴리스를 받는다. 첫 테스트 릴리스는 받을 것이 없어 full만 만든다
    Write-Host '== 이전 릴리스 내려받기 (delta 기준)'
    $token = gh auth token
    dotnet vpk download github --repoUrl $normalizedRepository --token $token --outputDir $ReleaseDir
    if ($LASTEXITCODE -ne 0) { Write-Host '   이전 릴리스가 없어 full 패키지만 만듭니다.' }

    # 5. 운영과 같은 인자로 패키징한다 (--framework의 이유는 release.yml의 같은 단계에 있다)
    $notesPath = Join-Path $WorkDir 'release-notes.md'
    Set-Content -Path $notesPath -Encoding utf8 -Value "## 테스트 릴리스 $Version`n`n- 업데이트 실제 설치 검증용 빌드입니다."
    Invoke-Checked 'vpk pack' {
        dotnet vpk pack --packId 'TanukiTarkovMap' --packVersion $Version --packDir $PublishDir `
            --mainExe 'TanukiTarkovMap.exe' --releaseNotes $notesPath --framework 'vcredist143-x64' `
            --outputDir $ReleaseDir
    }

    # 6. 운영과 같은 정리: 필요 없는 색인과, delta 기준으로 내려받은 이전 패키지를 뺀다
    Remove-Item -Path (Join-Path $ReleaseDir 'assets.win.json'), (Join-Path $ReleaseDir 'RELEASES') -ErrorAction SilentlyContinue
    Get-ChildItem -Path $ReleaseDir -Filter '*.nupkg' |
        Where-Object { $_.Name -notlike "*-$Version-*" } |
        Remove-Item
    Get-ChildItem -Path $ReleaseDir -Filter '*-Setup.exe' |
        Rename-Item -NewName "TanukiTarkovMap-Setup-$Version-x64.exe"
    Get-ChildItem -Path $ReleaseDir -Filter '*-Portable.zip' |
        Rename-Item -NewName "TanukiTarkovMap-Portable-$Version-x64.zip"

    # 7. 업데이트가 읽는 파일만 올린다. 프리릴리스 표시는 운영과 같이 버전의 '-'로 정한다
    $uploadFiles = @(Get-ChildItem -Path $ReleaseDir -Filter '*.nupkg') + @(Get-Item (Join-Path $ReleaseDir 'releases.win.json'))
    $releaseArguments = @('release', 'create', $Tag, '--repo', $OwnerRepository,
        '--title', "Test $Tag", '--notes', "업데이트 실제 설치 검증용 테스트 릴리스입니다. 실제로 쓰지 마세요.")
    if ($Version.Contains('-')) { $releaseArguments += '--prerelease' }
    $releaseArguments += $uploadFiles.FullName
    Invoke-Checked "GitHub 릴리스 $Tag 올리기" { gh @releaseArguments }

    # 8. Windows Sandbox에서 설치 파일과 로그를 주고받을 폴더를 연결한다 (한 번만 만든다)
    $sandboxConfig = Join-Path $TestRoot 'update-test.wsb'
    if (-not (Test-Path $sandboxConfig)) {
        $hostFolder = (Resolve-Path $TestRoot).Path
        Set-Content -Path $sandboxConfig -Encoding utf8 -Value @"
<Configuration>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$hostFolder</HostFolder>
      <SandboxFolder>C:\UpdateTest</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
</Configuration>
"@
    }

    Write-Host ''
    Write-Host "올린 릴리스: https://github.com/$OwnerRepository/releases/tag/$Tag"
    Write-Host "올린 파일: $(($uploadFiles | ForEach-Object Name) -join ', ')"
    Write-Host "설치 파일: $ReleaseDir"
    Write-Host "Sandbox 설정: $sandboxConfig"
}
finally {
    Pop-Location
}
