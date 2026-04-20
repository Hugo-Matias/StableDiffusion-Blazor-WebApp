using BlazorWebApp.Models;
using BlazorWebApp.Scheduler.Directives;
using BlazorWebApp.Scheduler.Models;
using static BlazorWebApp.Models.FragmentKeys;

namespace BlazorWebApp.Scheduler.Engine
{
    /// <summary>
    /// Default <see cref="IDirectiveExecutor"/> covering all built-in directive types.
    /// Prompt mutations are applied directly against the prompts fragment so later wildcard /
    /// style expansion in <c>IImageService.GenerateImagesAsync</c> sees the edited text.
    /// </summary>
    public class DirectiveExecutor : IDirectiveExecutor
    {
        private readonly IParameterApplier _applier;
        private readonly ILogger<DirectiveExecutor> _logger;

        public DirectiveExecutor(IParameterApplier applier, ILogger<DirectiveExecutor> logger)
        {
            _applier = applier;
            _logger = logger;
        }

        /// <inheritdoc />
        public void Apply(GenerationParameters parameters, JobOutputConfig output, Directive directive)
        {
            if (!directive.Enabled) return;

            switch (directive)
            {
                case SetValueDirective sv: _applier.Apply(parameters, output, sv.Target, sv.Value); break;
                case AppendPromptDirective ap: ApplyAppend(parameters, ap); break;
                case ReplacePromptDirective rp: ApplyReplace(parameters, rp); break;
                case AddLoraDirective al: ApplyAddLora(parameters, al); break;
                case RemoveLoraDirective rl: ApplyRemoveLora(parameters, rl); break;
                case ToggleLoraDirective tl: ApplyToggleLora(parameters, tl); break;
                case AddPromptStyleDirective ps: ApplyAddStyles(parameters, ps); break;
                case SwapAssetDirective sa: parameters.Assets[sa.AssetKey] = sa.AssetValue; break;
                case SetOutputDirective so:
                    if (so.ProjectName is not null) output.ProjectName = so.ProjectName;
                    if (so.FolderName is not null) output.FolderName = so.FolderName;
                    break;
                default:
                    _logger.LogWarning("Unknown directive type: {Type}", directive.GetType().Name);
                    break;
            }
        }

        private static void ApplyAppend(GenerationParameters p, AppendPromptDirective d)
        {
            if (string.IsNullOrEmpty(d.Text)) return;
            var fragment = p.GetOrCreateFragment(Fragments.Prompts);
            var key = d.IsNegative ? Params.Negative : Params.Positive;
            var current = fragment.GetValue<string>(key) ?? string.Empty;
            string combined;
            if (string.IsNullOrEmpty(current))
                combined = d.Text;
            else if (d.IsPrefix)
                combined = d.Text + d.Separator + current;
            else
                combined = current + d.Separator + d.Text;
            fragment.Values[key] = combined;
        }

        private static void ApplyReplace(GenerationParameters p, ReplacePromptDirective d)
        {
            if (string.IsNullOrEmpty(d.Search)) return;
            var fragment = p.GetOrCreateFragment(Fragments.Prompts);
            var key = d.IsNegative ? Params.Negative : Params.Positive;
            var current = fragment.GetValue<string>(key) ?? string.Empty;
            var comparison = d.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            fragment.Values[key] = current.Replace(d.Search, d.Replace, comparison);
        }

        private static void ApplyAddLora(GenerationParameters p, AddLoraDirective d)
        {
            if (d.Lora is null) return;
            // Clone so modifications in later iterations cannot leak back into the directive config.
            p.Loras.Add(new Lora(d.Lora));
        }

        private static void ApplyRemoveLora(GenerationParameters p, RemoveLoraDirective d)
        {
            if (string.IsNullOrWhiteSpace(d.LoraName)) return;
            p.Loras.RemoveAll(l => string.Equals(l.Name, d.LoraName, StringComparison.OrdinalIgnoreCase));
        }

        private void ApplyToggleLora(GenerationParameters p, ToggleLoraDirective d)
        {
            if (string.IsNullOrWhiteSpace(d.LoraName)) return;
            var lora = p.Loras.FirstOrDefault(l => string.Equals(l.Name, d.LoraName, StringComparison.OrdinalIgnoreCase));
            if (lora is null)
            {
                _logger.LogWarning("ToggleLoraDirective: LoRA '{Name}' not found; skipping.", d.LoraName);
                return;
            }
            lora.IsEnabled = d.Enable;
        }

        private void ApplyAddStyles(GenerationParameters p, AddPromptStyleDirective d)
        {
            if (d.StyleNames.Count == 0) return;
            // Styles are resolved by ImageService.PrepareGenerationParametersAsync from app state. Here we
            // only record the requested style names by prepending simple tokens to the positive prompt so
            // they flow through ParseStyles. This keeps the directive self-contained; richer resolution
            // (against the live style catalog) is left for a future refinement.
            var fragment = p.GetOrCreateFragment(Fragments.Prompts);
            var current = fragment.GetValue<string>(Params.Positive) ?? string.Empty;
            var styleTokens = string.Join(", ", d.StyleNames);
            fragment.Values[Params.Positive] = string.IsNullOrEmpty(current)
                ? styleTokens
                : current + ", " + styleTokens;
        }
    }
}
