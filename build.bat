@echo off
setlocal enabledelayedexpansion

echo ========================================
echo TanukiTarkovMap Release Build
echo ========================================
echo.

:: Set paths
set PROJECT_PATH=src\TanukiTarkovMap\TanukiTarkovMap.csproj
set OUTPUT_DIR=release

:: Clean previous builds
echo [1/3] Cleaning previous builds...
if exist "%OUTPUT_DIR%" (
    rd /s /q "%OUTPUT_DIR%"
)
mkdir "%OUTPUT_DIR%"

:: Build Release (CefSharp는 SingleFile 미지원)
echo.
echo [2/3] Building release...
dotnet publish "%PROJECT_PATH%" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=false ^
    -p:DebugType=None ^
    -p:DebugSymbols=false ^
    -o "%OUTPUT_DIR%"

if !errorlevel! neq 0 (
    echo.
    echo [ERROR] Build failed!
    goto :error
)

:: Clean up unnecessary files
echo.
echo [3/3] Cleaning up unnecessary files...
if exist "%OUTPUT_DIR%\*.xml" del /q "%OUTPUT_DIR%\*.xml" >nul 2>&1
if exist "%OUTPUT_DIR%\*.pdb" del /q "%OUTPUT_DIR%\*.pdb" >nul 2>&1
echo Cleaned up unnecessary files.

:: Show build results
echo.
echo ========================================
echo Build completed successfully!
echo ========================================
echo.
echo Output: %OUTPUT_DIR%\
echo.

:: Display folder size
for /f "tokens=3" %%a in ('dir "%OUTPUT_DIR%" /s /-c ^| findstr "File(s)"') do set totalsize=%%a
set /a sizemb=!totalsize!/1048576
echo Total Size: !sizemb! MB

echo.
echo Press any key to open release folder...
pause >nul
start "" "%OUTPUT_DIR%"
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