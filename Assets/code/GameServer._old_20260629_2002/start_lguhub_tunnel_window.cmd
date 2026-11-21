@echo off
title LGUHUB SSH Tunnel
echo Keep this window open. Closing it stops the tunnel.
echo Command: ssh -N -L 5001:127.0.0.1:5000 lguhub
echo.
ssh -N -o ServerAliveInterval=30 -o ExitOnForwardFailure=yes -L 5001:127.0.0.1:5000 lguhub
echo.
echo SSH tunnel ended. Test with: ssh lguhub
pause
