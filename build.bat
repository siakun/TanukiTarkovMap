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

:: Build Single File Executable
echo.
echo [2/3] Building single file executable...
dotnet publish "%PROJECT_PATH%" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
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
if exist "%OUTPUT_DIR%\Resources" rd /s /q "%OUTPUT_DIR%\Resources" >nul 2>&1
echo Cleaned up unnecessary files.

:: Show build results
echo.
echo ========================================
echo Build completed successfully!
echo ========================================
echo.
echo Output: %OUTPUT_DIR%\TanukiTarkovMap.exe
echo.

:: Display file size
for %%F in ("%OUTPUT_DIR%\TanukiTarkovMap.exe") do (
    set /a size=%%~zF/1048576
    echo File Size: !size! MB
)

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