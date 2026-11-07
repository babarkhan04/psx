@echo off
setlocal
cd /d "%~dp0"

echo [SmartPOS] Installing React dependencies...
if exist package-lock.json (
  call npm ci
) else (
  call npm install
)
if errorlevel 1 exit /b %errorlevel%

echo [SmartPOS] Building React frontend...
call npm run build
if errorlevel 1 exit /b %errorlevel%

if not exist "..\wwwroot\react\index.html" (
  echo ERROR: React build completed but ..\wwwroot\react\index.html was not created.
  exit /b 1
)

echo.
echo React build created successfully:
echo   ..\wwwroot\react\index.html
endlocal
