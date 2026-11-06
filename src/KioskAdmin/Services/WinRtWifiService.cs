using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KioskAdmin.Models;
using Windows.Devices.WiFi;
using Windows.Networking.Connectivity;
using Windows.Security.Credentials;

namespace KioskAdmin.Services
{
    public sealed class WinRtWifiService : IWifiService
    {
        private WiFiAdapter? _adapter;
        private bool _statusHooked;

        public event EventHandler<string>? StatusChanged;

        public bool IsReady => _adapter != null;

        public async Task<bool> InitializeAsync()
        {
            var access = await WiFiAdapter.RequestAccessAsync();
            if (access != WiFiAccessStatus.Allowed)
            {
                Raise("Wi-Fi control not granted: " + access);
                return false;
            }

            var adapters = await WiFiAdapter.FindAllAdaptersAsync();
            if (adapters.Count == 0)
            {
                Raise("No Wi-Fi adapter found on this device");
                return false;
            }

            _adapter = adapters[0];
            _adapter.AvailableNetworksChanged += (sender, args) => Raise("Available networks changed");

            if (!_statusHooked)
            {
                NetworkInformation.NetworkStatusChanged += sender => Raise("Network status changed");
                _statusHooked = true;
            }

            Raise("Wi-Fi adapter ready: " + _adapter.NetworkAdapter.NetworkAdapterId);
            return true;
        }

        public async Task<IReadOnlyList<WifiNetworkInfo>> ScanAsync()
        {
            var adapter = RequireAdapter();
            await adapter.ScanAsync();
            var connected = await GetConnectedSsidAsync();

            var networks = adapter.NetworkReport.AvailableNetworks
                .Where(n => !string.IsNullOrEmpty(n.Ssid) && !n.IsWiFiDirect)
                .GroupBy(n => n.Ssid)
                .Select(g => g.OrderByDescending(n => n.NetworkRssiInDecibelMilliwatts).First())
                .Select(n => new WifiNetworkInfo
                {
                    Ssid = n.Ssid,
                    Bssid = n.Bssid,
                    SignalBars = n.SignalBars,
                    Rssi = n.NetworkRssiInDecibelMilliwatts,
                    Security = DescribeSecurity(n.SecuritySettings.NetworkAuthenticationType),
                    IsOpen = n.SecuritySettings.NetworkAuthenticationType == NetworkAuthenticationType.Open80211,
                    IsConnected = string.Equals(n.Ssid, connected, StringComparison.Ordinal)
                })
                .OrderByDescending(n => n.IsConnected)
                .ThenByDescending(n => n.SignalBars)
                .ThenBy(n => n.Ssid)
                .ToList();

            Raise("Scan complete: " + networks.Count + " networks");
            return networks;
        }

        public async Task<WifiConnectResult> ConnectAsync(WifiNetworkInfo target, string password)
        {
            var adapter = RequireAdapter();
            await adapter.ScanAsync();
            var network = adapter.NetworkReport.AvailableNetworks
                .Where(n => string.Equals(n.Ssid, target.Ssid, StringComparison.Ordinal))
                .OrderByDescending(n => n.NetworkRssiInDecibelMilliwatts)
                .FirstOrDefault();

            if (network == null)
            {
                return new WifiConnectResult(false, "Network is no longer in range");
            }

            var open = network.SecuritySettings.NetworkAuthenticationType == NetworkAuthenticationType.Open80211;
            if (!open && string.IsNullOrEmpty(password))
            {
                return new WifiConnectResult(false, "This network needs a password");
            }

            WiFiConnectionResult result;
            try
            {
                if (open)
                {
                    result = await adapter.ConnectAsync(network, WiFiReconnectionKind.Automatic);
                }
                else
                {
                    var credential = new PasswordCredential();
                    credential.Password = password;
                    result = await adapter.ConnectAsync(network, WiFiReconnectionKind.Automatic, credential);
                }
            }
            catch (Exception ex)
            {
                Raise("Connect to " + target.Ssid + " threw " + ex.GetType().Name);
                return new WifiConnectResult(false, ex.Message);
            }

            var message = DescribeConnection(result.ConnectionStatus);
            Raise("Connect to " + target.Ssid + ": " + message);
            return new WifiConnectResult(result.ConnectionStatus == WiFiConnectionStatus.Success, message);
        }

        public Task DisconnectAsync()
        {
            var adapter = RequireAdapter();
            adapter.Disconnect();
            Raise("Disconnected");
            return Task.CompletedTask;
        }

        public async Task<string?> GetConnectedSsidAsync()
        {
            if (_adapter == null)
            {
                return null;
            }

            var profile = await _adapter.NetworkAdapter.GetConnectedProfileAsync();
            if (profile == null || !profile.IsWlanConnectionProfile)
            {
                return null;
            }

            return profile.WlanConnectionProfileDetails.GetConnectedSsid();
        }

        private WiFiAdapter RequireAdapter()
        {
            return _adapter ?? throw new InvalidOperationException("Wi-Fi service is not initialised");
        }

        private void Raise(string message)
        {
            StatusChanged?.Invoke(this, message);
        }

        private static string DescribeSecurity(NetworkAuthenticationType type)
        {
            switch (type)
            {
                case NetworkAuthenticationType.Open80211:
                    return "Open";
                case NetworkAuthenticationType.RsnaPsk:
                    return "WPA2-Personal";
                case NetworkAuthenticationType.Rsna:
                    return "WPA2-Enterprise";
                case NetworkAuthenticationType.WpaPsk:
                    return "WPA-Personal";
                case NetworkAuthenticationType.Wpa:
                    return "WPA-Enterprise";
                case NetworkAuthenticationType.Wpa3Sae:
                    return "WPA3-Personal";
                case NetworkAuthenticationType.Wpa3:
                    return "WPA3-Enterprise";
                case NetworkAuthenticationType.SharedKey80211:
                    return "WEP";
                default:
                    return type.ToString();
            }
        }

        private static string DescribeConnection(WiFiConnectionStatus status)
        {
            switch (status)
            {
                case WiFiConnectionStatus.Success:
                    return "Connected";
                case WiFiConnectionStatus.InvalidCredential:
                    return "Wrong password";
                case WiFiConnectionStatus.NetworkNotAvailable:
                    return "Network not available";
                case WiFiConnectionStatus.Timeout:
                    return "Timed out";
                case WiFiConnectionStatus.UnsupportedAuthenticationProtocol:
                    return "Authentication type not supported";
                case WiFiConnectionStatus.AccessRevoked:
                    return "Wi-Fi access revoked";
                default:
                    return "Failed (" + status + ")";
            }
        }
    }
}
