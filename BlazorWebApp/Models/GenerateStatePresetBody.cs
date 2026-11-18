namespace BlazorWebApp.Models
{
    public class GenerateStatePresetBody
    {
        public int SchemaVersion { get; set; } = 1;
        public GenerationParameters Parameters { get; set; } = new();
    }
}