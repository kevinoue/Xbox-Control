<#
.SYNOPSIS
  Builds the installer: artifacts\XboxControl-<version>.msi

.DESCRIPTION
  1. Builds the SolidWorks add-in (Release, fixed version)
  2. Publishes the settings app as a single self-contained exe (no .NET install needed)
  3. Builds the MSI with WiX (restored from NuGet)

  Needs the .NET 10 SDK and SolidWorks installed (for the API reference assemblies).
#>
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifacts = Join-Path $repo 'artifacts'
$settingsOut = Join-Path $artifacts 'settings'

function Run($exe, [string[]]$arguments) {
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "$exe failed with exit code $LASTEXITCODE" }
}

Remove-Item $settingsOut -Recurse -Force -ErrorAction SilentlyContinue

Write-Host '== Add-in' -ForegroundColor Cyan
Run dotnet @('build', (Join-Path $repo 'Xbox Control.Addin'), '-c', 'Release', '-nologo', '-v', 'q')

Write-Host '== Settings app' -ForegroundColor Cyan
Run dotnet @('publish', (Join-Path $repo 'Xbox Control.Settings'), '-c', 'Release', '-r', 'win-x64', '--self-contained',
    '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:EnableCompressionInSingleFile=true',
    '-p:DebugType=none', '-o', $settingsOut, '-nologo', '-v', 'q')

Write-Host '== Installer' -ForegroundColor Cyan
Run dotnet @('build', (Join-Path $repo 'installer\XboxControl.Installer.wixproj'), '-c', 'Release', '-nologo', '-v', 'q',
    "-p:SettingsDir=$settingsOut", "-o:$artifacts")

Get-ChildItem $artifacts -Filter *.msi | ForEach-Object { "{0}  ({1:N1} MB)" -f $_.FullName, ($_.Length / 1MB) }
