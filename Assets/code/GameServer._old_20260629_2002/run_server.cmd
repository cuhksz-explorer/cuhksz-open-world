@echo off
setlocal EnableExtensions
cd /d "%~dp0"
if errorlevel 1 (
  echo [GameServer] ERROR: cd failed.
  pause
  exit /b 1
)

set "ROOT=%~dp0"
set "PORT=8765"
if exist "%ROOT%.env.lguhub.local" (
  for /f "usebackq tokens=1,* delims==" %%A in ("%ROOT%.env.lguhub.local") do (
    if /i "%%A"=="GAME_SSO_SECRET" set "GAME_SSO_SECRET=%%B"
    if /i "%%A"=="LGUHUB_BASE_URL" if not defined LGUHUB_BASE_URL set "LGUHUB_BASE_URL=%%B"
    if /i "%%A"=="GAME_SSO_ALLOWED_IPS" set "GAME_SSO_ALLOWED_IPS=%%B"
  )
)
if not defined LGUHUB_BASE_URL set "LGUHUB_BASE_URL=https://lguhub.cn"
if not defined GAME_ADMIN_TOKEN set "GAME_ADMIN_TOKEN=sc-lguexplorer"

set "BACKEND_DIR=%ROOT%backend"
if exist "%BACKEND_DIR%\server.py" goto :have_backend
set "BACKEND_DIR=%ROOT%..\code\GameServer\backend"
if exist "%BACKEND_DIR%\server.py" goto :have_backend

echo [GameServer] ERROR: server.py not found.
echo [GameServer] Tried:
echo [GameServer]   %ROOT%backend\server.py
echo [GameServer]   %ROOT%..\code\GameServer\backend\server.py
pause
exit /b 1

:have_backend
echo [GameServer] Uvicorn backend: %BACKEND_DIR%
echo [GameServer] LGUHUB_BASE_URL=%LGUHUB_BASE_URL%
if defined GAME_SSO_SECRET (
  echo [GameServer] GAME_SSO_SECRET=configured
) else (
  echo [GameServer] WARN: GAME_SSO_SECRET not set, local DB login only
)

set "PYEXE=%ROOT%.venv\Scripts\python.exe"
if not exist "%PYEXE%" (
  echo [GameServer] ERROR: venv not found. Run start_server.bat first.
  pause
  exit /b 1
)
"%PYEXE%" --version >nul 2>&1
if errorlevel 1 (
  echo [GameServer] ERROR: venv Python is broken. Run start_server.bat to recreate it.
  pause
  exit /b 1
)

"%PYEXE%" -m uvicorn server:app --app-dir "%BACKEND_DIR%" --host 0.0.0.0 --port %PORT%
echo.
echo [GameServer] Uvicorn exited. Press any key to close.
pause
exit /b 0
