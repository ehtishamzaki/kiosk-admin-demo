using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using KioskAdmin.Models;
using KioskAdmin.Services;

namespace KioskAdmin
{
    public partial class MainWindow : Window
    {
        private readonly AppLog _log = App.Log;
        private readonly SettingsStore _store = SettingsStore.Default();
        private readonly IWifiService _wifi = new WinRtWifiService();
        private readonly PrinterService _printers = new PrinterService();
        private readonly ReportPrintService _reportPrinter = new ReportPrintService();
        private readonly ObservableCollection<WifiNetworkInfo> _networks = new ObservableCollection<WifiNetworkInfo>();
        private readonly ObservableCollection<PrinterInfo> _printerItems = new ObservableCollection<PrinterInfo>();
        private readonly ObservableCollection<string> _logLines = new ObservableCollection<string>();
        private readonly DispatcherTimer _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

        private AppSettings _settings = new AppSettings();
        private DiagnosticReport _report = new DiagnosticReport();
        private int _failedAttempts;
        private DateTime _lockoutUntil = DateTime.MinValue;
        private bool _wifiInitialised;
        private CancellationTokenSource? _trackCts;

        public MainWindow()
        {
            InitializeComponent();
            WifiList.ItemsSource = _networks;
            PrinterList.ItemsSource = _printerItems;
            LogList.ItemsSource = _logLines;
            _log.EntryWritten += OnLogEntry;
            _wifi.StatusChanged += OnWifiStatus;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _settings = _store.Load();
            if (string.IsNullOrEmpty(_settings.PinHash))
            {
                var created = PinGate.Create("1234");
                _settings.PinHash = created.Hash;
                _settings.PinSalt = created.Salt;
                _store.Save(_settings);
                _log.Warn("No admin PIN configured, default PIN written to " + _store.Path + ". Change it before deployment.");
            }

            DeviceNameText.Text = _settings.DeviceName;
            DeviceSerialText.Text = "Serial " + _settings.SerialNumber;
            FooterText.Text = "Config: " + _store.Path;
            ApplyKioskMode();

            _clock.Tick += (s, a) => ClockText.Text = DateTime.Now.ToString("ddd dd MMM HH:mm:ss");
            _clock.Start();

            NewReport();
            RefreshPrinterHeader();
            _ = RefreshNetworkHeaderAsync();
            PinBox.Focus();
            _log.Info("Main window ready");
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_settings.KioskMode && AdminPanel.Visibility != Visibility.Visible)
            {
                e.Cancel = true;
                _log.Warn("Close blocked while locked in kiosk mode");
            }
        }

        private void ApplyKioskMode()
        {
            if (!_settings.KioskMode)
            {
                return;
            }

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            Topmost = true;
        }

