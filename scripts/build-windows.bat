@echo off
setlocal
cd /d "%~dp0\.."

if not exist "ru.valentderah.current-media.sdPlugin\win" mkdir "ru.valentderah.current-media.sdPlugin\win"

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

set PUBLISH=src\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\StreamDeckCurrentMedia.exe
copy /Y "%PUBLISH%" "ru.valentderah.current-media.sdPlugin\win\StreamDeckCurrentMedia.exe"
echo Done: win\StreamDeckCurrentMedia.exe
