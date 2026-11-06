#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [string]$KioskUser = "kiosk",
    [string]$MaintenanceUser = "svc-maint",
    [switch]$ConfigureAutoLogon
)

$ErrorActionPreference = "Stop"

function Read-Secret($label) {
    $secure = Read-Host -Prompt $label -AsSecureString
    return $secure
}

if (-not (Get-LocalUser -Name $KioskUser -ErrorAction SilentlyContinue)) {
    $kioskPassword = Read-Secret "Password for $KioskUser"
    New-LocalUser -Name $KioskUser -Password $kioskPassword -PasswordNeverExpires -UserMayNotChangePassword -AccountNeverExpires -Description "Kiosk shell account" | Out-Null
    Add-LocalGroupMember -Group "Users" -Member $KioskUser
    Write-Host "Created standard user $KioskUser"
} else {
    Write-Host "$KioskUser already exists"
}

if (-not (Get-LocalUser -Name $MaintenanceUser -ErrorAction SilentlyContinue)) {
    $maintPassword = Read-Secret "Password for $MaintenanceUser"
    New-LocalUser -Name $MaintenanceUser -Password $maintPassword -PasswordNeverExpires -AccountNeverExpires -Description "Servicing and recovery account" | Out-Null
    Add-LocalGroupMember -Group "Administrators" -Member $MaintenanceUser
    Write-Host "Created administrator $MaintenanceUser"
} else {
    Write-Host "$MaintenanceUser already exists"
}

Disable-LocalUser -Name "Guest" -ErrorAction SilentlyContinue

if ($ConfigureAutoLogon) {
    Write-Host ""
    Write-Host "Auto logon for $KioskUser must be configured with Sysinternals Autologon so the password is stored as an LSA secret, not plain text:"
    Write-Host "    Autologon.exe $KioskUser $env:COMPUTERNAME <password>"
    Write-Host "Download: https://learn.microsoft.com/sysinternals/downloads/autologon"
}

Write-Host ""
Write-Host "Next: 02-Configure-ShellLauncher.ps1 -KioskUser $KioskUser"
