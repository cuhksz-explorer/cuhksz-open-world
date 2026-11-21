@echo off
setlocal EnableExtensions
cd /d "%~dp0"
if errorlevel 1 goto cd_fail

set "ROOT=%~dp0"
set "PORT=8765"
set "GAME_SERVER_PORT=%PORT%"
set "GAME_ADMIN_TOKEN=sc-lguexplorer"

REM Open LAN firewall ports (needs Administrator; skipped if not elevated)
call :ensure_firewall_rule "GameServer TCP 8765 LAN" TCP 8765
call :ensure_firewall_rule "GameServer UDP 8765 LAN" UDP 8765
call :ensure_firewall_rule "GameServer TCP 5000 LAN" TCP 5000
call :ensure_firewall_rule "GameServer UDP 5000 LAN" UDP 5000

net session >nul 2>&1
if errorlevel 1 (
  echo [GameServer] WARN - Not running as Administrator. Firewall may block teammates.
  echo [GameServer]       Right-click start_server.bat and choose Run as administrator
)

set "PORT_BUSY="
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":%PORT%" ^| findstr LISTENING') do (
  set "PORT_BUSY=1"
)
if defined PORT_BUSY (
  echo [GameServer] Port %PORT% is in use, trying to stop old server...
  call "%ROOT%stop_server.bat" quiet
)

set "BACKEND_DIR=%ROOT%backend"
if exist "%BACKEND_DIR%\requirements.txt" goto have_backend
set "BACKEND_DIR=%ROOT%..\code\GameServer\backend"
if exist "%BACKEND_DIR%\requirements.txt" goto have_backend

echo [GameServer] ERROR: requirements.txt not found.
echo [GameServer] Tried:
echo [GameServer]   %ROOT%backend\requirements.txt
echo [GameServer]   %ROOT%..\code\GameServer\backend\requirements.txt
pause
exit /b 1

:have_backend
set "PYEXE=%ROOT%.venv\Scripts\python.exe"

set "VENV_BROKEN="
if exist "%PYEXE%" (
  "%PYEXE%" --version >nul 2>&1
  if errorlevel 1 set "VENV_BROKEN=1"
) else (
  set "VENV_BROKEN=1"
)

if defined VENV_BROKEN (
  echo [GameServer] venv missing or broken, recreating...
  if exist "%ROOT%.venv" rmdir /s /q "%ROOT%.venv"
  py -3.12 -m venv "%ROOT%.venv" 2>nul
  if errorlevel 1 py -3 -m venv "%ROOT%.venv" 2>nul
  if errorlevel 1 python -m venv "%ROOT%.venv"
  if not exist "%PYEXE%" (
    echo [GameServer] ERROR - Python not found. Install Python 3.10+
    pause
    exit /b 1
  )
  "%PYEXE%" --version >nul 2>&1
  if errorlevel 1 (
    echo [GameServer] ERROR - venv still broken. Install Python 3.10+
    pause
    exit /b 1
  )
)

if not exist "%PYEXE%" (
  echo [GameServer] Creating venv...
  py -3 -m venv .venv 2>nul
  if errorlevel 1 python -m venv .venv
  if not exist "%PYEXE%" (
    echo [GameServer] ERROR - Python not found. Install Python 3.10+
    pause
    exit /b 1
  )
)

echo [GameServer] Root: %ROOT%
echo [GameServer] Backend: %BACKEND_DIR%
echo [GameServer] Installing deps...
"%PYEXE%" -m pip install -q -r "%BACKEND_DIR%\requirements.txt"
if errorlevel 1 (
  echo [GameServer] ERROR - pip install failed.
  pause
  exit /b 1
)

echo.
echo [GameServer] ========================================
echo [GameServer]   Single window mode
echo [GameServer]   Local: http://127.0.0.1:%PORT%
call :print_lan_urls %PORT%
echo [GameServer]   Teammates use the LAN URL above (same as Unity baseUrl)
echo [GameServer]   After start, run verify_lan_access.ps1 to test LAN access
echo [GameServer]   Stop: Ctrl+C in this window, or run stop_server.bat
echo [GameServer] ========================================
echo.

call "%ROOT%run_server.cmd"
exit /b %ERRORLEVEL%

:cd_fail
echo [GameServer] ERROR: Could not cd to script folder.
pause
exit /b 1

:ensure_firewall_rule
setlocal
set "RULE_NAME=%~1"
set "RULE_PROTO=%~2"
set "RULE_PORT=%~3"
set "LAN_REMOTE=10.0.0.0/8,172.16.0.0/12,192.168.0.0/16"

net session >nul 2>&1
if errorlevel 1 (
  echo [GameServer] WARN - Firewall setup skipped, need Administrator: %RULE_NAME%
  endlocal & exit /b 0
)

netsh advfirewall firewall delete rule name="%RULE_NAME%" >nul 2>&1
netsh advfirewall firewall add rule name="%RULE_NAME%" dir=in action=allow protocol=%RULE_PROTO% localport=%RULE_PORT% remoteip=%LAN_REMOTE% profile=any enable=yes >nul 2>&1
if errorlevel 1 (
  echo [GameServer] WARN - Firewall rule failed: %RULE_NAME%
) else (
  echo [GameServer] Firewall rule OK - %RULE_NAME%
)

endlocal & exit /b 0

:print_lan_urls
setlocal
set "P=%~1"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0print_lan_urls.ps1" -Port %P%
endlocal & exit /b 0
