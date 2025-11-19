Write-Host "====================================" -ForegroundColor Cyan
Write-Host "TarkovClient Package Creator" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan

# 버전 정보 추출 (환경변수 또는 csproj에서)
if ($env:GITHUB_REF -and $env:GITHUB_REF -match "refs/tags/v(.+)") {
    $version = "v" + $matches[1]
    Write-Host "[INFO] Using version from Git tag: $version" -ForegroundColor Cyan
}
else {
    # .csproj 파일에서 버전 추출
    $csprojContent = Get-Content "TarkovClient.csproj" -Raw
    if ($csprojContent -match '<Version>([\d\.]+)</Version>') {
        $version = "v" + $matches[1]
        Write-Host "[INFO] Using version from csproj: $version" -ForegroundColor Cyan
    }
    else {
        $version = "v0.1.1"
        Write-Host "[INFO] Using default version: $version" -ForegroundColor Yellow
    }
}

$zipName = "TarkovClient-$version.zip"
$tempDir = "temp-package"
$packageDir = "Tarkov Client $version"

Write-Host "[INFO] Creating package for $version..." -ForegroundColor Green

# 필수 파일 존재 확인
if (-not (Test-Path "publish\TarkovClient.exe")) {
    Write-Host "[ERROR] TarkovClient.exe not found!" -ForegroundColor Red
    Write-Host "[INFO] Please run 'scripts\build-publish.ps1' first to build the executable." -ForegroundColor Yellow
    exit 1
}

# 모든 txt 파일 찾기
$txtFiles = Get-ChildItem -Path "." -Filter "*.txt"
if (-not $txtFiles) {
    Write-Host "[WARNING] No txt files found in root directory." -ForegroundColor Yellow
}
Write-Host "[INFO] Found $($txtFiles.Count) txt file(s)" -ForegroundColor Green

# 임시 패키지 폴더 생성
if (Test-Path $tempDir) {
    Write-Host "[INFO] Cleaning temporary package folder..." -ForegroundColor Yellow
    Remove-Item -Path $tempDir -Recurse -Force
}
New-Item -Path $tempDir -ItemType Directory | Out-Null
New-Item -Path "$tempDir\$packageDir" -ItemType Directory | Out-Null

# TarkovClient.exe 복사
Write-Host "[INFO] Adding TarkovClient.exe..." -ForegroundColor Green
Copy-Item -Path "publish\TarkovClient.exe" -Destination "$tempDir\$packageDir\TarkovClient.exe"

# 모든 txt 파일 복사
foreach ($txtFile in $txtFiles) {
    Write-Host "[INFO] Adding $($txtFile.Name)..." -ForegroundColor Green
    Copy-Item -Path $txtFile.FullName -Destination "$tempDir\$packageDir\$($txtFile.Name)"
}

# ZIP 파일 생성
Write-Host "[INFO] Creating ZIP package..." -ForegroundColor Green
Compress-Archive -Path "$tempDir\*" -DestinationPath $zipName -Force

# 파일 크기 확인
$zipInfo = Get-Item $zipName
$zipSizeMB = [math]::Round($zipInfo.Length / 1MB, 2)

Write-Host "[SUCCESS] Package created successfully!" -ForegroundColor Green
Write-Host "[INFO] ZIP File: $zipName ($zipSizeMB MB)" -ForegroundColor Yellow
Write-Host "[INFO] Contents:" -ForegroundColor Yellow
Get-ChildItem "$tempDir\$packageDir" | ForEach-Object {
    $fileSizeMB = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  - $($_.Name) ($fileSizeMB MB)" -ForegroundColor Gray
}

# 임시 폴더 정리
Remove-Item -Path $tempDir -Recurse -Force

Write-Host ""
Write-Host "[INFO] Package ready for GitHub Release upload!" -ForegroundColor Green
Write-Host ""