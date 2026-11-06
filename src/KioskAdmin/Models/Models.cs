using System;
using System.Collections.Generic;

namespace KioskAdmin.Models
{
    public sealed class WifiNetworkInfo
    {
        public string Ssid { get; set; } = string.Empty;
        public string Bssid { get; set; } = string.Empty;
        public int SignalBars { get; set; }
        public double Rssi { get; set; }
        public string Security { get; set; } = string.Empty;
        public bool IsOpen { get; set; }
        public bool IsConnected { get; set; }
        public string ConnectedLabel => IsConnected ? "Connected" : string.Empty;
        public string SignalLabel => new string('|', SignalBars) + new string('.', Math.Max(0, 5 - SignalBars));
    }

    public sealed class WifiConnectResult
    {
        public WifiConnectResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public bool Success { get; }
        public string Message { get; }
    }

    public sealed class PrinterInfo
    {
        public string Name { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public bool IsNetwork { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public int Jobs { get; set; }
        public bool IsWindowsDefault { get; set; }
        public bool IsAppDefault { get; set; }
        public string Kind => IsNetwork ? "Network" : "Local";
        public string DefaultLabel => IsAppDefault ? "App default" : (IsWindowsDefault ? "Windows default" : string.Empty);
    }

    public sealed class PrintJobResult
    {
        private PrintJobResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public bool Success { get; }
        public string Message { get; }

        public static PrintJobResult Ok(string message) => new PrintJobResult(true, message);
        public static PrintJobResult Failed(string message) => new PrintJobResult(false, message);
    }

    public sealed class AppSettings
    {
        public string DeviceName { get; set; } = "DX-200 Analyzer";
        public string SerialNumber { get; set; } = "DX200-00017";
        public string PinHash { get; set; } = string.Empty;
        public string PinSalt { get; set; } = string.Empty;
        public string DefaultPrinter { get; set; } = string.Empty;
        public bool KioskMode { get; set; }
        public int MaxPinAttempts { get; set; } = 5;
        public int LockoutSeconds { get; set; } = 30;
    }

    public sealed class DiagnosticResult
    {
        public DiagnosticResult(string analyte, double value, string unit, double low, double high)
        {
            Analyte = analyte;
            Value = value;
            Unit = unit;
            Low = low;
            High = high;
        }

        public string Analyte { get; }
        public double Value { get; }
        public string Unit { get; }
        public double Low { get; }
        public double High { get; }
        public string Flag => Value < Low ? "LOW" : (Value > High ? "HIGH" : string.Empty);
    }

    public sealed class DiagnosticReport
    {
        public string ReportId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string SampleId { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public DateTime RunAt { get; set; }
        public List<DiagnosticResult> Results { get; } = new List<DiagnosticResult>();

        public IReadOnlyList<string> BuildLines()
        {
            var lines = new List<string>
            {
                "DIAGNOSTIC REPORT",
                "Report ID: " + ReportId,
                "Device: " + DeviceName + " (" + SerialNumber + ")",
                "Sample: " + SampleId,
                "Operator: " + Operator,
                "Run at: " + RunAt.ToString("yyyy-MM-dd HH:mm:ss"),
                string.Empty,
                FormatRow("Analyte", "Result", "Unit", "Range", "Flag")
            };

            foreach (var r in Results)
            {
                lines.Add(FormatRow(r.Analyte, r.Value.ToString("0.00"), r.Unit, r.Low.ToString("0.0") + " - " + r.High.ToString("0.0"), r.Flag));
            }

            lines.Add(string.Empty);
            lines.Add("Results are for the sample identified above only.");
            return lines;
        }

        public static DiagnosticReport Sample(string deviceName, string serialNumber)
        {
            var report = new DiagnosticReport
            {
                ReportId = "R" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                DeviceName = deviceName,
                SerialNumber = serialNumber,
                SampleId = "S-" + DateTime.Now.ToString("HHmmss"),
                Operator = "admin",
                RunAt = DateTime.Now
            };
            report.Results.Add(new DiagnosticResult("Glucose", 5.4, "mmol/L", 3.9, 5.6));
            report.Results.Add(new DiagnosticResult("Creatinine", 71.0, "umol/L", 60.0, 110.0));
            report.Results.Add(new DiagnosticResult("Sodium", 148.0, "mmol/L", 135.0, 145.0));
            report.Results.Add(new DiagnosticResult("Potassium", 4.1, "mmol/L", 3.5, 5.1));
            report.Results.Add(new DiagnosticResult("Haemoglobin", 11.2, "g/dL", 12.0, 16.0));
            return report;
        }

        private static string FormatRow(string a, string b, string c, string d, string e)
        {
            return a.PadRight(14) + b.PadLeft(8) + "  " + c.PadRight(8) + d.PadRight(16) + e;
        }
    }
}
