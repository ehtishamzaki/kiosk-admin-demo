using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KioskAdmin.Models;

namespace KioskAdmin.Services
{
    public interface IWifiService
    {
        event EventHandler<string>? StatusChanged;

        bool IsReady { get; }

        Task<bool> InitializeAsync();

        Task<IReadOnlyList<WifiNetworkInfo>> ScanAsync();

        Task<WifiConnectResult> ConnectAsync(WifiNetworkInfo network, string password);

        Task DisconnectAsync();

        Task<string?> GetConnectedSsidAsync();
    }
}
