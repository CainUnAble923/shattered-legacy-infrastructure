@echo off
rem Double-click me for the throwaway TEST shard. See README.txt.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0app\Play.ps1" -Test %*
if errorlevel 1 pause
