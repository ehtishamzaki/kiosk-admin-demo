using System;
using System.Printing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Xps;
using KioskAdmin.Models;

namespace KioskAdmin.Services
{
    public sealed class ReportPrintService
    {
        private const double PageWidth = 816;
        private const double PageHeight = 1056;
        private const double Margin = 72;

        public PrintJobResult Submit(string printerName, DiagnosticReport report, out string jobName)
        {
            jobName = "Diagnostic report " + report.ReportId;
            if (string.IsNullOrWhiteSpace(printerName))
            {
                return PrintJobResult.Failed("No printer selected");
            }

            try
            {
                using var queue = OpenQueue(printerName);
                queue.Refresh();
                if (!PrinterService.IsAvailable(queue.QueueStatus))
                {
                    return PrintJobResult.Failed("Printer not ready: " + PrinterService.DescribeStatus(queue.QueueStatus));
                }

                queue.CurrentJobSettings.Description = jobName;
                var document = BuildDocument(report);
                var writer = PrintQueue.CreateXpsDocumentWriter(queue);
                writer.Write(document);
                return PrintJobResult.Ok("Job submitted to " + queue.FullName);
            }
            catch (PrintSystemException ex)
            {
                return PrintJobResult.Failed("Print system error: " + ex.Message);
            }
            catch (Exception ex)
            {
                return PrintJobResult.Failed(ex.GetType().Name + ": " + ex.Message);
            }
        }

        public Task<PrintJobResult> TrackAsync(string printerName, string jobName, TimeSpan timeout, CancellationToken cancellation)
        {
            return Task.Run(() => Track(printerName, jobName, timeout, cancellation), cancellation);
        }

        private static PrintJobResult Track(string printerName, string jobName, TimeSpan timeout, CancellationToken cancellation)
        {
            var deadline = DateTime.UtcNow + timeout;
            try
            {
                using var queue = OpenQueue(printerName);
                while (DateTime.UtcNow < deadline && !cancellation.IsCancellationRequested)
                {
                    queue.Refresh();
                    PrintSystemJobInfo? match = null;
                    foreach (var job in queue.GetPrintJobInfoCollection())
                    {
                        if (match == null && string.Equals(job.Name, jobName, StringComparison.Ordinal))
                        {
                            match = job;
                        }
                        else
                        {
                            job.Dispose();
                        }
                    }

                    if (match == null)
                    {
                        return PrintJobResult.Ok("Job completed and left the queue");
                    }

                    using (match)
                    {
                        match.Refresh();
                        if (match.IsInError || match.IsUserInterventionRequired || match.IsOffline || match.IsPaperOut)
                        {
                            return PrintJobResult.Failed("Job stopped: " + match.JobStatus);
                        }

                        if (match.IsCompleted || match.IsPrinted)
                        {
                            return PrintJobResult.Ok("Job printed (" + match.NumberOfPages + " pages)");
                        }
                    }

                    Thread.Sleep(500);
                }

                return PrintJobResult.Ok("Job is queued; the printer has not confirmed completion yet");
            }
            catch (Exception ex)
            {
                return PrintJobResult.Failed("Could not track job: " + ex.Message);
            }
        }

        private static PrintQueue OpenQueue(string printerName)
        {
            if (printerName.StartsWith(@"\\", StringComparison.Ordinal))
            {
                var split = printerName.LastIndexOf('\\');
                if (split > 1)
                {
                    var server = new PrintServer(printerName.Substring(0, split));
                    return server.GetPrintQueue(printerName.Substring(split + 1));
                }
            }

            var local = new LocalPrintServer();
            return local.GetPrintQueue(printerName);
        }

        private static FixedDocument BuildDocument(DiagnosticReport report)
        {
            var document = new FixedDocument();
            document.DocumentPaginator.PageSize = new Size(PageWidth, PageHeight);

            var page = new FixedPage { Width = PageWidth, Height = PageHeight, Background = Brushes.White };
            var stack = new StackPanel { Width = PageWidth - Margin * 2 };
            FixedPage.SetLeft(stack, Margin);
            FixedPage.SetTop(stack, Margin);

            var lines = report.BuildLines();
            for (var i = 0; i < lines.Count; i++)
            {
                var block = new TextBlock
                {
                    Text = lines[i],
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = i == 0 ? 20 : 12,
                    FontWeight = i == 0 ? FontWeights.Bold : FontWeights.Normal,
                    Margin = new Thickness(0, 0, 0, i == 0 ? 14 : 3),
                    Foreground = Brushes.Black
                };
                stack.Children.Add(block);
            }

            var footer = new TextBlock
            {
                Text = report.DeviceName + "  |  " + report.SerialNumber + "  |  Printed " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                FontSize = 9,
                Foreground = Brushes.Gray
            };
            FixedPage.SetLeft(footer, Margin);
            FixedPage.SetTop(footer, PageHeight - Margin + 20);

            page.Children.Add(stack);
            page.Children.Add(footer);
            page.Measure(new Size(PageWidth, PageHeight));
            page.Arrange(new Rect(0, 0, PageWidth, PageHeight));
            page.UpdateLayout();

            var content = new PageContent();
            ((IAddChild)content).AddChild(page);
            document.Pages.Add(content);
            return document;
        }
    }
}
