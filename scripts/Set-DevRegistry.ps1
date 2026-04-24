# Writes the per-machine dev config that the add-in reads on startup.
# Equivalent to what the MSI will do at install time. Re-run to change values.
#
# Usage (from an elevated PowerShell):
#   .\scripts\Set-DevRegistry.ps1
#   .\scripts\Set-DevRegistry.ps1 -GreenlightUrl https://other.example.com
#   .\scripts\Set-DevRegistry.ps1 -Remove
#
# Admin rights are required because HKLM is per-machine.

[CmdletBinding()]
param(
    [string]$GreenlightUrl = 'https://meet.wald.rlp.de',
    [ValidateSet('auto','de','en')]
    [string]$Language = 'auto',
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'

$key = 'HKLM:\SOFTWARE\Greenlight\OutlookIntegration'

# Ensure we are elevated.
$principal = New-Object System.Security.Principal.WindowsPrincipal([System.Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'This script must be run from an elevated PowerShell session (HKLM is per-machine).'
}

if ($Remove) {
    if (Test-Path $key) {
        Remove-Item -Path $key -Recurse -Force
        Write-Host "Removed $key"
    } else {
        Write-Host "Key $key does not exist. Nothing to remove."
    }
    return
}

if (-not (Test-Path $key)) {
    New-Item -Path $key -Force | Out-Null
}

Set-ItemProperty -Path $key -Name 'GreenlightUrl' -Value $GreenlightUrl -Type String
Set-ItemProperty -Path $key -Name 'Language'      -Value $Language      -Type String
Set-ItemProperty -Path $key -Name 'InstallDir'    -Value '(dev)'         -Type String

Write-Host "Wrote:"
Get-ItemProperty -Path $key | Select-Object GreenlightUrl, Language, InstallDir | Format-List
