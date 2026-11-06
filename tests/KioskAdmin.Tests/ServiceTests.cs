using System;
using System.IO;
using System.Linq;
using System.Printing;
using KioskAdmin.Models;
using KioskAdmin.Services;
using Xunit;

namespace KioskAdmin.Tests
{
    public class PinGateTests
    {
        [Fact]
        public void Create_then_verify_accepts_the_same_pin()
        {
            var created = PinGate.Create("2468");
            Assert.True(PinGate.Verify("2468", created.Hash, created.Salt));
        }

        [Fact]
        public void Verify_rejects_a_different_pin()
        {
            var created = PinGate.Create("2468");
            Assert.False(PinGate.Verify("2469", created.Hash, created.Salt));
        }

        [Fact]
        public void Two_hashes_of_the_same_pin_use_different_salts()
        {
            var a = PinGate.Create("1234");
            var b = PinGate.Create("1234");
            Assert.NotEqual(a.Salt, b.Salt);
            Assert.NotEqual(a.Hash, b.Hash);
        }

        [Fact]
        public void Short_pin_is_rejected_at_creation()
        {
            Assert.Throws<ArgumentException>(() => PinGate.Create("12"));
        }

        [Fact]
        public void Verify_handles_corrupt_stored_values()
        {
            Assert.False(PinGate.Verify("1234", "not base64", "???"));
            Assert.False(PinGate.Verify("1234", string.Empty, string.Empty));
        }
    }

    public class SettingsStoreTests
    {
        [Fact]
        public void Missing_file_returns_defaults()
        {
            var store = new SettingsStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "config.json"));
            var settings = store.Load();
            Assert.Equal("DX-200 Analyzer", settings.DeviceName);
            Assert.Equal(string.Empty, settings.PinHash);
        }

        [Fact]
        public void Save_then_load_round_trips()
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var store = new SettingsStore(Path.Combine(dir, "config.json"));
            var settings = new AppSettings { DeviceName = "Bench unit", SerialNumber = "B-1", DefaultPrinter = @"\\srv\lab", KioskMode = true };
            store.Save(settings);

            var loaded = store.Load();
            Assert.Equal("Bench unit", loaded.DeviceName);
            Assert.Equal("B-1", loaded.SerialNumber);
            Assert.Equal(@"\\srv\lab", loaded.DefaultPrinter);
            Assert.True(loaded.KioskMode);
            Assert.False(File.Exists(Path.Combine(dir, "config.json.tmp")));
            Directory.Delete(dir, true);
        }

        [Fact]
        public void Corrupt_file_falls_back_to_defaults()
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "config.json");
            File.WriteAllText(path, "{ this is not json");
            var loaded = new SettingsStore(path).Load();
            Assert.Equal("DX-200 Analyzer", loaded.DeviceName);
            Directory.Delete(dir, true);
        }
    }

    public class PrinterStatusTests
    {
        [Fact]
        public void No_flags_means_ready()
        {
            Assert.Equal("Ready", PrinterService.DescribeStatus(PrintQueueStatus.None));
            Assert.True(PrinterService.IsAvailable(PrintQueueStatus.None));
        }

        [Fact]
        public void Offline_or_paper_out_is_not_available()
        {
            Assert.False(PrinterService.IsAvailable(PrintQueueStatus.Offline));
            Assert.False(PrinterService.IsAvailable(PrintQueueStatus.PaperOut | PrintQueueStatus.Printing));
        }

        [Fact]
        public void Busy_or_printing_is_still_available()
        {
            Assert.True(PrinterService.IsAvailable(PrintQueueStatus.Busy | PrintQueueStatus.Printing));
        }

        [Fact]
        public void Combined_flags_are_described_in_plain_words()
        {
            var text = PrinterService.DescribeStatus(PrintQueueStatus.PaperOut | PrintQueueStatus.DoorOpen);
            Assert.Contains("Out of paper", text);
            Assert.Contains("Door open", text);
        }
    }

    public class DiagnosticReportTests
    {
        [Fact]
        public void Sample_report_lines_include_header_and_every_result()
        {
            var report = DiagnosticReport.Sample("Unit", "S1");
            var lines = report.BuildLines();

            Assert.Equal("DIAGNOSTIC REPORT", lines[0]);
            Assert.Contains(lines, l => l.StartsWith("Report ID: " + report.ReportId));
            foreach (var r in report.Results)
            {
                Assert.Contains(lines, l => l.StartsWith(r.Analyte));
            }
        }

        [Fact]
        public void Out_of_range_results_are_flagged()
        {
            var high = new DiagnosticResult("Sodium", 148, "mmol/L", 135, 145);
            var low = new DiagnosticResult("Haemoglobin", 11.2, "g/dL", 12, 16);
            var normal = new DiagnosticResult("Potassium", 4.1, "mmol/L", 3.5, 5.1);

            Assert.Equal("HIGH", high.Flag);
            Assert.Equal("LOW", low.Flag);
            Assert.Equal(string.Empty, normal.Flag);
        }

        [Fact]
        public void Report_ids_are_unique_per_sample()
        {
            var a = DiagnosticReport.Sample("U", "S");
            Assert.StartsWith("R", a.ReportId);
            Assert.Equal(5, a.Results.Count);
            Assert.Equal(a.Results.Count, a.Results.Select(r => r.Analyte).Distinct().Count());
        }
    }
}
