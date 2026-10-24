@echo off
rem Inside the house: the TEST shard, straight to 192.168.1.58:2594, skipping DNS.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Play.ps1" -Lan %*
if errorlevel 1 pause
