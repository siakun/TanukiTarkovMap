@echo off
setlocal enabledelayedexpansion

echo ========================================
echo TanukiTarkovMap Release Build (Velopack)
echo ========================================
echo.

:: Set paths
set PROJECT_PATH=src\TanukiTarkovMap\TanukiTarkovMap.csproj
set PUBLISH_DIR=publish
set RELEASE_DIR=releases

:: Version (수동 지정 또는 자동)
if "%1"=="" (
    set VERSION=1.0.0
) else (
    set VERSION=%1
)

echo Version: %VERSION%
echo.

:: Clean previous builds
echo [1/4] Cleaning previous builds...
if exist "%PUBLISH_DIR%" rd /s /q "%PUBLISH_DIR%"
if exist "%RELEASE_DIR%" rd /s /q "%RELEASE_DIR%"
mkdir "%PUBLISH_DIR%"
mkdir "%RELEASE_DIR%"

:: Publish application
echo.
echo [2/4] Publishing application...
dotnet publish "%PROJECT_PATH%" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -o "%PUBLISH_DIR%"

if !errorlevel! neq 0 (
    echo.
    echo [ERROR] Publish failed!
    goto :error
)

:: Check vpk tool
echo.
echo [3/4] Checking vpk tool...
where vpk >nul 2>&1
if !errorlevel! neq 0 (
    echo Installing vpk tool...
    dotnet tool install -g vpk
    if !errorlevel! neq 0 (
        echo [ERROR] Failed to install vpk tool!
        goto :error
    )
)

:: Pack with Velopack
echo.
echo [4/4] Packing with Velopack...
vpk pack ^
    --packId "TanukiTarkovMap" ^
    --packVersion "%VERSION%" ^
    --packDir "%PUBLISH_DIR%" ^
    --mainExe "TanukiTarkovMap.exe" ^
    --outputDir "%RELEASE_DIR%"

if !errorlevel! neq 0 (
    echo.
    echo [ERROR] Velopack packing failed!
    goto :error
)

:: Remove unnecessary files
del "%RELEASE_DIR%\assets.win.json" 2>nul
del "%RELEASE_DIR%\RELEASES" 2>nul

:: Show build results
echo.
echo ========================================
echo Build completed successfully!
echo ========================================
echo.
echo Output: %RELEASE_DIR%\
echo.
echo Files created:
dir /b "%RELEASE_DIR%"
echo.

:: Display folder size
for /f "usebackq" %%a in (`powershell -Command "(Get-ChildItem -Path '%RELEASE_DIR%' -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB -as [int]"`) do set sizemb=%%a
echo Total Size: !sizemb! MB

echo.
echo To release on GitHub:
echo   1. Create a new release on GitHub
echo   2. Upload all files from %RELEASE_DIR%\
echo.
echo Press any key to open releases folder...
pause >nul
start "" "%RELEASE_DIR%"
goto :end

:error
echo.
echo ========================================
echo Build failed! Check the error messages above.
echo ========================================
pause
exit /b 1

:end
endlocal
exit /b 0
