@echo off
rem Tell the package where Ultima Online is:  Setup.bat "D:\Games\Ultima Online Classic"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup.ps1" %*
pause
