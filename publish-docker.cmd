@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\publish-docker.ps1" %*
