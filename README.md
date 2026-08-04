# AcadDwgBrowser — плагин AutoCAD (2024 / 2025 / 2026)

Windows .NET-плагин: команда `DWGBROWSER` → modeless-палитра → список DWG по API → скачивание → открытие в AutoCAD.

## Простая установка (рекомендуется)

На **Windows** с AutoCAD и [.NET SDK 8](https://dotnet.microsoft.com/download):

1. Скопируйте на ПК всю папку проекта  
2. Запустите **`Setup.bat`** (двойной клик)  
3. Дождитесь «Установка завершена»  
4. Полностью перезапустите AutoCAD  
5. Введите команду: **`DWGBROWSER`**

`Setup.bat` сам скачает NuGet-пакеты, соберёт DLL и поставит плагин в  
`%AppData%\Autodesk\ApplicationPlugins\`.

Краткая памятка: файл **`УСТАНОВКА.txt`**.

## Архитектура

```
команда DWGBROWSER
        ↓
PaletteSet (не блокирует AutoCAD)
        ↓
AcadDwgBrowser.Core  →  HTTP API (список + download)
        ↓
локальный .dwg
        ↓
DocumentManager.Open
```

| Проект | Target | AutoCAD |
|---|---|---|
| `AcadDwgBrowser.Core` | netstandard2.0 | — |
| `AcadDwgBrowser.Plugin.Acad2024` | net48 | 2024 |
| `AcadDwgBrowser.Plugin.Acad2025` | net8.0-windows | 2025 и 2026 |

Сборка идёт через NuGet `AutoCAD.NET` — локальные пути к AutoCAD для компиляции не нужны.

## Сборка вручную

```powershell
.\scripts\build.ps1
.\scripts\pack.ps1          # dist\AcadDwgBrowser-1.0.0.zip
```

На macOS (ZIP без DLL):

```bash
./scripts/pack.sh
```

## Раздача готового ZIP (после Setup/build на Windows)

Если DLL уже собраны, пользователю достаточно:

1. Распаковать `AcadDwgBrowser-*.zip`  
2. **`Install.bat`**  
3. Перезапуск AutoCAD → `DWGBROWSER`

ZIP **без** предварительной сборки на Windows не содержит DLL — команда не появится. Для установки из исходников всегда используйте **`Setup.bat`**.

## Конфиг API

После установки:

`%AppData%\Autodesk\ApplicationPlugins\AcadDwgBrowser.bundle\Contents\config.json`

Пример ответа списка: `mock-api/sample-list-response.json`.

## Важно

- Плагин только для **Windows**  
- AutoCAD 2025 и 2026 используют одну net8-сборку  
- Вход: команда **`DWGBROWSER`** (палитра modeless)
