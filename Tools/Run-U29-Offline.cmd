@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
echo U29 offline validation will take roughly 90-120 minutes.
echo Do not use this PC while the visible Player performance matrix is running.
echo The run is fully local and does not call Codex or consume model-token limits.
echo.
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%Run-U29-Offline.ps1" %*
set "RESULT=%ERRORLEVEL%"
echo.
if "%RESULT%"=="0" (
  echo U29 offline validation PASSED.
) else (
  echo U29 offline validation FAILED or is incomplete. Open the newest Logs\U29\offline-* report.
)
echo Exit code: %RESULT%
pause
exit /b %RESULT%
