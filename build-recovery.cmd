@echo off
setlocal
cd /d "%~dp0"
where x86_64-w64-mingw32-gcc >nul 2>nul
if errorlevel 1 (
  echo Rebuilding the native recovery EXE requires MinGW-w64 x64 GCC in PATH.
  echo The prebuilt MCRtool-Recovery-x64.exe is already included.
  pause
  exit /b 1
)
x86_64-w64-mingw32-gcc -Wall -Wextra -Werror -O2 -static -municode Recovery.c -o MCRtool-Recovery-x64.exe -ladvapi32
if errorlevel 1 (
  pause
  exit /b 1
)
echo Native x64 recovery executable built successfully.
pause
