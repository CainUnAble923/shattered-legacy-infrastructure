@echo off
rem The throwaway TEST shard, shatteredlegacyuo.com:2594. Works from anywhere.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Play.ps1" -Test %*
if errorlevel 1 pause
