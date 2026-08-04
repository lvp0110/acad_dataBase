#Requires -Version 5.1
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

Write-Host "Building AutoCAD 2024 (net48, NuGet AutoCAD.NET)..."
dotnet build (Join-Path $root "src\AcadDwgBrowser.Plugin.Acad2024\AcadDwgBrowser.Plugin.Acad2024.csproj") -c $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Building AutoCAD 2025/2026 (net8, NuGet AutoCAD.NET)..."
dotnet build (Join-Path $root "src\AcadDwgBrowser.Plugin.Acad2025\AcadDwgBrowser.Plugin.Acad2025.csproj") -c $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Done. Bundle: $(Join-Path $root 'bundle\AcadDwgBrowser.bundle')"
