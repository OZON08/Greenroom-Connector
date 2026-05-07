# Builds the Greenroom Connector MSI for both languages back-to-back.
# Outputs land in src\GreenroomConnector.Installer\bin\$Configuration\:
#   GreenroomConnector-de-DE.msi
#   GreenroomConnector-en-US.msi
#
# Usage (from a normal PowerShell, repo root):
#   .\scripts\Build-Installer.ps1
#   .\scripts\Build-Installer.ps1 -Configuration Debug
#   .\scripts\Build-Installer.ps1 -Cultures de-DE          # only the German MSI
#
# The wixproj's default Cultures is de-DE; -Cultures here drives msbuild's
# /p:Cultures=... so the matching localization (loc\<culture>.wxl) is bound.

[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('de-DE','en-US')]
    [string[]]$Cultures = @('de-DE','en-US')
)

$ErrorActionPreference = 'Stop'
$proj = Join-Path $PSScriptRoot '..\src\GreenroomConnector.Installer\GreenroomConnector.Installer.wixproj'

foreach ($c in $Cultures) {
    Write-Host "=== Building $c ($Configuration) ===" -ForegroundColor Cyan
    & dotnet build $proj -c $Configuration "/p:Cultures=$c"
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $c (exit $LASTEXITCODE)" }
}

Write-Host "Done." -ForegroundColor Green
