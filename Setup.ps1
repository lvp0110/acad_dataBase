#Requires -Version 5.1
<#
.SYNOPSIS
  Build + install AcadDwgBrowser into AutoCAD ApplicationPlugins.
#>
param(
    [ValidateSet("User", "AllUsers")]
    [string]$Scope = "User",
    [switch]$Skip2024,
    [switch]$Skip2025,
    [string]$LogPath = ""
)

$ErrorActionPreference = "Stop"
$exitCode = 1

function Write-Log {
    param([string]$Message, [string]$Color = "White")
    $line = "[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $Message
    if ($script:LogFile) {
        Add-Content -LiteralPath $script:LogFile -Value $line -Encoding UTF8
    }
    if ($Color -eq "White") {
        Write-Host $line
    } else {
        Write-Host $line -ForegroundColor $Color
    }
}

function Write-Step([string]$text) {
    Write-Log ""
    Write-Log "==> $text" "Cyan"
}

function Show-Notify([string]$title, [string]$text) {
    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue
        [System.Windows.Forms.MessageBox]::Show($text, $title) | Out-Null
    } catch {
        # ignore UI failures
    }
}

try {
    if ([string]::IsNullOrWhiteSpace($LogPath)) {
        $LogPath = Join-Path $PSScriptRoot "Setup.log"
    }
    $script:LogFile = $LogPath
    Set-Content -LiteralPath $script:LogFile -Value ("AcadDwgBrowser Setup log " + (Get-Date -Format o)) -Encoding UTF8

    $root = $PSScriptRoot
    if ([string]::IsNullOrWhiteSpace($root)) {
        $root = Split-Path -Parent $MyInvocation.MyCommand.Path
    }

    Write-Log ("Root: " + $root)
    Write-Log ("User: " + $env:USERNAME)
    Write-Log ("PS:   " + $PSVersionTable.PSVersion)

    if ($env:OS -ne "Windows_NT") {
        throw "Только Windows."
    }

    $bundleName = "AcadDwgBrowser.bundle"
    $bundlePath = Join-Path $root "bundle\$bundleName"
    $assets = Join-Path $root "scripts\dist-assets"
    $pkg2024 = Join-Path $root "src\AcadDwgBrowser.Plugin.Acad2024\AcadDwgBrowser.Plugin.Acad2024.csproj"
    $pkg2025 = Join-Path $root "src\AcadDwgBrowser.Plugin.Acad2025\AcadDwgBrowser.Plugin.Acad2025.csproj"

    if (-not (Test-Path -LiteralPath $pkg2025) -and -not (Test-Path -LiteralPath $pkg2024)) {
        throw "Не найдены проекты в src\. Нужна полная папка проекта."
    }

    Write-Step "Проверка dotnet"
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        throw "dotnet не найден. Установите .NET SDK 8: https://dotnet.microsoft.com/download"
    }
    Write-Log ("dotnet: " + $dotnet.Source)
    $sdks = & dotnet --list-sdks 2>&1
    Write-Log ("SDKs:`n" + ($sdks | Out-String))

    Write-Step "Подготовка bundle"
    New-Item -ItemType Directory -Force -Path (Join-Path $bundlePath "Contents\2024") | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $bundlePath "Contents\2025") | Out-Null
    if (-not (Test-Path -LiteralPath (Join-Path $bundlePath "PackageContents.xml"))) {
        throw "Нет PackageContents.xml в $bundlePath"
    }
    foreach ($name in @("Install.bat", "Install.ps1", "README-INSTALL.txt")) {
        $src = Join-Path $assets $name
        if (Test-Path -LiteralPath $src) {
            Copy-Item -LiteralPath $src -Destination (Join-Path $bundlePath $name) -Force
        }
    }

    $built = New-Object System.Collections.Generic.List[string]
    $errors = New-Object System.Collections.Generic.List[string]

    function Invoke-PluginBuild {
        param(
            [string]$Label,
            [string]$ProjectPath,
            [string]$DllPath
        )

        if (-not (Test-Path -LiteralPath $ProjectPath)) {
            $errors.Add("${Label}: нет файла проекта ${ProjectPath}")
            return
        }

        Write-Step "Сборка $Label"
        Write-Log ("Project: " + $ProjectPath)

        # Native stderr from dotnet becomes ErrorRecord under Stop; do not treat it as fatal.
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        try {
            $out = & dotnet build $ProjectPath -c Release --verbosity minimal 2>&1
            $code = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $prevEap
        }
        $out | ForEach-Object { Write-Log (($_ | Out-String).TrimEnd()) }

        if ($code -ne 0) {
            $errors.Add("${Label}: dotnet build завершился с кодом $code")
            return
        }

        if (-not (Test-Path -LiteralPath $DllPath)) {
            $errors.Add("${Label}: сборка ок, но нет DLL: ${DllPath}")
            return
        }

        $built.Add("${Label} -> ${DllPath}")
        Write-Log ("OK: " + $DllPath) "Green"
    }

    if (-not $Skip2024) {
        Invoke-PluginBuild -Label "AutoCAD 2024 (net48)" `
            -ProjectPath $pkg2024 `
            -DllPath (Join-Path $bundlePath "Contents\2024\AcadDwgBrowser.dll")
    }

    if (-not $Skip2025) {
        Invoke-PluginBuild -Label "AutoCAD 2025/2026 (net8)" `
            -ProjectPath $pkg2025 `
            -DllPath (Join-Path $bundlePath "Contents\2025\AcadDwgBrowser.dll")
    }

    if ($built.Count -eq 0) {
        $detail = ($errors -join "`n")
        throw @"
Не удалось собрать ни одну DLL. ApplicationPlugins не обновлён.

Ошибки:
$detail

Частые причины:
- нет .NET SDK 8
- нет .NET Framework 4.8 Developer Pack (для AutoCAD 2024)
- нет интернета для NuGet (пакет AutoCAD.NET)
"@
    }

    if ($errors.Count -gt 0) {
        Write-Log "Часть сборок не удалась, ставим то что собралось:" "Yellow"
        $errors | ForEach-Object { Write-Log ("  - " + $_) "Yellow" }
    }

    Write-Step "Копирование в ApplicationPlugins"
    if ($Scope -eq "User") {
        $targetRoot = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins"
    } else {
        $targetRoot = Join-Path $env:ProgramData "Autodesk\ApplicationPlugins"
    }
    $target = Join-Path $targetRoot $bundleName
    Write-Log ("Target: " + $target)

    New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null
    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
    Copy-Item -LiteralPath $bundlePath -Destination $target -Recurse -Force

    $installedDlls = @(Get-ChildItem -LiteralPath $target -Recurse -Filter "AcadDwgBrowser.dll" -ErrorAction SilentlyContinue)
    if ($installedDlls.Count -eq 0) {
        throw "Копирование выполнено, но AcadDwgBrowser.dll не найден в $target"
    }

    Write-Log ""
    Write-Log "========================================" "Green"
    Write-Log " УСТАНОВКА ЗАВЕРШЕНА" "Green"
    Write-Log "========================================" "Green"
    Write-Log ("Папка: " + $target)
    $built | ForEach-Object { Write-Log ("  " + $_) }
    $installedDlls | ForEach-Object { Write-Log ("  DLL: " + $_.FullName) }
    Write-Log "Дальше: перезапуск AutoCAD → команда DWGBROWSER"
    Write-Log ("Конфиг: " + (Join-Path $target "Contents\config.json"))

    Show-Notify "AcadDwgBrowser" @"
Установка завершена.

$target

Перезапустите AutoCAD и введите:
DWGBROWSER
"@

    $exitCode = 0
}
catch {
    $msg = $_.Exception.Message
    Write-Log ("ОШИБКА: " + $msg) "Red"
    if ($_.ScriptStackTrace) {
        Write-Log $_.ScriptStackTrace "DarkGray"
    }
    Show-Notify "AcadDwgBrowser — ошибка" ("Установка не удалась.`n`n" + $msg + "`n`nСм. Setup.log")
    $exitCode = 1
}

exit $exitCode
