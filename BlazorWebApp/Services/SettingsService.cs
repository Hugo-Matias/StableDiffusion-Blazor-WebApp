using BlazorWebApp.Models;
using System.Text.Json;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service responsible for managing application settings.
    /// Handles loading, saving, and validation of configuration settings from JSON file.
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private readonly string _settingsFile = "BlazorDiffusion.json";
        private readonly IIOService _io;

        public AppSettings Settings { get; private set; }

        public SettingsService(IIOService io)
        {
            _io = io;
            Settings = new AppSettings();
            LoadSettings();
        }

        public void LoadSettings()
        {
            var json = _io.LoadText(_settingsFile);

            if (json != null)
            {
                try
                {
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        Settings = settings;
                        SaveSettings(); // Ensure any new default values are persisted
                        return;
                    }
                }
                catch (JsonException)
                {
                    // If JSON is invalid, fall through to create defaults
                }
            }

            // Create defaults if file doesn't exist or JSON is invalid
            Settings = new AppSettings();
            SaveSettings();
        }

        public void SaveSettings()
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            _io.SaveText(_settingsFile, json);
        }
    }
}
