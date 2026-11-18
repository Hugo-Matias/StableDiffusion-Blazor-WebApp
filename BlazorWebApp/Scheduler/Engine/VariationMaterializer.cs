using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Scheduler.Variations;
using BlazorWebApp.Services;

namespace BlazorWebApp.Scheduler.Engine
{
    /// <summary>
    /// Default <see cref="IVariationMaterializer"/> covering all built-in variation types.
    /// Deterministic variations are evaluated inline; <see cref="WildcardVariation"/> delegates to
    /// <see cref="IWildcardService"/> and <see cref="LlmVariation"/> delegates to <see cref="OllamaService"/>.
    /// </summary>
    public class VariationMaterializer : IVariationMaterializer
    {
        private readonly IWildcardService _wildcards;
        private readonly OllamaService _ollama;
        private readonly IDatabaseService _database;
        private readonly ILogger<VariationMaterializer> _logger;

        public VariationMaterializer(
            IWildcardService wildcards,
            OllamaService ollama,
            IDatabaseService database,
            ILogger<VariationMaterializer> logger)
        {
            _wildcards = wildcards;
            _ollama = ollama;
            _database = database;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<object?>> MaterializeAsync(Variation variation, CancellationToken cancellationToken = default)
        {
            return variation switch
            {
                ListVariation l => l.Values.ToArray(),
                RangeVariation r => MaterializeRange(r),
                ToggleVariation t => new[] { t.OnValue, t.OffValue },
                SearchReplaceVariation sr => sr.Replacements.Cast<object?>().ToArray(),
                RandomVariation rv => MaterializeRandom(rv),
                WildcardVariation wv => await MaterializeWildcardAsync(wv, cancellationToken),
                LlmVariation lv => await MaterializeLlmAsync(lv, cancellationToken),
                _ => throw new NotSupportedException($"Unknown variation type: {variation.GetType().Name}")
            };
        }

        private static IReadOnlyList<object?> MaterializeRange(RangeVariation r)
        {
            if (r.Step <= 0)
                throw new ArgumentException($"RangeVariation step must be > 0 (got {r.Step}).", nameof(r));

            var values = new List<object?>();
            // Inclusive range with floating-point tolerance equal to half a step.
            var tolerance = r.Step / 2.0;
            for (double v = r.Start; v <= r.End + tolerance; v += r.Step)
            {
                values.Add(r.IsInteger ? (object)(long)Math.Round(v) : v);
            }
            return values;
        }

        private static IReadOnlyList<object?> MaterializeRandom(RandomVariation r)
        {
            if (r.Count <= 0) return Array.Empty<object?>();
            if (r.Max <= r.Min)
                throw new ArgumentException($"RandomVariation Max ({r.Max}) must be greater than Min ({r.Min}).", nameof(r));

            var rng = r.Seed.HasValue ? new Random(r.Seed.Value) : Random.Shared;
            var values = new List<object?>(r.Count);
            for (int i = 0; i < r.Count; i++)
            {
                var v = r.Min + rng.NextDouble() * (r.Max - r.Min);
                values.Add(r.IsInteger ? (object)(long)Math.Floor(v) : v);
            }
            return values;
        }

        private async Task<IReadOnlyList<object?>> MaterializeWildcardAsync(WildcardVariation w, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(w.CollectionName))
                return Array.Empty<object?>();

            // Full-sweep mode: no repeats and no count -> use every entry exactly once.
            if (!w.AllowRepeats && w.Count is null)
            {
                var all = await _wildcards.GetAllEntryValues(w.CollectionName);
                return all.Cast<object?>().ToArray();
            }

            var count = w.Count ?? 1;
            if (count <= 0) return Array.Empty<object?>();

            var values = new List<object?>(count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var pick = w.Weighted
                    ? await _wildcards.GetRandomEntryWeighted(w.CollectionName)
                    : await _wildcards.GetRandomEntry(w.CollectionName);

                if (pick is null)
                {
                    _logger.LogWarning("Wildcard collection '{Collection}' returned null; aborting materialization.", w.CollectionName);
                    break;
                }

                if (!w.AllowRepeats && !seen.Add(pick))
                {
                    // Skip duplicates but keep drawing; guard against infinite loops.
                    if (seen.Count > count * 4) break;
                    i--;
                    continue;
                }

                values.Add(pick);
            }
            return values;
        }

        private async Task<IReadOnlyList<object?>> MaterializeLlmAsync(LlmVariation l, CancellationToken ct)
        {
            if (l.Count <= 0 || string.IsNullOrWhiteSpace(l.ModelName)) return Array.Empty<object?>();

            // When a system-prompt template is selected, use it as the chat seed and substitute {prompt}.
            List<OllamaChatMessage>? templateMessages = null;
            if (l.SystemPromptTemplateId is int templateId && templateId > 0)
            {
                var template = await _database.GetSystemPromptTemplate(templateId);
                if (template is not null)
                {
                    templateMessages = template.Messages;
                }
                else
                {
                    _logger.LogWarning("SystemPromptTemplate {Id} not found; falling back to default ExpandPrompt.", templateId);
                }
            }

            var values = new List<object?>(l.Count);
            for (int i = 0; i < l.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                string text;
                if (templateMessages is not null)
                {
                    var messages = templateMessages
                        .Select(m => new OllamaChatMessage
                        {
                            Role = m.Role,
                            Content = (m.Content ?? string.Empty).Replace("{prompt}", l.BasePrompt),
                        })
                        .ToList();
                    var response = await _ollama.SendChatMessage(l.ModelName, messages);
                    text = response?.Message?.Content ?? l.BasePrompt;
                }
                else
                {
                    text = await _ollama.ExpandPrompt(l.BasePrompt, l.ModelName, l.IsNegative);
                }

                values.Add(text);
            }
            return values;
        }
    }
}
