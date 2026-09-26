@echo off
rem The throwaway TEST shard, 192.168.1.58:2594. Inside the house only.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Play.ps1" -Test %*
if errorlevel 1 pause
