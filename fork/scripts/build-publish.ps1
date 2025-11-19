Write-Host "====================================" -ForegroundColor Cyan
Write-Host "TarkovClient Publish Build" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan

# Build for deployment
Write-Host "[INFO] Creating publish package..." -ForegroundColor Green

# Clean previous publish folder
if (Test-Path "publish") {
    Write-Host "[INFO] Cleaning previous publish folder..." -ForegroundColor Yellow
    Remove-Item -Path "publish" -Recurse -Force
}

# Publish Self-Contained single file
Write-Host "[INFO] This may take several minutes due to single-file compilation..." -ForegroundColor Yellow
Write-Host "[INFO] Building self-contained executable (no .NET Runtime required)..." -ForegroundColor Green

$result = dotnet publish --configuration Release --output publish --self-contained true --runtime win-x64 --verbosity minimal

# Check if build was successful by checking if exe exists
if (-not (Test-Path "publish\TarkovClient.exe")) {
    Write-Host "[ERROR] Publish failed! TarkovClient.exe was not created." -ForegroundColor Red
    exit 1
}

# Remove unnecessary files for deployment
if (Test-Path "publish\*.pdb") {
    Write-Host "[INFO] Removing debug files (.pdb)..." -ForegroundColor Yellow
    Remove-Item -Path "publish\*.pdb" -Force
}
if (Test-Path "publish\*.xml") {
    Write-Host "[INFO] Removing documentation files (.xml)..." -ForegroundColor Yellow
    Remove-Item -Path "publish\*.xml" -Force
}

# Display file size
$fileInfo = Get-Item "publish\TarkovClient.exe"
$fileSizeMB = [math]::Round($fileInfo.Length / 1MB, 2)

Write-Host "[SUCCESS] Publish completed successfully!" -ForegroundColor Green
Write-Host "[INFO] Output: publish\TarkovClient.exe ($fileSizeMB MB)" -ForegroundColor Yellow
Write-Host "[INFO] Features: Self-Contained, Single-File, All dependencies included" -ForegroundColor Yellow
Write-Host "[INFO] Users do not need to install .NET Runtime separately!" -ForegroundColor Green
Write-Host "[INFO] Unnecessary files (.pdb, .xml) removed for clean deployment" -ForegroundColor Yellow
Write-Host ""