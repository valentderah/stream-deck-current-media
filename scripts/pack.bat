@echo off
setlocal
cd /d "%~dp0\.."
streamdeck pack ru.valentderah.current-media.sdPlugin -o dist --force
