using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing application settings.
    /// Handles loading, saving, and validation of configuration settings.
    /// </summary>
    public interface ISettingsService
    {
        AppSettings Settings { get; }
        
        void LoadSettings();
        void SaveSettings();
    }
}
