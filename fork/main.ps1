param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("dev", "publish", "package", "setup")]
    [string]$BuildType = ""
)

# 사용법 표시 함수
function Show-Usage {
    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host "TarkovClient Build Script" -ForegroundColor Cyan
    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage: .\build.ps1 [BuildType]" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Build Types:" -ForegroundColor White
    Write-Host "  dev       - Development build (bin/dev)" -ForegroundColor Green
    Write-Host "  publish   - Self-contained deployment (publish/)" -ForegroundColor Green
    Write-Host "  package   - GitHub Release ZIP package" -ForegroundColor Green
    Write-Host "  setup     - Windows installer package (setup/Output/)" -ForegroundColor Green
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor White
    Write-Host "  .\build.ps1 dev" -ForegroundColor Gray
    Write-Host "  .\build.ps1 publish" -ForegroundColor Gray
    Write-Host "  .\build.ps1 package" -ForegroundColor Gray
    Write-Host "  .\build.ps1 setup" -ForegroundColor Gray
    Write-Host ""
}

# 인자가 없으면 사용법 표시
if ([string]::IsNullOrEmpty($BuildType)) {
    Show-Usage
    exit 1
}

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "TarkovClient Build Script" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# 프로젝트 루트로 이동
Set-Location -Path $PSScriptRoot

# 빌드 스크립트 실행
switch ($BuildType.ToLower()) {
    "dev" {
        Write-Host "[INFO] Starting development build..." -ForegroundColor Green
        & "scripts\build-dev.ps1"
    }
    "publish" {
        Write-Host "[INFO] Starting publish build..." -ForegroundColor Green
        & "scripts\build-publish.ps1"
    }
    "package" {
        Write-Host "[INFO] Starting package creation..." -ForegroundColor Green
        & "scripts\create-package.ps1"
    }
    "setup" {
        Write-Host "[INFO] Starting installer build..." -ForegroundColor Green
        
        # publish 폴더가 없으면 먼저 publish 빌드 실행
        if (-not (Test-Path "publish\TarkovClient.exe")) {
            Write-Host "[INFO] TarkovClient.exe not found. Running publish build first..." -ForegroundColor Yellow
            Write-Host "[INFO] This may take 2-5 minutes for single-file compilation..." -ForegroundColor Cyan
            & "scripts\build-publish.ps1"
            if ($LASTEXITCODE -ne 0) {
                Write-Host "[ERROR] Publish build failed!" -ForegroundColor Red
                exit $LASTEXITCODE
            }
        }
        
        & "setup\build-installer.ps1"
    }
    default {
        Write-Host "[ERROR] Unknown build type: $BuildType" -ForegroundColor Red
        Show-Usage
        exit 1
    }
}

# 빌드 결과 확인
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "[SUCCESS] Build process completed!" -ForegroundColor Green