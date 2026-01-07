@echo off
echo Building MediaManagerHelper...
echo.

REM Переходим в папку скрипта
cd /d "%~dp0"

REM Собираем проект с оптимизациями для уменьшения размера
REM Примечание: Trimming и ReadyToRun отключены для максимального уменьшения размера
REM ReadyToRun увеличивает размер файла за счет предкомпилированного кода
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:EnableCompressionInSingleFile=true -p:PublishReadyToRun=false -p:PublishReadyToRunComposite=false -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false -p:InvariantGlobalization=true

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Build failed!
    exit /b %ERRORLEVEL%
)

echo.
echo Build successful!
echo.

REM Определяем путь к собранному файлу (относительно текущей папки)
set PUBLISH_PATH=bin\Release\net8.0-windows10.0.17763.0\win-x64\publish\MediaManagerHelper.exe
set TARGET_PATH=..\ru.valentderah.media-manager.sdPlugin\bin\MediaManagerHelper.exe

REM Проверяем, существует ли собранный файл
if not exist "%PUBLISH_PATH%" (
    echo ERROR: Built file not found at: %PUBLISH_PATH%
    echo Please check the build output above for errors.
    exit /b 1
)

REM Копируем файл
echo Copying MediaManagerHelper.exe to plugin bin folder...
copy /Y "%PUBLISH_PATH%" "%TARGET_PATH%"

if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to copy file to: %TARGET_PATH%
    exit /b %ERRORLEVEL%
)

REM Проверяем, что файл скопирован
if not exist "%TARGET_PATH%" (
    echo ERROR: File was not copied successfully!
    exit /b 1
)

echo.
echo Done! MediaManagerHelper.exe copied to plugin bin folder.
echo File location: %TARGET_PATH%

