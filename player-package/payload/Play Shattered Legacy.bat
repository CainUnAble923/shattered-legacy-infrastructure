@echo off
rem Double-click me. Runs app\Play.ps1; see README.txt.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0app\Play.ps1" %*
if errorlevel 1 pause
