@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "PORT=8765"
set "QUIET=0"
if /i "%~1"=="quiet" set "QUIET=1"

if "%QUIET%"=="0" echo [GameServer] Stopping server on port %PORT%...

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0stop_server.ps1" -Port %PORT%
set "STOP_RC=%ERRORLEVEL%"

if not "%STOP_RC%"=="0" (
  echo [GameServer] PowerShell stop failed, trying netstat fallback...
  for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":%PORT%" ^| findstr LISTENING') do (
    taskkill /PID %%a /F >nul 2>&1
  )
)

if "%QUIET%"=="0" (
  set "STILL="
  for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":%PORT%" ^| findstr LISTENING') do set "STILL=1"
  if defined STILL (
    echo [GameServer] WARN - Port %PORT% still in use. Close the old server window or end the process manually.
  ) else (
    echo [GameServer] Port %PORT% released.
  )
  echo.
  pause
)

exit /b 0
