@echo off
chcp 65001 >nul
setlocal EnableExtensions
cd /d "%~dp0"

set "LOG=%~dp0install-log.txt"
echo ==== AcadDwgBrowser install %DATE% %TIME% ==== > "%LOG%"
echo Dir: %CD%>> "%LOG%"
echo.

echo ========================================
echo  AcadDwgBrowser install
echo ========================================
echo Log: %LOG%
echo.

set "BUNDLE="
if exist "%~dp0AcadDwgBrowser.bundle\PackageContents.xml" set "BUNDLE=%~dp0AcadDwgBrowser.bundle"
if not defined BUNDLE if exist "%~dp0bundle\AcadDwgBrowser.bundle\PackageContents.xml" set "BUNDLE=%~dp0bundle\AcadDwgBrowser.bundle"

if not defined BUNDLE (
  echo ERROR: AcadDwgBrowser.bundle not found>> "%LOG%"
  echo ERROR: AcadDwgBrowser.bundle not found next to Install.bat
  echo Put Install.bat next to AcadDwgBrowser.bundle folder.
  notepad "%LOG%"
  pause
  exit /b 1
)

echo BUNDLE=%BUNDLE%>> "%LOG%"
echo Source: %BUNDLE%

set "HASDLL="
if exist "%BUNDLE%\Contents\2024\AcadDwgBrowser.dll" set "HASDLL=1"
if exist "%BUNDLE%\Contents\2025\AcadDwgBrowser.dll" set "HASDLL=1"
if not defined HASDLL (
  echo ERROR: no AcadDwgBrowser.dll in bundle>> "%LOG%"
  echo ERROR: no AcadDwgBrowser.dll inside bundle
  dir /s /b "%BUNDLE%\*.dll" >> "%LOG%" 2>&1
  notepad "%LOG%"
  pause
  exit /b 1
)

set "TARGET=%AppData%\Autodesk\ApplicationPlugins\AcadDwgBrowser.bundle"
echo TARGET=%TARGET%>> "%LOG%"
echo Target: %TARGET%

if not exist "%AppData%\Autodesk\ApplicationPlugins" mkdir "%AppData%\Autodesk\ApplicationPlugins"
if exist "%TARGET%" (
  echo Removing old install...>> "%LOG%"
  rmdir /s /q "%TARGET%"
)

echo Copying with robocopy...>> "%LOG%"
robocopy "%BUNDLE%" "%TARGET%" /E /NFL /NDL /NJH /NJS /nc /ns /np
set "RC=%ERRORLEVEL%"
echo robocopy exit=%RC%>> "%LOG%"

rem robocopy codes 0-7 are success
if %RC% GEQ 8 (
  echo ERROR: robocopy failed code %RC%>> "%LOG%"
  echo ERROR: copy failed, code %RC%
  notepad "%LOG%"
  pause
  exit /b 1
)

if not exist "%TARGET%\PackageContents.xml" (
  echo ERROR: PackageContents.xml missing after copy>> "%LOG%"
  echo ERROR: install folder incomplete
  notepad "%LOG%"
  pause
  exit /b 1
)

dir /s /b "%TARGET%\AcadDwgBrowser.dll" > "%TEMP%\acad-dlls.txt" 2>&1
type "%TEMP%\acad-dlls.txt" >> "%LOG%"

echo.
echo ========================================
echo  INSTALL OK
echo ========================================
echo %TARGET%
type "%TEMP%\acad-dlls.txt"
echo.

echo INSTALL OK> "%~dp0INSTALL-OK.txt"
echo %TARGET%>> "%~dp0INSTALL-OK.txt"
type "%TEMP%\acad-dlls.txt" >> "%~dp0INSTALL-OK.txt"

echo INSTALL OK> "%LOG%"
echo %TARGET%>> "%LOG%"

echo Created: %~dp0INSTALL-OK.txt
echo Created: %LOG%
echo.
echo Restart AutoCAD, then type: DWGBROWSER
echo.
pause
exit /b 0
