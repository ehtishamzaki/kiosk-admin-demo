#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [string]$KioskUser = "kiosk",
    [string]$ShellPath = "C:\Program Files\KioskAdmin\KioskAdmin.exe"
)

$namespace = "root\standardcimv2\embedded"
$results = @()

function Add-Check([string]$name, [bool]$pass, [string]$detail) {
    $script:results += [pscustomobject]@{ Check = $name; Result = $(if ($pass) { "PASS" } else { "FAIL" }); Detail = $detail }
}

$edition = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion").EditionID
Add-Check "Windows edition supports lockdown features" ($edition -match "Enterprise|IoTEnterprise") $edition

$kiosk = Get-LocalUser -Name $KioskUser -ErrorAction SilentlyContinue
Add-Check "Kiosk account exists" ($null -ne $kiosk) $KioskUser
if ($kiosk) {
    $isAdmin = (Get-LocalGroupMember -Group Administrators -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "*\$KioskUser" }) -ne $null
    Add-Check "Kiosk account is not an administrator" (-not $isAdmin) ""
}

Add-Check "Shell executable present" (Test-Path $ShellPath) $ShellPath

try {
    $sl = [wmiclass]"\\localhost\${namespace}:WESL_UserSetting"
    $enabled = $sl.IsEnabled().Enabled
    Add-Check "Shell Launcher enabled" $enabled ""
    if ($kiosk) {
        $sid = (New-Object System.Security.Principal.NTAccount($KioskUser)).Translate([System.Security.Principal.SecurityIdentifier]).Value
        $custom = $sl.GetCustomShell($sid)
        Add-Check "Kiosk user mapped to app shell" ($custom.Shell -eq $ShellPath) $custom.Shell
    }
} catch {
    Add-Check "Shell Launcher enabled" $false "feature not available: $($_.Exception.Message)"
}

try {
    $keys = Get-WmiObject -Namespace $namespace -Class WEKF_PredefinedKey -ErrorAction Stop
    $blocked = ($keys | Where-Object { $_.Enabled }).Id
    Add-Check "Keyboard Filter blocks Win key" ($blocked -contains "Win") ($blocked -join ", ")
    Add-Check "Keyboard Filter blocks Ctrl+Alt+Del" ($blocked -contains "Ctrl+Alt+Del") ""
} catch {
    Add-Check "Keyboard Filter configured" $false "feature not available"
}

$appId = Get-Service AppIDSvc -ErrorAction SilentlyContinue
Add-Check "AppLocker service running" ($appId -and $appId.Status -eq "Running") $(if ($appId) { $appId.Status } else { "missing" })
$policy = Get-AppLockerPolicy -Effective -ErrorAction SilentlyContinue
$exe = $policy.RuleCollections | Where-Object { $_.RuleCollectionType -eq "Exe" }
Add-Check "AppLocker exe rules present" ($exe -and $exe.Count -gt 0) $(if ($exe) { "mode $($exe.EnforcementMode)" } else { "" })

$profiles = Get-NetFirewallProfile
Add-Check "Firewall inbound default is Block on all profiles" (($profiles | Where-Object { $_.DefaultInboundAction -ne "Block" }).Count -eq 0) (($profiles | ForEach-Object { "$($_.Name)=$($_.DefaultInboundAction)" }) -join " ")

$rdp = (Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\Terminal Server").fDenyTSConnections
Add-Check "Remote Desktop disabled" ($rdp -eq 1) "fDenyTSConnections=$rdp"

$uwf = Get-WindowsOptionalFeature -Online -FeatureName Client-UnifiedWriteFilter -ErrorAction SilentlyContinue
Add-Check "Unified Write Filter feature state (optional)" $true $(if ($uwf) { $uwf.State } else { "not available" })

$results | Format-Table -AutoSize
$failed = ($results | Where-Object { $_.Result -eq "FAIL" }).Count
Write-Host ""
Write-Host ("{0} checks, {1} failed" -f $results.Count, $failed)
exit $failed
