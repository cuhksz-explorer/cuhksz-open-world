@echo off
cd /d "%~dp0"
if errorlevel 1 goto :fail_cd

title GameServer + LGUHUB Tunnel

echo.
echo [GameServer] One-click: SSH tunnel + GameServer
echo.

where ssh >nul 2>&1
if errorlevel 1 goto :fail_ssh

if not exist "%~dp0.venv\Scripts\python.exe" goto :fail_venv

netstat -ano | findstr ":5001 " | findstr "LISTENING" >nul
if errorlevel 1 (
  echo [GameServer] Starting SSH tunnel in a new window...
  start "LGUHUB SSH Tunnel" cmd /k call "%~dp0start_lguhub_tunnel_window.cmd"
) else (
  echo [GameServer] Port 5001 already listening, reusing tunnel.
)

echo [GameServer] Waiting for tunnel, then starting server...
"%~dp0.venv\Scripts\python.exe" "%~dp0tunnel_run_server.py"
set ERR=%ERRORLEVEL%
if not "%ERR%"=="0" goto :fail_run
exit /b 0

:fail_cd
echo [GameServer] ERROR: cannot cd to script folder.
pause
exit /b 1

:fail_ssh
echo [GameServer] ERROR: ssh not found. Install OpenSSH Client.
pause
exit /b 1

:fail_venv
echo [GameServer] ERROR: .venv not found. Run start_server.bat first.
pause
exit /b 1

:fail_run
echo.
echo [GameServer] Startup failed, code %ERR%
pause
exit /b %ERR%
