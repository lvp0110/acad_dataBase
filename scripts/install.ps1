#Requires -Version 5.1
<#
.SYNOPSIS
  Installs the repo-built bundle (developer helper).
.DESCRIPTION
  For end-user ZIPs use dist-assets\Install.bat after unpacking.
#>
param(
    [ValidateSet("User", "AllUsers")]
    [string]$Scope = "User"
)

$ErrorActionPreference = "Stop"
& (Join-Path $PSScriptRoot "dist-assets\Install.ps1") -Scope $Scope
