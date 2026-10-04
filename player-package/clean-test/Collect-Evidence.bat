@echo off
rem Windows Sandbox only: writes C:\SL\results\evidence.txt (cc-P35). Leave the game running.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Collect-Evidence.ps1"
pause
