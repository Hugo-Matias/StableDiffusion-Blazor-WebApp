using BlazorWebApp.Models;
using BlazorWebApp.Scheduler.Models;
using BlazorWebApp.Scheduler.Targets;
using System.Text.Json;
using static BlazorWebApp.Models.FragmentKeys;

namespace BlazorWebApp.Scheduler.Engine
{
    /// <summary>
    /// Default <see cref="IParameterApplier"/>. Writes to fragments, prompts, LoRAs, assets and output config.
    /// Null values are no-ops except for output routing where null restores the job default.
    /// </summary>
    public class ParameterApplier : IParameterApplier
    {
        private readonly ILogger<ParameterApplier> _logger;

        public ParameterApplier(ILogger<ParameterApplier> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public void Apply(GenerationParameters parameters, JobOutputConfig output, ParameterTarget target, object? value)
        {
            // Directives and variations hydrated from JSON (e.g. Run snapshots, persisted jobs)
            // arrive as JsonElement. Unwrap once at the boundary so every downstream reader
            // observes plain CLR primitives.
            value = UnwrapJsonElement(value);

            switch (target)
            {
                case FragmentTarget ft: ApplyFragment(parameters, ft, value); break;
                case PromptTarget pt: ApplyPrompt(parameters, pt, value); break;
                case LoraTarget lt: ApplyLora(parameters, lt, value); break;
                case AssetTarget at: ApplyAsset(parameters, at, value); break;
                case OutputTarget ot: ApplyOutput(output, ot, value); break;
                default:
                    _logger.LogWarning("Unknown parameter target type: {Type}", target.GetType().Name);
                    break;
            }
        }

        private static void ApplyFragment(GenerationParameters p, FragmentTarget target, object? value)
        {
            if (string.IsNullOrWhiteSpace(target.FragmentId) || string.IsNullOrWhiteSpace(target.ParamKey)) return;
            var fragment = p.GetOrCreateFragment(target.FragmentId);
            fragment.Values[target.ParamKey] = value;
        }

        private static void ApplyPrompt(GenerationParameters p, PromptTarget target, object? value)
        {
            var fragment = p.GetOrCreateFragment(Fragments.Prompts);
            var key = target.IsNegative ? Params.Negative : Params.Positive;
            var produced = value?.ToString() ?? string.Empty;

            if (target.Mode == PromptWriteMode.Replace || string.IsNullOrEmpty(produced))
            {
                // Replace explicitly, or nothing to combine - just write the produced value.
                fragment.Values[key] = produced;
                return;
            }

            var current = fragment.Values.TryGetValue(key, out var existing) ? existing?.ToString() ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(current))
            {
                // Nothing to append/prepend to; skip the separator to avoid leading/trailing commas.
                fragment.Values[key] = produced;
                return;
            }

            var sep = target.Separator ?? string.Empty;
            fragment.Values[key] = target.Mode switch
            {
                PromptWriteMode.Prepend => produced + sep + current,
                _ => current + sep + produced, // Append (default)
            };
        }

        private void ApplyLora(GenerationParameters p, LoraTarget target, object? value)
        {
            if (string.IsNullOrWhiteSpace(target.LoraName)) return;
            var lora = p.Loras.FirstOrDefault(l => string.Equals(l.Name, target.LoraName, StringComparison.OrdinalIgnoreCase));
            if (lora is null)
            {
                _logger.LogWarning("LoraTarget '{Name}' not found in parameters.Loras; skipping.", target.LoraName);
                return;
            }
            // Value is interpreted as the LoRA strength.
            if (value is null) return;
            lora.Strength = ConvertToFloat(value);
        }

        private static void ApplyAsset(GenerationParameters p, AssetTarget target, object? value)
        {
            if (string.IsNullOrWhiteSpace(target.AssetKey)) return;
            p.Assets[target.AssetKey] = value?.ToString() ?? string.Empty;
        }

        private static void ApplyOutput(JobOutputConfig output, OutputTarget target, object? value)
        {
            var s = value?.ToString();
            switch (target.Field)
            {
                case OutputField.Project: output.ProjectName = s; break;
                case OutputField.Folder: output.FolderName = s; break;
            }
        }

        private static float ConvertToFloat(object value) => value switch
        {
            float f => f,
            double d => (float)d,
            int i => i,
            long l => l,
            decimal m => (float)m,
            string s when float.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture)
        };

        /// <summary>
        /// Normalizes <see cref="JsonElement"/> values that result from JSON-cloned directives and
        /// variations back into plain CLR primitives (<see cref="string"/>, <see cref="long"/>,
        /// <see cref="double"/>, <see cref="bool"/>). Objects / arrays are left as-is.
        /// </summary>
        private static object? UnwrapJsonElement(object? value)
        {
            if (value is not JsonElement el) return value;
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.TryGetInt64(out var l) ? (object)l : el.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => value,
            };
        }
    }
}
