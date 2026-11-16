<#
.SYNOPSIS
  Registers the Xbox Control SolidWorks add-in for the current user (no administrator rights).

.DESCRIPTION
  Copies the built add-in to %LOCALAPPDATA%\XboxControl\Addin\<version>\ (so SolidWorks never
  locks the build output), registers its COM class under HKCU\Software\Classes, and adds the
  SolidWorks add-in keys. Run it again after every build; each build has a new version.

  If SolidWorks runs as administrator it ignores per-user COM registrations. In that case run
  this script from an elevated PowerShell, which registers for all users instead.

.EXAMPLE
  .\tools\register-addin.ps1                 # register the Debug build
  .\tools\register-addin.ps1 -Unregister     # remove the registration
#>
param(
    [string]$Dll = (Join-Path $PSScriptRoot '..\Xbox Control.Addin\bin\Debug\net48\XboxControlAddin.dll'),
    [switch]$Unregister
)
$ErrorActionPreference = 'Stop'

$clsid  = '{682711DD-EBC4-4666-B8A9-33FCF8AB5C09}'
$progId = 'XboxControl.SolidWorksAddin'
$class  = 'XboxControl.Addin.SwAddin'
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
$classes  = if ($isAdmin) { 'HKLM:\SOFTWARE\Classes' } else { 'HKCU:\Software\Classes' }
$swAddins = if ($isAdmin) { 'HKLM:\SOFTWARE\SolidWorks\Addins' } else { 'HKCU:\Software\SolidWorks\Addins' }
$startup  = 'HKCU:\Software\SolidWorks\AddInsStartup'

function Remove-Key($path) { if (Test-Path $path) { Remove-Item $path -Recurse -Force } }

if ($Unregister) {
    Remove-Key "$classes\CLSID\$clsid"
    Remove-Key "$classes\$progId"
    Remove-Key "$swAddins\$clsid"
    Remove-Key "$startup\$clsid"
    "Unregistered the Xbox Control add-in."
    return
}

$Dll = (Resolve-Path $Dll).Path
$name = [Reflection.AssemblyName]::GetAssemblyName($Dll)
$dest = Join-Path $env:LOCALAPPDATA "XboxControl\Addin\$($name.Version)"
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item (Join-Path (Split-Path $Dll) '*') $dest -Force
$deployed = Join-Path $dest (Split-Path $Dll -Leaf)
$codeBase = 'file:///' + $deployed.Replace([char]92, [char]47)

# COM class: the same keys regasm /codebase writes.
$key = New-Item "$classes\CLSID\$clsid" -Force
Set-ItemProperty $key.PSPath '(default)' $class
foreach ($sub in @('InprocServer32', "InprocServer32\$($name.Version)")) {
    $k = New-Item "$classes\CLSID\$clsid\$sub" -Force
    if ($sub -eq 'InprocServer32') {
        Set-ItemProperty $k.PSPath '(default)' 'mscoree.dll'
        Set-ItemProperty $k.PSPath 'ThreadingModel' 'Both'
    }
    Set-ItemProperty $k.PSPath 'Class' $class
    Set-ItemProperty $k.PSPath 'Assembly' $name.FullName
    Set-ItemProperty $k.PSPath 'RuntimeVersion' 'v4.0.30319'
    Set-ItemProperty $k.PSPath 'CodeBase' $codeBase
}
Set-ItemProperty (New-Item "$classes\CLSID\$clsid\ProgId" -Force).PSPath '(default)' $progId
New-Item "$classes\CLSID\$clsid\Implemented Categories\{62C8FE65-4EBB-45E7-B440-6E39B2CDBF29}" -Force | Out-Null
Set-ItemProperty (New-Item "$classes\$progId" -Force).PSPath '(default)' $class
Set-ItemProperty (New-Item "$classes\$progId\CLSID" -Force).PSPath '(default)' $clsid

# SolidWorks add-in list, and load at startup.
$k = New-Item "$swAddins\$clsid" -Force
New-ItemProperty $k.PSPath '(default)' -Value 0 -PropertyType DWord -Force | Out-Null
Set-ItemProperty $k.PSPath 'Description' 'Move around models with an Xbox controller'
Set-ItemProperty $k.PSPath 'Title' 'Xbox Control'
$k = New-Item "$startup\$clsid" -Force
New-ItemProperty $k.PSPath '(default)' -Value 1 -PropertyType DWord -Force | Out-Null

$scope = if ($isAdmin) { 'all users' } else { 'the current user' }
"Registered Xbox Control add-in $($name.Version) for $scope"
"  $deployed"
