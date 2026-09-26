@echo off
rem Inside the house: connect straight to 192.168.1.58:2593, skipping DNS.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Play.ps1" -Lan %*
if errorlevel 1 pause
