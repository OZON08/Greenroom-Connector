# Builds the Greenroom Connector MSI for both languages back-to-back.
# Outputs land in culture-specific folders:
#   src\GreenroomConnector.Installer\bin\$Configuration\de-DE\GreenroomConnector-de-DE.msi
#   src\GreenroomConnector.Installer\bin\$Configuration\en-US\GreenroomConnector-en-US.msi
#
# Usage (from a normal PowerShell, repo root):
#   .\scripts\Build-Installer.ps1
#   .\scripts\Build-Installer.ps1 -Configuration Debug
#   .\scripts\Build-Installer.ps1 -Cultures de-DE          # only the German MSI
#   .\scripts\Build-Installer.ps1 -CertificateThumbprint ABC123...
#   .\scripts\Build-Installer.ps1 -SignAuthenticode         # sign DLL + MSI too
#
# The wixproj's default Cultures is de-DE; -Cultures here drives MSBuild's
# /p:Cultures=... so the matching localization (<culture>.wxl) is bound.
# VSTO projects require Visual Studio MSBuild because the OfficeTools targets
# are not shipped in the .NET SDK used by dotnet build.

[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('de-DE','en-US')]
    [string[]]$Cultures = @('de-DE','en-US'),

    [Alias('SigningThumbprint')]
    [string]$CertificateThumbprint = $env:GREENROOM_SIGNING_CERTIFICATE_THUMBPRINT,

    [switch]$SignAuthenticode,

    [string]$TimestampUrl = ''
)

$ErrorActionPreference = 'Stop'
$proj = Join-Path $PSScriptRoot '..\src\GreenroomConnector.Installer\GreenroomConnector.Installer.wixproj'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$installerOutputRoot = Join-Path $repoRoot "src\GreenroomConnector.Installer\bin\$Configuration"
$stagingRoot = Join-Path ([System.IO.Path]::GetTempPath()) "GreenroomConnectorInstaller-$([guid]::NewGuid())"
$stagedOutputs = New-Object System.Collections.Generic.List[object]

function Resolve-MSBuildPath {
    if ($env:MSBUILD_EXE -and (Test-Path -LiteralPath $env:MSBUILD_EXE)) {
        return $env:MSBUILD_EXE
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if ($msbuild -and (Test-Path -LiteralPath $msbuild)) {
            return $msbuild
        }
    }

    $command = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    throw 'MSBuild.exe was not found. Install Visual Studio 2022 with the .NET desktop development and Office/SharePoint development workloads, or set MSBUILD_EXE to a full MSBuild.exe path.'
}

function Resolve-SignToolPath {
    if ($env:SIGNTOOL_EXE -and (Test-Path -LiteralPath $env:SIGNTOOL_EXE)) {
        return $env:SIGNTOOL_EXE
    }

    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $kitRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (Test-Path -LiteralPath $kitRoot) {
        $signTool = Get-ChildItem -LiteralPath $kitRoot -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1

        if ($signTool) {
            return $signTool.FullName
        }
    }

    throw 'signtool.exe was not found. Install the Windows SDK, or set SIGNTOOL_EXE to a full signtool.exe path.'
}

function Get-LocalSigningThumbprint {
    $localProps = Join-Path $PSScriptRoot '..\Directory.Build.local.props'
    if (-not (Test-Path -LiteralPath $localProps)) {
        return ''
    }

    [xml]$props = Get-Content -LiteralPath $localProps -Raw
    return [string]$props.Project.PropertyGroup.GrcSigningCertificateThumbprint
}

function Invoke-AuthenticodeSign {
    param(
        [string]$SignTool,
        [string]$Thumbprint,
        [string]$Path,
        [string]$Timestamp
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Cannot sign missing file: $Path"
    }

    if ($Timestamp) {
        & $SignTool sign /fd SHA256 /sha1 $Thumbprint /tr $Timestamp /td SHA256 $Path
    } else {
        & $SignTool sign /fd SHA256 /sha1 $Thumbprint $Path
    }

    if ($LASTEXITCODE -ne 0) { throw "Signing failed for $Path (exit $LASTEXITCODE)" }
}

