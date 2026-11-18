namespace BlazorWebApp.Services.WildcardForge.Models
{
    /// <summary>
    /// Static descriptor for one of the four verbosity levels used across the Forge UI and prompts.
    /// </summary>
    public class VerbosityLevelInfo
    {
        public string Key { get; set; } = string.Empty; // minimal | balanced | detailed | verbose
        public string Label { get; set; } = string.Empty;
        public string WordRange { get; set; } = string.Empty;
        public string BestFor { get; set; } = string.Empty;
    }
}
