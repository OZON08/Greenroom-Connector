# Writes the per-machine dev config that the add-in reads on startup.
# Equivalent to what the MSI will do at install time. Re-run to change values.
#
# Usage (from an elevated PowerShell):
#   .\scripts\Set-DevRegistry.ps1
#   .\scripts\Set-DevRegistry.ps1 -GreenlightUrl https://other.example.com
#   .\scripts\Set-DevRegistry.ps1 -LocationText "BigBlueButton-Konferenz"
#   .\scripts\Set-DevRegistry.ps1 -ShowDialIn
#   .\scripts\Set-DevRegistry.ps1 -Remove
#
# Admin rights are required because HKLM is per-machine.

[CmdletBinding()]
param(
    [string]$GreenlightUrl = 'http://localhost:3000',
    [ValidateSet('auto','de','en')]
    [string]$Language = 'auto',
    # Text written into the appointment Location field by the add-in.
    # Empty (default) means: leave Location untouched. {room} is substituted
    # with the selected room name at insert time.
    [string]$LocationText = 'BigBlueButton-Konferenz',
    # Toggle for the localized phone dial-in block (Strings.Meeting_DialIn).
    # Wrapping text is in the resx; this switch only controls visibility.
    [switch]$ShowDialIn,
    # Deployment-specific phone number, substituted into the {number} slot
    # of Strings.Meeting_DialIn. If empty AND -ShowDialIn is set, the section
    # is suppressed (avoids printing a half-blank line).
    [string]$DialInNumber = '+49 30 1234 5678',
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'

$key = 'HKLM:\SOFTWARE\GreenroomConnector'

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

$showDialInValue = if ($ShowDialIn) { 'true' } else { 'false' }

Set-ItemProperty -Path $key -Name 'GreenlightUrl' -Value $GreenlightUrl    -Type String
Set-ItemProperty -Path $key -Name 'Language'      -Value $Language         -Type String
Set-ItemProperty -Path $key -Name 'LocationText'  -Value $LocationText     -Type String
Set-ItemProperty -Path $key -Name 'ShowDialIn'    -Value $showDialInValue  -Type String
Set-ItemProperty -Path $key -Name 'DialInNumber'  -Value $DialInNumber     -Type String
Set-ItemProperty -Path $key -Name 'InstallDir'    -Value '(dev)'           -Type String

# Drop the legacy DialInText value if a previous run wrote it.
Remove-ItemProperty -Path $key -Name 'DialInText' -Force -ErrorAction SilentlyContinue

Write-Host "Wrote:"
Get-ItemProperty -Path $key |
    Select-Object GreenlightUrl, Language, LocationText, ShowDialIn, DialInNumber, InstallDir |
    Format-List
