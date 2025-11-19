Write-Host "====================================" -ForegroundColor Cyan
Write-Host "TarkovClient Development Build" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan

# 개발용 빌드 설정
Write-Host "[INFO] Building Development version..." -ForegroundColor Green

# 개발용 빌드 (디버그 모드, 빠른 빌드)
$result = dotnet build --configuration Debug --verbosity minimal

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Development build failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "[SUCCESS] Development build completed successfully!" -ForegroundColor Green
Write-Host "[INFO] Output: bin\dev\net8.0-windows\TarkovClient.exe" -ForegroundColor Yellow
Write-Host "[INFO] Features: Debug symbols, Hot reload, Detailed logging" -ForegroundColor Yellow
Write-Host ""