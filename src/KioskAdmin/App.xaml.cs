using System.Windows;
using System.Windows.Threading;
using KioskAdmin.Services;

namespace KioskAdmin
{
    public partial class App : Application
    {
        public static AppLog Log { get; } = AppLog.Default();

        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            Log.Info("Application starting, version " + typeof(App).Assembly.GetName().Version);
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Info("Application exiting with code " + e.ApplicationExitCode);
            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error("Unhandled exception", e.Exception);
            MessageBox.Show("Something went wrong. The error has been logged.\n\n" + e.Exception.Message, "Kiosk Admin", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
        }
    }
}
