@echo off
setlocal EnableExtensions
cd /d "%~dp0"

net session >nul 2>&1
if errorlevel 1 (
  echo [GameServer] Requesting Administrator privileges for firewall setup...
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b 0
)

set "PORT=8765"
echo [GameServer] Configuring LAN firewall (TCP/UDP %PORT%)...

call :ensure_firewall_rule "GameServer TCP 8765 LAN" TCP 8765
call :ensure_firewall_rule "GameServer UDP 8765 LAN" UDP 8765
call :ensure_firewall_rule "GameServer TCP 5000 LAN" TCP 5000
call :ensure_firewall_rule "GameServer UDP 5000 LAN" UDP 5000

echo.
echo [GameServer] Firewall done. Teammate test: http://YOUR_LAN_IP:%PORT%/health
pause
exit /b 0

:ensure_firewall_rule
setlocal
set "RULE_NAME=%~1"
set "RULE_PROTO=%~2"
set "RULE_PORT=%~3"
set "LAN_REMOTE=10.0.0.0/8,172.16.0.0/12,192.168.0.0/16"
netsh advfirewall firewall delete rule name="%RULE_NAME%" >nul 2>&1
netsh advfirewall firewall add rule name="%RULE_NAME%" dir=in action=allow protocol=%RULE_PROTO% localport=%RULE_PORT% remoteip=%LAN_REMOTE% profile=any enable=yes
if errorlevel 1 (
  echo [GameServer] FAIL: %RULE_NAME%
) else (
  echo [GameServer] OK: %RULE_NAME%
)
endlocal & exit /b 0
