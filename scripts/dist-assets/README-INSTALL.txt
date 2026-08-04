AcadDwgBrowser — установка

ВАЖНО: Install.bat подходит только если в пакете УЖЕ есть AcadDwgBrowser.dll
(после сборки на Windows через Setup.bat / build.ps1).

Если DLL нет — не используйте этот Install.bat из «пустого» архива.
Возьмите весь проект и запустите Setup.bat в корне.

1. Распакуйте архив (с DLL).
2. Запустите Install.bat.
3. Перезапустите AutoCAD.
4. Команда: DWGBROWSER

Плагин копируется в:
  %AppData%\Autodesk\ApplicationPlugins\AcadDwgBrowser.bundle

Настройка API: Contents\config.json
