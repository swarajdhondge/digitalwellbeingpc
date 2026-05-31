@echo off
REM Build & launch the Digital Wellbeing (Pulse) app. Double-click or run from a terminal.
REM See run.ps1 for options and the Quick Heal / antivirus note.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run.ps1" %*
if errorlevel 1 pause
