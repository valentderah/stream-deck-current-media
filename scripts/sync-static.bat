@echo off
setlocal
cd /d "%~dp0\.."

set "ASSETS_DIR=assets"
set "DIST_DIR=dist\ru.valentderah.current-media.sdPlugin"

if not exist "%DIST_DIR%" mkdir "%DIST_DIR%"

echo ==> Syncing static assets...
robocopy "%ASSETS_DIR%" "%DIST_DIR%" /MIR /XD win mac /NFL /NDL /NJH /NJS
if %ERRORLEVEL% LEQ 7 (
    echo     Done.
    exit /b 0
) else (
    exit /b 1
)
