using System;
using System.IO;

namespace KioskAdmin.Services
{
    public sealed class AppLog
    {
        private readonly string _directory;
        private readonly object _gate = new object();

        public AppLog(string directory)
        {
            _directory = directory;
            Directory.CreateDirectory(_directory);
        }

        public event EventHandler<string>? EntryWritten;

        public static AppLog Default()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KioskAdmin", "logs");
            return new AppLog(dir);
        }

        public void Info(string message) => Write("INFO", message);

        public void Warn(string message) => Write("WARN", message);

        public void Error(string message, Exception? ex = null)
        {
            Write("ERROR", ex == null ? message : message + " | " + ex.GetType().Name + ": " + ex.Message);
        }

        private void Write(string level, string message)
        {
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + message;
            lock (_gate)
            {
                var file = Path.Combine(_directory, "kioskadmin-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                File.AppendAllText(file, line + Environment.NewLine);
            }

            EntryWritten?.Invoke(this, line);
        }
    }
}
