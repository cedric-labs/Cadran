@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\publish-windows.ps1" %*
