@echo off
setlocal
cd /d "%~dp0"
set "COMPILER=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%COMPILER%" set "COMPILER=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%COMPILER%" (
  echo .NET Framework 4 compiler not found.
  pause
  exit /b 1
)
"%COMPILER%" /nologo /codepage:65001 /target:winexe /platform:anycpu /out:MCRtool.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll /reference:Microsoft.CSharp.dll MCRtool.cs FileSearch.cs Tasks.cs
if errorlevel 1 (
  pause
  exit /b 1
)
echo MCRtool.exe created successfully.
pause
