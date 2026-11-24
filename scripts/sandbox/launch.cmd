@echo off
rem Starts Windows Sandbox with the .wsb file given as the first argument.
rem Windows Sandbox only starts inside the signed-in desktop session. Run this file from that session:
rem double-click it, or start it from a scheduled task with an interactive logon. A plain remote shell
rem (SSH, a service) runs outside the desktop session and cannot open the sandbox window.
start "" WindowsSandbox.exe "%~1"