        private void PinBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TryUnlock();
            }
        }

        private void UnlockButton_Click(object sender, RoutedEventArgs e)
        {
            TryUnlock();
        }

        private void TryUnlock()
        {
            if (DateTime.Now < _lockoutUntil)
            {
                var wait = (int)Math.Ceiling((_lockoutUntil - DateTime.Now).TotalSeconds);
                PinMessage.Text = "Too many attempts. Try again in " + wait + " seconds.";
                return;
            }

            var pin = PinBox.Password;
            PinBox.Clear();
            if (PinGate.Verify(pin, _settings.PinHash, _settings.PinSalt))
            {
                _failedAttempts = 0;
                PinMessage.Text = string.Empty;
                LockPanel.Visibility = Visibility.Collapsed;
                AdminPanel.Visibility = Visibility.Visible;
                _log.Info("Administrator unlocked the device panel");
                RefreshPrinters();
                _ = EnsureWifiAsync();
                return;
            }

            _failedAttempts++;
            _log.Warn("Failed PIN attempt " + _failedAttempts);
            if (_failedAttempts >= _settings.MaxPinAttempts)
            {
                _failedAttempts = 0;
                _lockoutUntil = DateTime.Now.AddSeconds(_settings.LockoutSeconds);
                PinMessage.Text = "Too many attempts. Locked for " + _settings.LockoutSeconds + " seconds.";
                _log.Warn("PIN entry locked out until " + _lockoutUntil.ToString("HH:mm:ss"));
                return;
            }

            PinMessage.Text = "Incorrect PIN.";
        }

        private void LockButton_Click(object sender, RoutedEventArgs e)
        {
            AdminPanel.Visibility = Visibility.Collapsed;
            LockPanel.Visibility = Visibility.Visible;
            WifiPasswordBox.Clear();
            PinBox.Focus();
            _log.Info("Device panel locked");
        }

        private async Task EnsureWifiAsync()
        {
            if (_wifiInitialised)
            {
                return;
            }

            ScanButton.IsEnabled = false;
            try
            {
                _wifiInitialised = await _wifi.InitializeAsync();
                if (_wifiInitialised)
                {
                    await ScanAsync();
                }
                else
                {
                    WifiStatusText.Text = "Wi-Fi unavailable on this device";
                }
            }
            catch (Exception ex)
            {
                _log.Error("Wi-Fi initialisation failed", ex);
                WifiStatusText.Text = "Wi-Fi error: " + ex.Message;
            }
            finally
            {
                ScanButton.IsEnabled = true;
            }
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_wifiInitialised)
            {
                await EnsureWifiAsync();
                return;
            }

            await ScanAsync();
        }

        private async Task ScanAsync()
        {
            ScanButton.IsEnabled = false;
            WifiStatusText.Text = "Scanning...";
            try
            {
                var found = await _wifi.ScanAsync();
                _networks.Clear();
                foreach (var n in found)
                {
                    _networks.Add(n);
                }

                WifiStatusText.Text = found.Count + " networks in range";
            }
            catch (Exception ex)
            {
                _log.Error("Wi-Fi scan failed", ex);
                WifiStatusText.Text = "Scan failed: " + ex.Message;
            }
            finally
            {
                ScanButton.IsEnabled = true;
                await RefreshNetworkHeaderAsync();
            }
        }

        private void WifiList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = WifiList.SelectedItem as WifiNetworkInfo;
            ConnectButton.IsEnabled = selected != null && !selected.IsConnected;
            WifiPasswordBox.IsEnabled = selected != null && !selected.IsOpen;
            WifiResultText.Text = selected == null ? string.Empty : (selected.IsOpen ? "Open network, no password needed" : selected.Security);
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (WifiList.SelectedItem is not WifiNetworkInfo selected)
            {
                return;
            }

            ConnectButton.IsEnabled = false;
            WifiResultText.Text = "Connecting to " + selected.Ssid + "...";
            try
            {
                var result = await _wifi.ConnectAsync(selected, WifiPasswordBox.Password);
                WifiResultText.Text = result.Message;
                WifiResultText.Foreground = result.Success ? (System.Windows.Media.Brush)FindResource("Accent") : (System.Windows.Media.Brush)FindResource("Danger");
                if (result.Success)
                {
                    WifiPasswordBox.Clear();
                    await ScanAsync();
                }
            }
            catch (Exception ex)
            {
                _log.Error("Wi-Fi connect failed", ex);
                WifiResultText.Text = "Connect failed: " + ex.Message;
            }
            finally
            {
                ConnectButton.IsEnabled = WifiList.SelectedItem is WifiNetworkInfo s && !s.IsConnected;
            }
        }

        private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_wifiInitialised)
            {
                return;
            }

            try
            {
                await _wifi.DisconnectAsync();
                await ScanAsync();
            }
            catch (Exception ex)
            {
                _log.Error("Wi-Fi disconnect failed", ex);
                WifiStatusText.Text = "Disconnect failed: " + ex.Message;
            }
        }

        private void OnWifiStatus(object? sender, string message)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _log.Info("Wi-Fi: " + message);
                _ = RefreshNetworkHeaderAsync();
            });
        }

        private async Task RefreshNetworkHeaderAsync()
        {
            try
            {
                var ssid = _wifi.IsReady ? await _wifi.GetConnectedSsidAsync() : null;
                NetworkStatusText.Text = string.IsNullOrEmpty(ssid) ? "Network: not connected" : "Network: " + ssid;
            }
            catch (Exception ex)
            {
                NetworkStatusText.Text = "Network: unknown";
                _log.Error("Network status read failed", ex);
            }
        }

        private void RefreshPrintersButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshPrinters();
        }

        private void RefreshPrinters()
        {
            PrinterMessageText.Text = "Reading print queues...";
            try
            {
                var found = _printers.GetPrinters(_settings.DefaultPrinter);
                _printerItems.Clear();
                foreach (var p in found)
                {
                    _printerItems.Add(p);
                }

                PrinterMessageText.Text = found.Count + " printers";
                _log.Info("Printer refresh: " + found.Count + " queues");
            }
            catch (Exception ex)
            {
                _log.Error("Printer enumeration failed", ex);
                PrinterMessageText.Text = "Could not read printers: " + ex.Message;
            }

            RefreshPrinterHeader();
        }

        private void RefreshPrinterHeader()
        {
            PrinterStatusText.Text = string.IsNullOrEmpty(_settings.DefaultPrinter) ? "Printer: none selected" : "Printer: " + _settings.DefaultPrinter;
            PrintTargetText.Text = string.IsNullOrEmpty(_settings.DefaultPrinter) ? "Select a printer on the Printers tab first" : "Prints to " + _settings.DefaultPrinter;
            PrintButton.IsEnabled = !string.IsNullOrEmpty(_settings.DefaultPrinter);
        }

        private void PrinterList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = PrinterList.SelectedItem as PrinterInfo;
            SetAppDefaultButton.IsEnabled = selected != null;
            SetWindowsDefaultButton.IsEnabled = selected != null;
        }

        private void SetAppDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            if (PrinterList.SelectedItem is not PrinterInfo selected)
            {
                return;
            }

            _settings.DefaultPrinter = selected.Name;
            _store.Save(_settings);
            _log.Info("Report printer set to " + selected.Name);
            PrinterMessageText.Text = "Reports will print to " + selected.Name;
            RefreshPrinters();
        }

        private void SetWindowsDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            if (PrinterList.SelectedItem is not PrinterInfo selected)
            {
                return;
            }

            if (_printers.SetWindowsDefault(selected.Name, out var error))
            {
                _log.Info("Windows default printer set to " + selected.Name);
                PrinterMessageText.Text = "Windows default is now " + selected.Name;
                RefreshPrinters();
            }
            else
            {
                _log.Warn("Set Windows default failed: " + error);
                PrinterMessageText.Text = "Could not set default: " + error;
            }
        }

        private void AddNetworkPrinterButton_Click(object sender, RoutedEventArgs e)
        {
            var path = NetworkPrinterBox.Text.Trim();
            if (_printers.AddNetworkPrinter(path, out var error))
            {
                _log.Info("Added network printer " + path);
                PrinterMessageText.Text = "Added " + path;
                RefreshPrinters();
            }
            else
            {
                _log.Warn("Add network printer failed for " + path + ": " + error);
                PrinterMessageText.Text = "Could not add printer: " + error;
            }
        }

        private void NewReportButton_Click(object sender, RoutedEventArgs e)
        {
            NewReport();
        }

        private void NewReport()
        {
            _report = DiagnosticReport.Sample(_settings.DeviceName, _settings.SerialNumber);
            ReportPreview.Text = string.Join(Environment.NewLine, _report.BuildLines());
            PrintResultText.Text = string.Empty;
        }

        private async void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            PrintButton.IsEnabled = false;
            PrintResultText.Text = "Sending to " + _settings.DefaultPrinter + "...";
            var submit = _reportPrinter.Submit(_settings.DefaultPrinter, _report, out var jobName);
            if (!submit.Success)
            {
                _log.Warn("Print submit failed: " + submit.Message);
                PrintResultText.Text = submit.Message;
                PrintButton.IsEnabled = true;
                return;
            }

            _log.Info(submit.Message + " as '" + jobName + "'");
            PrintResultText.Text = submit.Message + ". Waiting for the printer...";

            _trackCts?.Cancel();
            _trackCts = new CancellationTokenSource();
            try
            {
                var tracked = await _reportPrinter.TrackAsync(_settings.DefaultPrinter, jobName, TimeSpan.FromSeconds(30), _trackCts.Token);
                PrintResultText.Text = tracked.Message;
                if (tracked.Success)
                {
                    _log.Info("Print job '" + jobName + "': " + tracked.Message);
                }
                else
                {
                    _log.Warn("Print job '" + jobName + "': " + tracked.Message);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                PrintButton.IsEnabled = true;
                RefreshPrinters();
            }
        }

        private void OnLogEntry(object? sender, string line)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _logLines.Insert(0, line);
                while (_logLines.Count > 500)
                {
                    _logLines.RemoveAt(_logLines.Count - 1);
                }
            });
        }
    }
}
