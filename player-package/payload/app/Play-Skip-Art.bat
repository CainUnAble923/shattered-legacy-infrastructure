@echo off
rem Starts the game with the art as it is now, checking and preparing nothing (cc-P411). See README.txt.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Play.ps1" -SkipArtRebuild %*
if errorlevel 1 pause
