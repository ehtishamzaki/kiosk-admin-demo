#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [switch]$Remove
)

$ErrorActionPreference = "Stop"
$namespace = "root\standardcimv2\embedded"

$blockedKeys = @(
    "Win",
    "Ctrl+Alt+Del",
    "Ctrl+Esc",
    "Alt+Tab",
    "Alt+Esc",
    "Alt+F4",
    "Alt+Space",
    "Win+L",
    "Win+R",
    "Win+E",
    "Win+D",
    "Win+I",
    "Win+X",
    "Ctrl+Shift+Esc",
    "Shift+Ctrl+Esc",
    "F1",
    "F3"
)

$feature = Get-WindowsOptionalFeature -Online -FeatureName Client-KeyboardFilter
if ($feature.State -ne "Enabled") {
    Write-Host "Enabling Keyboard Filter feature"
    Enable-WindowsOptionalFeature -Online -FeatureName Client-KeyboardFilter -NoRestart | Out-Null
    Write-Host "A reboot is required before the filter classes are available. Re-run this script afterwards."
    return
}

$predefined = Get-WmiObject -Namespace $namespace -Class WEKF_PredefinedKey
foreach ($key in $predefined) {
    if ($blockedKeys -contains $key.Id) {
        $key.Enabled = -not $Remove
        $key.Put() | Out-Null
        Write-Host ("{0,-18} {1}" -f $key.Id, $(if ($Remove) { "allowed" } else { "blocked" }))
    }
}

$settings = Get-WmiObject -Namespace $namespace -Class WEKF_Settings
foreach ($setting in $settings) {
    if ($setting.Name -eq "DisableKeyboardFilterForAdministrators") {
        $setting.Value = "true"
        $setting.Put() | Out-Null
        Write-Host "Keyboard Filter disabled for administrators (maintenance account keeps full keyboard)"
    }
    if ($setting.Name -eq "BreakoutKeyScanCode") {
        Write-Host "Breakout key scan code is $($setting.Value) (default Scroll Lock, pressed five times)"
    }
}

Write-Host ""
Write-Host "Reboot for the key filters to take effect."
