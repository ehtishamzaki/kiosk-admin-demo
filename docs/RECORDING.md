# Recording the demo

Target length three minutes. Record with Xbox Game Bar (Win+G) or OBS at 1080p on a Windows 11 VM that has a passed-through or USB Wi-Fi adapter. Use Windows 11 Enterprise or IoT Enterprise evaluation media for the lockdown part; Shell Launcher and Keyboard Filter are not on Pro.

## Set up before recording

1. `dotnet publish src\KioskAdmin -c Release -r win-x64 --self-contained false -o "C:\Program Files\KioskAdmin"`
2. Run the app once as yourself so `C:\ProgramData\KioskAdmin\config.json` exists, then set `"KioskMode": true` in it.
3. Install a PDF printer (Microsoft Print to PDF is enough) and, if you have one, a real network printer.
4. As administrator in `lockdown\`: `01-Create-Accounts.ps1`, `02-Configure-ShellLauncher.ps1`, `03-Configure-KeyboardFilter.ps1` (reboot, run again), `04-Apply-AppLocker.ps1`, `05-Configure-Firewall.ps1`, then `06-Verify.ps1` and keep that output on screen for the recording.
5. Reboot and sign in as `kiosk`.

## Shot list

1. Boot straight into the app, no desktop, no taskbar. Press the Windows key and Alt+Tab to show nothing happens. (20 s)
2. Enter a wrong PIN, then the right one. Point out the lockout counter in the Log tab. (20 s)
3. Wi-Fi tab: Scan, pick a network, enter a wrong password, show "Wrong password", enter the right one, show the header switch to the SSID. Disconnect and reconnect. (50 s)
4. Printers tab: Refresh, show status text on each queue, choose one with "Use for reports", add a network printer by UNC path. (30 s)
5. Print report tab: New sample report, Print, show the job tracking message and the printed PDF or page. Pull the USB cable or set the printer offline and print again to show the failure path. (40 s)
6. Lock the panel. Switch to the `06-Verify.ps1` output and the AppLocker audit events. (20 s)
7. Sign out, sign in as `svc-maint`, show the normal desktop appears and the recovery doc. (20 s)

Say what you are doing in one sentence per shot. Do not narrate the code.
