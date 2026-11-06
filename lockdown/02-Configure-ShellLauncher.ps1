#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [string]$KioskUser = "kiosk",
    [string]$ShellPath = "C:\Program Files\KioskAdmin\KioskAdmin.exe",
    [switch]$Remove
)

$ErrorActionPreference = "Stop"

$restartShell = 1
$restartDevice = 0
$shutdownDevice = 2
$doNothing = 3

$namespace = "root\standardcimv2\embedded"

$feature = Get-WindowsOptionalFeature -Online -FeatureName Client-EmbeddedShellLauncher
if ($feature.State -ne "Enabled") {
    Write-Host "Enabling Shell Launcher feature (Windows 10/11 IoT Enterprise or Enterprise)"
    Enable-WindowsOptionalFeature -Online -FeatureName Client-EmbeddedShellLauncher -NoRestart | Out-Null
}

function Get-AccountSid([string]$name) {
    $account = New-Object System.Security.Principal.NTAccount($name)
    return $account.Translate([System.Security.Principal.SecurityIdentifier]).Value
}

$shellLauncher = [wmiclass]"\\localhost\${namespace}:WESL_UserSetting"
$kioskSid = Get-AccountSid $KioskUser

if ($Remove) {
    try { $shellLauncher.RemoveCustomShell($kioskSid) | Out-Null } catch { }
    $shellLauncher.SetEnabled($false) | Out-Null
    Write-Host "Shell Launcher disabled and custom shell removed for $KioskUser"
    return
}

if (-not (Test-Path $ShellPath)) {
    throw "Shell executable not found at $ShellPath. Publish the app there first."
}

$shellLauncher.SetDefaultShell("explorer.exe", $restartDevice) | Out-Null

try { $shellLauncher.RemoveCustomShell($kioskSid) | Out-Null } catch { }
$shellLauncher.SetCustomShell($kioskSid, $ShellPath, $null, $null, $restartShell) | Out-Null

$shellLauncher.SetEnabled($true) | Out-Null

$enabled = $shellLauncher.IsEnabled()
Write-Host "Shell Launcher enabled: $($enabled.Enabled)"
Write-Host "$KioskUser shell: $ShellPath (restart app on exit or crash)"
Write-Host "All other users: explorer.exe"
Write-Host ""
Write-Host "Reboot and sign in as $KioskUser to verify. Sign in as the maintenance account to get the normal desktop."
