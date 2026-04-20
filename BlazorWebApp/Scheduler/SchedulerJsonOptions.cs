using System.Text.Json;
using System.Text.Json.Serialization;
using BlazorWebApp.Data.Converters;

namespace BlazorWebApp.Scheduler
{
    /// <summary>
    /// Centralized <see cref="JsonSerializerOptions"/> for persisting Scheduler aggregates
    /// (<see cref="Models.Job"/>, directives, variations, targets).
    /// Registers the project-wide <see cref="GenerationParametersJsonConverter"/> and
    /// <see cref="FragmentParametersJsonConverter"/> so nested <see cref="BlazorWebApp.Models.GenerationParameters"/>
    /// values (stored on <see cref="Models.Job.BaseParameters"/>) round-trip with full type fidelity.
    /// </summary>
    public static class SchedulerJsonOptions
    {
        /// <summary>
        /// Default options: camelCase, indented, enum-as-string, case-insensitive reads.
        /// </summary>
        public static JsonSerializerOptions Default { get; } = Build(writeIndented: true);

        /// <summary>
        /// Compact options (no indentation) suited for DB column persistence.
        /// </summary>
        public static JsonSerializerOptions Compact { get; } = Build(writeIndented: false);

        private static JsonSerializerOptions Build(bool writeIndented) => new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = writeIndented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(),
                new GenerationParametersJsonConverter(),
                new FragmentParametersJsonConverter()
            }
        };
    }
}
