@echo off
setlocal
cd /d "%~dp0.."
set "COMPILER=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%COMPILER%" set "COMPILER=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
"%COMPILER%" /nologo /codepage:65001 /out:MCRtool.Tests.exe /reference:MCRtool.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll tests\Tests.cs
if errorlevel 1 exit /b 1
MCRtool.Tests.exe
pause
