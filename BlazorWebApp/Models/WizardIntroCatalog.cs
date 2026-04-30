using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BlazorWebApp.Models
{
    /// <summary>
    /// One step of the hardcoded intro scaffold (Subject -> Scenery -> Lighting -> Mood -> Style).
    /// Drives the section prompts handed to the LLM in <see cref="WizardStage.Intro"/>.
    /// </summary>
    public class WizardIntroSection
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("guidance")] public string Guidance { get; set; } = string.Empty;
        [JsonPropertyName("examples")] public List<string> Examples { get; set; } = new();
    }

    internal class WizardIntroFile
    {
        [JsonPropertyName("version")] public int Version { get; set; }
        [JsonPropertyName("sections")] public List<WizardIntroSection> Sections { get; set; } = new();
    }

    /// <summary>
    /// Cached, immutable view of the intro scaffold loaded from <c>Data/wizard_intro.json</c>.
    /// Singleton lifetime; <see cref="LoadAsync"/> is idempotent and thread-safe.
    /// </summary>
    public class WizardIntroCatalog
    {
        private readonly ILogger<WizardIntroCatalog> _logger;
        private readonly SemaphoreSlim _loadGate = new(1, 1);
        private IReadOnlyList<WizardIntroSection>? _sections;
        private int _version;

        public WizardIntroCatalog(ILogger<WizardIntroCatalog> logger)
        {
            _logger = logger;
        }

        /// <summary>Total number of intro steps (matches <c>sections</c> length).</summary>
        public int Count => _sections?.Count ?? 0;

        /// <summary>Schema version from the JSON file. <c>0</c> until <see cref="LoadAsync"/> runs.</summary>
        public int Version => _version;

        /// <summary>All sections in declared order. Empty until loaded.</summary>
        public IReadOnlyList<WizardIntroSection> Sections =>
            _sections ?? (IReadOnlyList<WizardIntroSection>)Array.Empty<WizardIntroSection>();

        /// <summary>
        /// Loads (and caches) the intro catalog. Safe to call multiple times concurrently.
        /// On parse failure, logs and falls back to an empty catalog so callers can short-circuit.
        /// </summary>
        public async Task<IReadOnlyList<WizardIntroSection>> LoadAsync(CancellationToken ct = default)
        {
            if (_sections is { } cached) return cached;
            await _loadGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_sections is { } afterWait) return afterWait;

                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "wizard_intro.json");
                if (!File.Exists(path))
                {
                    _logger.LogWarning("wizard_intro.json not found at {Path}; wizard intro stage will be empty", path);
                    _sections = Array.Empty<WizardIntroSection>();
                    return _sections;
                }

                try
                {
                    using var stream = File.OpenRead(path);
                    var file = await JsonSerializer.DeserializeAsync<WizardIntroFile>(stream, WizardJsonOptions.Compact, ct)
                        .ConfigureAwait(false);
                    var sections = file?.Sections ?? new List<WizardIntroSection>();
                    var sanitized = sections
                        .Where(s => !string.IsNullOrWhiteSpace(s.Id) && !string.IsNullOrWhiteSpace(s.Title))
                        .ToList();
                    _version = file?.Version ?? 0;
                    _sections = new ReadOnlyCollection<WizardIntroSection>(sanitized);
                    _logger.LogInformation("Loaded {Count} wizard intro sections (version {Version})", sanitized.Count, _version);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load wizard_intro.json");
                    _sections = Array.Empty<WizardIntroSection>();
                }

                return _sections!;
            }
            finally
            {
                _loadGate.Release();
            }
        }
    }
}
