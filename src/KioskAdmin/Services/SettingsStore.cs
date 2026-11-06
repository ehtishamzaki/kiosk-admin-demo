using System;
using System.IO;
using System.Text.Json;
using KioskAdmin.Models;

namespace KioskAdmin.Services
{
    public sealed class SettingsStore
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { WriteIndented = true };
        private readonly string _path;

        public SettingsStore(string path)
        {
            _path = path;
        }

        public string Path => _path;

        public static SettingsStore Default()
        {
            var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KioskAdmin");
            return new SettingsStore(System.IO.Path.Combine(dir, "config.json"));
        }

        public AppSettings Load()
        {
            if (!File.Exists(_path))
            {
                return new AppSettings();
            }

            try
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
            }
            catch (JsonException)
            {
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            var dir = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Options));
            File.Move(tmp, _path, true);
        }
    }
}
