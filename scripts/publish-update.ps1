#Requires -Version 5.1
param(
    [Parameter(Mandatory = $true)]
    [string]$SharePath,

    [string]$Version = "",

    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$pack = Join-Path $PSScriptRoot "pack.ps1"
$xmlPath = Join-Path $root "bundle\AcadDwgBrowser.bundle\PackageContents.xml"

if (-not (Test-Path -LiteralPath $SharePath)) {
    New-Item -ItemType Directory -Force -Path $SharePath | Out-Null
}

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    [xml]$xml = Get-Content -LiteralPath $xmlPath -Raw
    $xml.ApplicationPackage.AppVersion = $Version
    $xml.ApplicationPackage.FriendlyVersion = $Version
    $xml.Save($xmlPath)
    Write-Host "PackageContents.xml AppVersion=$Version"
}

$packArgs = @()
if ($SkipBuild) { $packArgs += "-SkipBuild" }
if (-not [string]::IsNullOrWhiteSpace($Version)) { $packArgs += "-Version"; $packArgs += $Version }

& $pack @packArgs

$zip = Get-ChildItem (Join-Path $root "dist\AcadDwgBrowser-*.zip") |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if (-not $zip) { throw "ZIP not found in dist\" }

# Read version from package if not passed
if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$xml = Get-Content -LiteralPath $xmlPath -Raw
    $Version = $xml.ApplicationPackage.AppVersion
}

$zipName = "AcadDwgBrowser-$Version.zip"
$destZip = Join-Path $SharePath $zipName
Copy-Item -LiteralPath $zip.FullName -Destination $destZip -Force

$manifest = @{
    version    = $Version
    packageUrl = $destZip
    notes      = "Published $(Get-Date -Format o)"
} | ConvertTo-Json

$manifestPath = Join-Path $SharePath "manifest.json"
Set-Content -LiteralPath $manifestPath -Value $manifest -Encoding UTF8

Write-Host ""
Write-Host "Published:"
Write-Host "  ZIP:      $destZip"
Write-Host "  Manifest: $manifestPath"
Write-Host ""
Write-Host "On each PC set in config.json:"
Write-Host "  `"UpdateManifestUrl`": `"$manifestPath`""
Write-Host ""
Write-Host "Users only need to restart AutoCAD to get the update."
