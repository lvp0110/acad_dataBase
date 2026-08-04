$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$log = Join-Path $here "install-log.txt"
function Log($m) { Add-Content -LiteralPath $log -Value $m; Write-Host $m }

"" | Set-Content -LiteralPath $log
Log "AcadDwgBrowser PowerShell install"

$bundle = $null
$c1 = Join-Path $here "AcadDwgBrowser.bundle"
$c2 = Join-Path $here "bundle\AcadDwgBrowser.bundle"
if (Test-Path (Join-Path $c1 "PackageContents.xml")) { $bundle = $c1 }
elseif (Test-Path (Join-Path $c2 "PackageContents.xml")) { $bundle = $c2 }
else { throw "AcadDwgBrowser.bundle not found" }

$dlls = Get-ChildItem -LiteralPath $bundle -Recurse -Filter "AcadDwgBrowser.dll" -ErrorAction SilentlyContinue
if (-not $dlls) { throw "No AcadDwgBrowser.dll in bundle" }

$targetRoot = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins"
$target = Join-Path $targetRoot "AcadDwgBrowser.bundle"
New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null
if (Test-Path $target) { Remove-Item $target -Recurse -Force }
Copy-Item $bundle $target -Recurse -Force

$ok = Join-Path $here "INSTALL-OK.txt"
@(
  "INSTALL OK",
  $target,
  (Get-ChildItem $target -Recurse -Filter "AcadDwgBrowser.dll" | ForEach-Object FullName)
) | Set-Content -LiteralPath $ok

Log "OK: $target"
Log "Wrote $ok"
