@echo off
cd /d "%~dp0"
call "%~dp0START_GAME_SERVER_TUNNEL.cmd"
if errorlevel 1 pause
exit /b %ERRORLEVEL%
