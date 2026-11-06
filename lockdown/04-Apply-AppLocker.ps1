#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [string]$PolicyPath = (Join-Path $PSScriptRoot "AppLocker-Policy.xml"),
    [switch]$Enforce,
    [switch]$Remove
)

$ErrorActionPreference = "Stop"

if ($Remove) {
    $empty = [xml]'<AppLockerPolicy Version="1" />'
    $tmp = Join-Path $env:TEMP "applocker-empty.xml"
    $empty.Save($tmp)
    Set-AppLockerPolicy -XmlPolicy $tmp
    Write-Host "AppLocker policy cleared"
    return
}

Set-Service -Name AppIDSvc -StartupType Automatic
Start-Service -Name AppIDSvc

[xml]$policy = Get-Content $PolicyPath
foreach ($collection in $policy.AppLockerPolicy.RuleCollection) {
    $collection.EnforcementMode = $(if ($Enforce) { "Enabled" } else { "AuditOnly" })
}
$resolved = Join-Path $env:TEMP "applocker-resolved.xml"
$policy.Save($resolved)

Set-AppLockerPolicy -XmlPolicy $resolved
Write-Host ("AppLocker policy applied in {0} mode" -f $(if ($Enforce) { "ENFORCE" } else { "AUDIT" }))
Write-Host "Review Event Viewer > Applications and Services Logs > Microsoft > Windows > AppLocker before switching to -Enforce."
