@echo off
rem Double-click me to get the newest Shattered Legacy Legion scripts now. Play does this on every start too.
rem Runs app\Update-Scripts.ps1; your own scripts are never touched, and a published script you edited is kept in
rem app\tazuo\LegionScripts\backup\ before it is replaced.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0app\Update-Scripts.ps1" -Offer %*
pause
