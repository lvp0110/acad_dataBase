@echo off
chcp 65001 >nul
setlocal EnableExtensions
cd /d "%~dp0"

set "LOG=%~dp0Setup.log"
set "PS=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"

echo ========================================
echo  AcadDwgBrowser — установка для AutoCAD
echo ========================================
echo.
echo Папка: %CD%
echo Лог:   %LOG%
echo.

if not exist "%~dp0Setup.ps1" (
  echo ОШИБКА: рядом с Setup.bat нет файла Setup.ps1
  echo Нужна ПОЛНАЯ папка проекта, не только один bat-файл.
  echo.
  pause
  exit /b 1
)

if not exist "%~dp0src\AcadDwgBrowser.Plugin.Acad2025\AcadDwgBrowser.Plugin.Acad2025.csproj" (
  echo ОШИБКА: не найдена папка src\ с проектами плагина.
  echo Скопируйте на Windows весь проект "autocad database".
  echo.
  pause
  exit /b 1
)

where dotnet >nul 2>&1
if errorlevel 1 (
  echo ОШИБКА: не найден dotnet.
  echo Установите .NET SDK 8: https://dotnet.microsoft.com/download
  echo Затем снова запустите Setup.bat
  echo.
  pause
  exit /b 1
)

echo Запуск Setup.ps1 ...
echo.

"%PS%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup.ps1" -LogPath "%LOG%"
set ERR=%ERRORLEVEL%

echo.
echo ---------- хвост лога ----------
if exist "%LOG%" (
  powershell -NoProfile -Command "Get-Content -LiteralPath '%LOG%' -Tail 40"
) else (
  echo Файл лога не создан.
)
echo --------------------------------
echo.

if %ERR% neq 0 (
  echo Установка НЕ завершена. Код: %ERR%
  echo Откройте Setup.log рядом с Setup.bat и пришлите текст ошибки.
  start "" notepad "%LOG%" 2>nul
) else (
  echo Установка ЗАВЕРШЕНА успешно.
  echo Папка плагина:
  echo   %AppData%\Autodesk\ApplicationPlugins\AcadDwgBrowser.bundle
  echo.
  echo Перезапустите AutoCAD и введите: DWGBROWSER
)

echo.
pause
exit /b %ERR%
