using BlazorWebApp.Models;

namespace BlazorWebApp.Services.Cleanup
{
    public record CleanupGroupExplanationOptions
    {
        public const string SectionName = "Cleanup:VisionLanguage";

        public bool Enabled { get; init; }
        public string ModelName { get; init; } = string.Empty;
        public InterrogationStyle Style { get; init; } = InterrogationStyle.Simple;
        public bool ForceRefresh { get; init; }
    }
}