@echo off
chcp 65001 >nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1" -TrustCertificate
if errorlevel 1 echo A instalacao falhou. Leia a mensagem acima.
pause
