using BlazorWebApp.Models;

namespace BlazorWebApp.Services.Cleanup
{
    public record CleanupGroupExplanationOptions
    {
        public const string SectionName = "Cleanup:VisionLanguage";

        public bool Enabled { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public InterrogationStyle Style { get; set; } = InterrogationStyle.Simple;
        public bool ForceRefresh { get; set; }
    }
}