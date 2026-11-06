using System;
using System.Collections.Generic;
using System.Printing;
using System.Runtime.InteropServices;
using KioskAdmin.Models;

namespace KioskAdmin.Services
{
    public sealed class PrinterService
    {
        private const PrintQueueStatus BlockingStates =
            PrintQueueStatus.Offline
            | PrintQueueStatus.Error
            | PrintQueueStatus.NotAvailable
            | PrintQueueStatus.PaperOut
            | PrintQueueStatus.PaperJam
            | PrintQueueStatus.PaperProblem
            | PrintQueueStatus.DoorOpen
            | PrintQueueStatus.UserIntervention
            | PrintQueueStatus.PendingDeletion
            | PrintQueueStatus.NoToner
            | PrintQueueStatus.OutOfMemory
            | PrintQueueStatus.ServerUnknown;

        public IReadOnlyList<PrinterInfo> GetPrinters(string appDefault)
        {
            var list = new List<PrinterInfo>();
            string windowsDefault = string.Empty;
            try
            {
                using var def = LocalPrintServer.GetDefaultPrintQueue();
                windowsDefault = def.FullName;
            }
            catch (PrintSystemException)
            {
            }

            using var server = new LocalPrintServer();
            var types = new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections };
            foreach (var queue in server.GetPrintQueues(types))
            {
                using (queue)
                {
                    var info = new PrinterInfo
                    {
                        Name = queue.FullName,
                        ShortName = queue.Name,
                        IsNetwork = queue.FullName.StartsWith(@"\\", StringComparison.Ordinal),
                        IsWindowsDefault = string.Equals(queue.FullName, windowsDefault, StringComparison.OrdinalIgnoreCase),
                        IsAppDefault = string.Equals(queue.FullName, appDefault, StringComparison.OrdinalIgnoreCase)
                    };

                    try
                    {
                        queue.Refresh();
                        info.Location = queue.Location ?? string.Empty;
                        info.Status = DescribeStatus(queue.QueueStatus);
                        info.IsAvailable = IsAvailable(queue.QueueStatus);
                        info.Jobs = queue.NumberOfJobs;
                    }
                    catch (Exception ex)
                    {
                        info.Status = "Unavailable: " + ex.Message;
                        info.IsAvailable = false;
                    }

                    list.Add(info);
                }
            }

            list.Sort((a, b) =>
            {
                var c = b.IsAppDefault.CompareTo(a.IsAppDefault);
                if (c != 0) return c;
                c = b.IsWindowsDefault.CompareTo(a.IsWindowsDefault);
                if (c != 0) return c;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return list;
        }

        public static bool IsAvailable(PrintQueueStatus status)
        {
            return (status & BlockingStates) == 0;
        }

        public static string DescribeStatus(PrintQueueStatus status)
        {
            if (status == PrintQueueStatus.None)
            {
                return "Ready";
            }

            var parts = new List<string>();
            foreach (PrintQueueStatus flag in Enum.GetValues(typeof(PrintQueueStatus)))
            {
                if (flag != PrintQueueStatus.None && (status & flag) == flag)
                {
                    parts.Add(Humanise(flag));
                }
            }

            return parts.Count == 0 ? "Ready" : string.Join(", ", parts);
        }

        public bool AddNetworkPrinter(string uncPath, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(uncPath) || !uncPath.StartsWith(@"\\", StringComparison.Ordinal))
            {
                error = "Enter the printer as \\\\server\\printer";
                return false;
            }

            if (AddPrinterConnection(uncPath))
            {
                return true;
            }

            error = new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;
            return false;
        }

        public bool SetWindowsDefault(string name, out string error)
        {
            error = string.Empty;
            if (SetDefaultPrinter(name))
            {
                return true;
            }

            error = new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;
            return false;
        }

        private static string Humanise(PrintQueueStatus flag)
        {
            switch (flag)
            {
                case PrintQueueStatus.PaperOut: return "Out of paper";
                case PrintQueueStatus.PaperJam: return "Paper jam";
                case PrintQueueStatus.PaperProblem: return "Paper problem";
                case PrintQueueStatus.DoorOpen: return "Door open";
                case PrintQueueStatus.TonerLow: return "Toner low";
                case PrintQueueStatus.NoToner: return "No toner";
                case PrintQueueStatus.UserIntervention: return "Needs attention";
                case PrintQueueStatus.NotAvailable: return "Not available";
                case PrintQueueStatus.PendingDeletion: return "Being removed";
                case PrintQueueStatus.ManualFeed: return "Manual feed";
                case PrintQueueStatus.OutputBinFull: return "Output bin full";
                case PrintQueueStatus.OutOfMemory: return "Out of memory";
                case PrintQueueStatus.ServerUnknown: return "Server unknown";
                case PrintQueueStatus.IOActive: return "Transferring";
                case PrintQueueStatus.WarmingUp: return "Warming up";
                case PrintQueueStatus.PowerSave: return "Power save";
                default: return flag.ToString();
            }
        }

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool AddPrinterConnection(string pName);

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDefaultPrinter(string pszPrinter);
    }
}
