using BlazorWebApp.Models;
using BlazorWebApp.Scheduler.Models;
using BlazorWebApp.Scheduler.Targets;
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
            fragment.Values[key] = value?.ToString() ?? string.Empty;
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
    }
}
