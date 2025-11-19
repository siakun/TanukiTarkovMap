@echo off
echo ====================================
echo Building TarkovClient Single EXE...
echo ====================================

dotnet publish -p:PublishProfile=SingleFile

echo.
echo ====================================
echo Build Complete!
echo Output: bin\Release\Publish\SingleFile\TarkovClient.exe
echo ====================================
pause