try {
    New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

    $msbuild = Resolve-MSBuildPath
    $env:NUGET_PACKAGES = if ($env:NUGET_PACKAGES) {
        $env:NUGET_PACKAGES
    } else {
        Join-Path $env:USERPROFILE '.nuget\packages'
    }

    Write-Host "Using MSBuild: $msbuild" -ForegroundColor DarkGray
    Write-Host "Using NuGet package cache: $env:NUGET_PACKAGES" -ForegroundColor DarkGray

    if (-not $CertificateThumbprint) {
        $CertificateThumbprint = $env:GREENROOM_SIGNING_THUMBPRINT
    }

    if (-not $CertificateThumbprint) {
        $CertificateThumbprint = Get-LocalSigningThumbprint
    }

    if ($CertificateThumbprint) {
        Write-Host "Using certificate thumbprint: $CertificateThumbprint" -ForegroundColor DarkGray
    }

    if ($CertificateThumbprint -and -not $SignAuthenticode) {
        Write-Warning 'Authenticode signing is disabled. Outlook may show the COM add-in publisher as <None>; use -SignAuthenticode for installable MSI packages.'
    }

    $signTool = ''
    if ($SignAuthenticode) {
        if (-not $CertificateThumbprint) {
            throw 'Certificate thumbprint is required. Run scripts\New-SigningCertificate.ps1 or pass -CertificateThumbprint.'
        }

        $signTool = Resolve-SignToolPath
        Write-Host "Using signtool: $signTool" -ForegroundColor DarkGray
    }

    foreach ($c in $Cultures) {
        Write-Host "=== Building $c ($Configuration) ===" -ForegroundColor Cyan
        $msbuildArgs = @(
            $proj,
            '/restore',
            '/m',
            "/p:Configuration=$Configuration",
            '/p:Platform=AnyCPU',
            "/p:Cultures=$c"
        )

        if ($CertificateThumbprint) {
            $msbuildArgs += "/p:GrcSigningCertificateThumbprint=$CertificateThumbprint"
        }

        if ($SignAuthenticode) {
            $msbuildArgs += '/p:GrcAuthenticodeSign=true'
            $msbuildArgs += "/p:GrcSignToolPath=$signTool"
            if ($TimestampUrl) {
                $msbuildArgs += "/p:GrcTimestampUrl=$TimestampUrl"
            }
        }

        & $msbuild @msbuildArgs
        if ($LASTEXITCODE -ne 0) { throw "Build failed for $c (exit $LASTEXITCODE)" }

        $cultureOutput = Join-Path $installerOutputRoot $c
        $msi = Join-Path $cultureOutput "GreenroomConnector-$c.msi"
        $wixpdb = Join-Path $cultureOutput "GreenroomConnector-$c.wixpdb"

        if ($SignAuthenticode) {
            Invoke-AuthenticodeSign -SignTool $signTool -Thumbprint $CertificateThumbprint -Path $msi -Timestamp $TimestampUrl
        } elseif (-not (Test-Path -LiteralPath $msi)) {
            throw "Build completed but MSI was not found: $msi"
        }

        $stageCultureOutput = Join-Path $stagingRoot $c
        New-Item -ItemType Directory -Path $stageCultureOutput -Force | Out-Null
        $stagedMsi = Join-Path $stageCultureOutput "GreenroomConnector-$c.msi"
        $stagedWixpdb = Join-Path $stageCultureOutput "GreenroomConnector-$c.wixpdb"

        Copy-Item -LiteralPath $msi -Destination $stagedMsi -Force
        if (Test-Path -LiteralPath $wixpdb) {
            Copy-Item -LiteralPath $wixpdb -Destination $stagedWixpdb -Force
        }

        $stagedOutputs.Add([pscustomobject]@{
            Culture = $c
            OutputDirectory = $cultureOutput
            Msi = $stagedMsi
            Wixpdb = $stagedWixpdb
        })
    }
} finally {
    foreach ($output in $stagedOutputs) {
        New-Item -ItemType Directory -Path $output.OutputDirectory -Force | Out-Null
        Copy-Item -LiteralPath $output.Msi -Destination $output.OutputDirectory -Force
        if (Test-Path -LiteralPath $output.Wixpdb) {
            Copy-Item -LiteralPath $output.Wixpdb -Destination $output.OutputDirectory -Force
        }
    }

    if (Test-Path -LiteralPath $stagingRoot) {
        $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        $stagingFullPath = [System.IO.Path]::GetFullPath($stagingRoot)
        if ($stagingFullPath.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $stagingFullPath -Recurse -Force
        }
    }
}

Write-Host "Done." -ForegroundColor Green
