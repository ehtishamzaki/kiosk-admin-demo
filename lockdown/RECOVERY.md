# Servicing and recovery

The device has two local accounts. `kiosk` is a standard user whose shell is the diagnostic application. `svc-maint` is a local administrator whose shell is the normal Windows desktop. Everything below assumes that split.

## Normal administration (no desktop needed)

Wi-Fi, printers and report printing are configured from inside the application. Enter the admin PIN on the lock screen. The PIN is stored as a salted PBKDF2 hash in `C:\ProgramData\KioskAdmin\config.json`; five wrong attempts lock the panel for 30 seconds. Both values are configurable in the same file.

## Getting a desktop on the device

1. Press the Keyboard Filter breakout key (Scroll Lock, five times) if Ctrl+Alt+Del is filtered, or attach a keyboard if the device has none.
2. Sign out of `kiosk` (the application exposes no sign-out; use Ctrl+Alt+Del after breakout, or restart the device and interrupt auto logon by holding Shift).
3. Sign in as `svc-maint`. Shell Launcher maps that account to `explorer.exe`, Keyboard Filter is disabled for administrators, and AppLocker allows administrators to run anything.

## Application crash or exit

Shell Launcher is configured with the "restart shell" action for `kiosk`, so if the application exits or crashes it is relaunched immediately. Repeated crashes are visible in `C:\ProgramData\KioskAdmin\logs\`. If a bad build loops, sign in as `svc-maint` and run `02-Configure-ShellLauncher.ps1 -Remove`.

## Updating the application

1. Sign in as `svc-maint`.
2. Copy the new build to `C:\Program Files\KioskAdmin\`. AppLocker allows that folder for everyone, so the path must not change.
3. If Unified Write Filter is enabled, run `uwfmgr filter disable`, reboot, update, then `uwfmgr filter enable` and reboot again.
4. Run `06-Verify.ps1` and sign back in as `kiosk`.

## Windows will not boot into the application

- Hold Shift while clicking Restart from the sign-in screen, or boot from Windows installation media, to reach Advanced startup and Safe Mode. Shell Launcher does not apply in Safe Mode.
- From Safe Mode, sign in as `svc-maint` and either repair the application folder or run `02-Configure-ShellLauncher.ps1 -Remove`.

## Rolling back the lockdown entirely

Run, in this order, as `svc-maint`:

    .\05-Configure-Firewall.ps1 -Remove
    .\04-Apply-AppLocker.ps1 -Remove
    .\03-Configure-KeyboardFilter.ps1 -Remove
    .\02-Configure-ShellLauncher.ps1 -Remove

Reboot. The device is a normal Windows installation again with the two local accounts still present.

## What is deliberately not locked down

- Outbound traffic. Network printing and any device telemetry need it. An explicit outbound allow-list is a follow-up once the endpoints are confirmed.
- Unified Write Filter. It protects the system volume from drift but complicates updates and logging; enable it only after the overlay exclusions (logs, config, data) are agreed.
- BitLocker. Recommended for a device holding diagnostic data; it is a separate decision because of recovery key custody.
