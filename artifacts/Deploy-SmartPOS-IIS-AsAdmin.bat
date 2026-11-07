@echo off
title SmartPOS IIS deploy - approve UAC if asked
echo Deploying SmartPOS into C:\inetpub\wwwroot\exam
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "D:\New folder (2)\smartpos\artifacts\deploy-iis-exam.ps1"
echo.
echo Exit code: %ERRORLEVEL%
pause
