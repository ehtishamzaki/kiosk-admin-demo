#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [string[]]$MaintenanceSubnets = @("10.0.0.0/8", "192.168.0.0/16"),
    [switch]$AllowRdpFromMaintenance,
    [switch]$Remove
)

$ErrorActionPreference = "Stop"
$prefix = "KioskAdmin"

if ($Remove) {
    Get-NetFirewallRule -DisplayName "$prefix *" -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    Set-NetFirewallProfile -Profile Domain,Private,Public -DefaultInboundAction NotConfigured -DefaultOutboundAction NotConfigured
    Write-Host "Firewall rules removed and defaults restored"
    return
}

Set-NetFirewallProfile -Profile Domain,Private,Public -Enabled True -DefaultInboundAction Block -DefaultOutboundAction Allow -NotifyOnListen False -LogBlocked True -LogMaxSizeKilobytes 8192

$noisyGroups = @(
    "File and Printer Sharing",
    "Network Discovery",
    "Remote Desktop",
    "Remote Assistance",
    "Windows Remote Management",
    "Cast to Device functionality",
    "Wireless Display",
    "Delivery Optimization",
    "mDNS"
)
foreach ($group in $noisyGroups) {
    Get-NetFirewallRule -DisplayGroup $group -Direction Inbound -ErrorAction SilentlyContinue | Disable-NetFirewallRule
}

Get-NetFirewallRule -DisplayName "$prefix *" -ErrorAction SilentlyContinue | Remove-NetFirewallRule

New-NetFirewallRule -DisplayName "$prefix core networking in" -Group $prefix -Direction Inbound -Protocol ICMPv4 -IcmpType 8 -RemoteAddress $MaintenanceSubnets -Action Allow | Out-Null

if ($AllowRdpFromMaintenance) {
    New-NetFirewallRule -DisplayName "$prefix maintenance RDP" -Group $prefix -Direction Inbound -Protocol TCP -LocalPort 3389 -RemoteAddress $MaintenanceSubnets -Action Allow | Out-Null
    Write-Host "RDP allowed inbound from $($MaintenanceSubnets -join ', ') only"
}

$servicesToDisable = @("RemoteRegistry", "SSDPSRV", "upnphost", "XblAuthManager", "XblGameSave", "XboxNetApiSvc", "WMPNetworkSvc", "RetailDemo", "MapsBroker")
foreach ($svc in $servicesToDisable) {
    $service = Get-Service -Name $svc -ErrorAction SilentlyContinue
    if ($service) {
        Stop-Service -Name $svc -Force -ErrorAction SilentlyContinue
        Set-Service -Name $svc -StartupType Disabled
        Write-Host ("{0,-18} disabled" -f $svc)
    }
}

Write-Host ""
Write-Host "Inbound default: BLOCK on all profiles. Outbound default: ALLOW."
Write-Host "Printing to network printers is outbound (TCP 9100 / 515 / 631) and keeps working."
Write-Host "Add an explicit outbound allow-list later once the device's endpoints are confirmed."
