@echo off
setlocal
cd /d "%~dp0\.."

set "WIN_DIR=dist\ru.valentderah.current-media.sdPlugin\win"

if exist "%WIN_DIR%" rmdir /S /Q "%WIN_DIR%"
mkdir "%WIN_DIR%"

echo ==> Publishing .NET (Windows)...
dotnet publish src\StreamDeckCurrentMedia.csproj ^
  -c Release ^
  -f net8.0-windows10.0.19041.0 ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:DebugType=none ^
  -p:DebugSymbols=false
if errorlevel 1 exit /b 1

set "PUBLISH=src\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\StreamDeckCurrentMedia.exe"
copy /Y "%PUBLISH%" "%WIN_DIR%\StreamDeckCurrentMedia.exe" > nul

echo     Saved to %WIN_DIR%
exit /b 0
