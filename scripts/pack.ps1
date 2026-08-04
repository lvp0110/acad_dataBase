#Requires -Version 5.1
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Version = "",
    [string]$OutputDir = "",
    [switch]$SkipBuild,
    [switch]$SkipDllCheck
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = Split-Path -Parent $PSScriptRoot
$bundleName = "AcadDwgBrowser.bundle"
$bundlePath = Join-Path $root "bundle\$bundleName"
$packageXml = Join-Path $bundlePath "PackageContents.xml"
$distAssets = Join-Path $PSScriptRoot "dist-assets"
$assetFiles = @("Install.bat", "Install.ps1", "README-INSTALL.txt")

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot "build.ps1") -Configuration $Configuration
}

if (-not (Test-Path -LiteralPath $packageXml)) {
    throw "PackageContents.xml not found: $packageXml"
}

foreach ($required in $assetFiles) {
    $path = Join-Path $distAssets $required
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing dist asset: $path"
    }
}

foreach ($name in $assetFiles) {
    Copy-Item -LiteralPath (Join-Path $distAssets $name) -Destination (Join-Path $bundlePath $name) -Force
}

$dll2024 = Join-Path $bundlePath "Contents\2024\AcadDwgBrowser.dll"
$dll2025 = Join-Path $bundlePath "Contents\2025\AcadDwgBrowser.dll"
$missing = @()
if (-not (Test-Path -LiteralPath $dll2024)) { $missing += $dll2024 }
if (-not (Test-Path -LiteralPath $dll2025)) { $missing += $dll2025 }
if ($missing.Count -gt 0) {
    $msg = "Bundle DLLs missing:`n$($missing -join "`n")"
    if ($SkipDllCheck) {
        Write-Warning $msg
    } else {
        throw @"
$msg
Run Setup.bat or .\scripts\build.ps1 first, or pass -SkipDllCheck.
"@
    }
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$xml = Get-Content -LiteralPath $packageXml -Raw
    $Version = $xml.ApplicationPackage.AppVersion
    if ([string]::IsNullOrWhiteSpace($Version)) { $Version = "0.0.0" }
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $root "dist"
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$zipName = "AcadDwgBrowser-$Version.zip"
$zipPath = Join-Path $OutputDir $zipName
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }

$stageRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("AcadDwgBrowser-pack-" + [Guid]::NewGuid().ToString("N"))
$payloadName = "AcadDwgBrowser-$Version"
$payloadRoot = Join-Path $stageRoot $payloadName
$stageBundle = Join-Path $payloadRoot $bundleName

try {
    New-Item -ItemType Directory -Force -Path $payloadRoot | Out-Null
    Copy-Item -LiteralPath $bundlePath -Destination $stageBundle -Recurse -Force

    foreach ($name in $assetFiles) {
        $src = Join-Path $distAssets $name
        Copy-Item -LiteralPath $src -Destination (Join-Path $payloadRoot $name) -Force
        Copy-Item -LiteralPath $src -Destination (Join-Path $stageBundle $name) -Force
    }

    Get-ChildItem -LiteralPath $stageBundle -Recurse -Force -File | Where-Object {
        $name = $_.Name
        if ($name -in $assetFiles) { return $false }
        if ($name -eq ".gitkeep") { return $true }
        if ($name -eq "PackageContents.xml" -or $name -eq "config.json") { return $false }
        if ($_.Extension -eq ".pdb") { return $true }
        if ($name.EndsWith(".deps.json", [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
        if ($_.Extension -eq ".xml") { return $true }
        return $false
    } | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }

    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $payloadRoot, $zipPath,
        [System.IO.Compression.CompressionLevel]::Optimal, $true)

    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $names = @($zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        if (-not ($names | Where-Object { $_.EndsWith("/Install.bat") -or $_ -eq "Install.bat" })) {
            throw "Packaging failed: Install.bat missing"
        }
    }
    finally { $zip.Dispose() }
}
finally {
    if (Test-Path -LiteralPath $stageRoot) {
        Remove-Item -LiteralPath $stageRoot -Recurse -Force
    }
}

Write-Host "Packed: $zipPath"
Write-Host "For end users who already have DLLs in the zip: unzip → Install.bat"
Write-Host "For source install on a PC with AutoCAD tooling: run Setup.bat instead"
