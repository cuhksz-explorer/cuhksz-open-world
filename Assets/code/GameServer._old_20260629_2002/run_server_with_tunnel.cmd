@echo off
cd /d "%~dp0"
call "%~dp0START_GAME_SERVER_TUNNEL.cmd"
exit /b %ERRORLEVEL%
