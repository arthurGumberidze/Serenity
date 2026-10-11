@echo off
setlocal
echo U29 focused diagnostic: about 18 minutes plus build. No Codex/API calls.
echo Do not use the PC while the visible Player runs. This is not the final U29 gate.
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Run-U29-Diagnostic.ps1" %*
set "RESULT=%ERRORLEVEL%"
echo Exit code: %RESULT%
pause
exit /b %RESULT%
