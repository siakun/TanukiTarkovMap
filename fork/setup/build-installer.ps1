# TarkovClient Installer Build Script (PowerShell)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Write-Host "====================================" -ForegroundColor Green
Write-Host "TarkovClient Installer Build" -ForegroundColor Green  
Write-Host "====================================" -ForegroundColor Green

# Inno Setup 경로 찾기
$InnoSetupPaths = @(
    "C:\Users\$env:USERNAME\AppData\Local\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 5\ISCC.exe"
)

$InnoSetup = $null
foreach ($path in $InnoSetupPaths) {
    if (Test-Path $path) {
        $InnoSetup = $path
        break
    }
}

if (-not $InnoSetup) {
    Write-Host "[ERROR] Inno Setup not found" -ForegroundColor Red
    Write-Host "Checked paths:" -ForegroundColor Yellow
    foreach ($path in $InnoSetupPaths) {
        Write-Host "  - $path" -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host "Solution: winget install JRSoftware.InnoSetup" -ForegroundColor Cyan
    exit 1
}

Write-Host "[INFO] Inno Setup found at: $InnoSetup" -ForegroundColor Cyan

# publish 폴더 확인
if (-not (Test-Path "publish\TarkovClient.exe")) {
    Write-Host "[ERROR] TarkovClient.exe not found in publish folder" -ForegroundColor Red
    Write-Host "[INFO] Please run: ./build.ps1 publish first" -ForegroundColor Yellow
    exit 1
}

# Output 폴더 생성
if (-not (Test-Path "Output")) {
    Write-Host "[INFO] Creating Output folder..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path "Output" -Force | Out-Null
}

Write-Host "[INFO] Building installer..." -ForegroundColor Cyan
Write-Host ""

# Inno Setup 스크립트 컴파일
& "$InnoSetup" "setup\TarkovClient.iss"

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "[SUCCESS] Installer built successfully!" -ForegroundColor Green
    Write-Host "[INFO] Output: Output\TarkovClientSetup.exe" -ForegroundColor Cyan
    Write-Host "[INFO] Features:" -ForegroundColor Cyan
    Write-Host "  - Professional Windows installer" -ForegroundColor White
    Write-Host "  - Program Files installation" -ForegroundColor White
    Write-Host "  - Desktop shortcut creation" -ForegroundColor White
    Write-Host "  - Start menu registration" -ForegroundColor White
    Write-Host "  - Add/Remove Programs registration" -ForegroundColor White
    Write-Host "  - Complete uninstall support" -ForegroundColor White
    Write-Host ""
    Write-Host "[USAGE] Double-click TarkovClientSetup.exe to install" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Installer build failed!" -ForegroundColor Red
}