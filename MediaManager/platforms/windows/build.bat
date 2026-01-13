@echo off
setlocal enabledelayedexpansion

REM Проверка аргументов для отключения AOT (если нужно)
set DISABLE_AOT=false
if "%1"=="no-aot" set DISABLE_AOT=true
if "%1"=="--no-aot" set DISABLE_AOT=true
if "%1"=="-no-aot" set DISABLE_AOT=true

echo Building MediaManager for Windows...
if "%DISABLE_AOT%"=="true" (
    echo Mode: Standard .NET (AOT disabled)
    set AOT_PARAM=-p:PublishAot=false
) else (
    echo Mode: .NET Native AOT (default)
    echo Compiling with .NET Native AOT (this may take longer)...
    set AOT_PARAM=-p:PublishAot=true
)
echo.

cd /d "%~dp0"

dotnet publish -c Release -r win-x64 --self-contained true %AOT_PARAM% -p:PublishTrimmed=true -p:DebugType=none -p:DebugSymbols=false -p:InvariantGlobalization=true -p:TrimMode=full -p:TrimAnalysis=false -p:EventSourceSupport=false -p:UseSystemResourceKeys=true

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Build failed!
    exit /b %ERRORLEVEL%
)

echo.
echo Build successful!
echo.

REM В AOT режиме файл может быть в разных местах
set PUBLISH_PATH_PUBLISH=bin\Release\net8.0-windows10.0.17763.0\win-x64\publish\MediaManager.exe
set PUBLISH_PATH_DIRECT=bin\Release\net8.0-windows10.0.17763.0\win-x64\MediaManager.exe

set PUBLISH_PATH=
if exist "%PUBLISH_PATH_PUBLISH%" (
    set PUBLISH_PATH=%PUBLISH_PATH_PUBLISH%
) else if exist "%PUBLISH_PATH_DIRECT%" (
    set PUBLISH_PATH=%PUBLISH_PATH_DIRECT%
)

if not defined PUBLISH_PATH (
    echo ERROR: Built file not found!
    echo Checked locations:
    echo   %PUBLISH_PATH_PUBLISH%
    echo   %PUBLISH_PATH_DIRECT%
    echo Please check the build output above for errors.
    exit /b 1
)

set TARGET_DIR=%~dp0..\..\..\ru.valentderah.current-media.sdPlugin\bin
set TARGET_PATH=%TARGET_DIR%\MediaManager.exe

REM Проверка наличия UPX для сжатия
set UPX_PATH=
if exist "%~dp0tools\upx\upx.exe" (
    set UPX_PATH=%~dp0tools\upx\upx.exe
) else if exist "%UPX_PATH%" (
    REM Используем переменную окружения, если установлена
) else (
    where upx.exe >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        set UPX_PATH=upx.exe
    )
)

if defined UPX_PATH (
    echo.
    echo Compressing with UPX...
    echo Original size:
    dir "%PUBLISH_PATH%" | findstr MediaManager.exe
    
    REM Создаем резервную копию перед сжатием
    set BACKUP_PATH=%PUBLISH_PATH%.backup
    copy /Y "%PUBLISH_PATH%" "%BACKUP_PATH%" >nul
    
    REM Пробуем сжать с разными уровнями сжатия
    "%UPX_PATH%" --best --lzma "%PUBLISH_PATH%" 2>nul
    if %ERRORLEVEL% NEQ 0 (
        echo UPX compression failed, trying with --ultra-brute...
        copy /Y "%BACKUP_PATH%" "%PUBLISH_PATH%" >nul
        "%UPX_PATH%" --ultra-brute --lzma "%PUBLISH_PATH%" 2>nul
        if %ERRORLEVEL% NEQ 0 (
            echo UPX compression failed, using original file.
            copy /Y "%BACKUP_PATH%" "%PUBLISH_PATH%" >nul
        )
    )
    
    if exist "%BACKUP_PATH%" (
        del "%BACKUP_PATH%" >nul 2>&1
    )
    
    echo Compressed size:
    dir "%PUBLISH_PATH%" | findstr MediaManager.exe
) else (
    echo.
    echo UPX not found. Skipping compression.
    echo To enable compression, either:
    echo   1. Place upx.exe in tools\upx\ folder
    echo   2. Add UPX to PATH
    echo   3. Set UPX_PATH environment variable
)

if not exist "%TARGET_DIR%" (
    mkdir "%TARGET_DIR%"
)

echo.
echo Copying MediaManager.exe to plugin bin folder...
copy /Y "%PUBLISH_PATH%" "%TARGET_PATH%"

if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to copy file to: %TARGET_PATH%
    exit /b %ERRORLEVEL%
)

if not exist "%TARGET_PATH%" (
    echo ERROR: File was not copied successfully!
    exit /b 1
)

echo.
echo Done! MediaManager.exe copied to plugin bin folder.
echo File location: %TARGET_PATH%

