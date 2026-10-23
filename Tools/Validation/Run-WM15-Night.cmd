@echo off
setlocal
title Serenity - WM15 overnight validation
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0Run-WM15.ps1" %*
set "WM15_EXIT=%ERRORLEVEL%"
echo.
echo Validation finished. Exit code: %WM15_EXIT%
echo Reports: C:\serenity_game\Artifacts\WM15\night-*
echo 0 = automated pass; 1 = failed checks; 2 = partial; 3 = runner error.
pause
exit /b %WM15_EXIT%
