using BlazorWebApp.Models.CharacterCreator;
using System.Text.Json;

namespace BlazorWebApp.Services;

public class CharacterCreatorCatalogService : ICharacterCreatorCatalogService
{
    private const string RegionsFile = "character_regions.json";
    private const string TraitsFile = "character_traits.json";
    private const string PromptRulesFile = "character_prompt_rules.json";
    private const string PresetsFile = "character_presets.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly string _catalogDirectory;
    private readonly ILogger<CharacterCreatorCatalogService> _logger;

    public CharacterCreatorCatalogService(IWebHostEnvironment environment, ILogger<CharacterCreatorCatalogService> logger)
        : this(Path.Combine(environment.ContentRootPath, "Data", "CharacterCreator"), logger)
    {
    }

    public CharacterCreatorCatalogService(string catalogDirectory, ILogger<CharacterCreatorCatalogService> logger)
    {
        _catalogDirectory = catalogDirectory;
        _logger = logger;
    }

    public async Task<CharacterCreatorCatalog> LoadAsync(CancellationToken cancellationToken = default)
    {
        var fallback = CharacterCreatorCatalog.CreateFallback();
        var warnings = new List<CharacterCatalogWarning>();
        var regions = await LoadItemsAsync(RegionsFile, "character-regions", fallback.Regions, IsValidRegion, GetRegionId, warnings, cancellationToken);
        var traits = await LoadItemsAsync(TraitsFile, "character-traits", fallback.TraitDefinitions, IsValidTrait, GetTraitId, warnings, cancellationToken);
        var promptRules = await LoadItemsAsync(PromptRulesFile, "character-prompt-rules", fallback.PromptRules, IsValidPromptRule, GetPromptRuleId, warnings, cancellationToken);
        var presets = await LoadItemsAsync(PresetsFile, "character-presets", fallback.Presets, IsValidPreset, GetPresetId, warnings, cancellationToken);

        return new CharacterCreatorCatalog
        {
            SchemaVersion = 1,
            Regions = regions,
            TraitDefinitions = traits,
            PromptRules = promptRules,
            Presets = presets,
            ConflictRules = ExtractConflictRules(promptRules),
            Warnings = warnings
        };
    }

    private async Task<List<T>> LoadItemsAsync<T>(
        string fileName,
        string expectedCatalogId,
        List<T> fallbackItems,
        Func<T, bool> isValid,
        Func<T, string> getId,
        List<CharacterCatalogWarning> warnings,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(_catalogDirectory, fileName);
        if (!File.Exists(path))
        {
            return fallbackItems;
        }

        CharacterCatalogEnvelope<T>? envelope;
        try
        {
            await using var stream = File.OpenRead(path);
            envelope = await JsonSerializer.DeserializeAsync<CharacterCatalogEnvelope<T>>(stream, JsonOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Character Creator catalog file {FileName} could not be loaded.", fileName);
            warnings.Add(CharacterCatalogWarning.Create(fileName, $"Could not load catalog file: {ex.Message}"));
            return fallbackItems;
        }

        if (envelope is null)
        {
            warnings.Add(CharacterCatalogWarning.Create(fileName, "Catalog file was empty."));
            return fallbackItems;
        }

        if (envelope.SchemaVersion <= 0)
        {
            warnings.Add(CharacterCatalogWarning.Create(fileName, "Catalog schemaVersion must be greater than zero."));
            return fallbackItems;
        }

        if (!string.Equals(envelope.CatalogId, expectedCatalogId, StringComparison.Ordinal))
        {
            warnings.Add(CharacterCatalogWarning.Create(fileName, $"Catalog id '{envelope.CatalogId}' does not match expected id '{expectedCatalogId}'."));
            return fallbackItems;
        }

        var items = new List<T>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in envelope.Items)
        {
            var id = getId(item);
            if (!isValid(item))
            {
                warnings.Add(CharacterCatalogWarning.Create(fileName, "Catalog item is missing required fields.", id));
                continue;
            }

            if (!ids.Add(id))
            {
                warnings.Add(CharacterCatalogWarning.Create(fileName, $"Duplicate item id '{id}' was skipped.", id));
                continue;
            }

            items.Add(item);
        }

        return items.Count == 0 ? fallbackItems : items;
    }

    private static List<CharacterConflictRuleDefinition> ExtractConflictRules(IEnumerable<CharacterPromptRuleDefinition> promptRules)
    {
        return new List<CharacterConflictRuleDefinition>();
    }

    private static bool IsValidRegion(CharacterRegionDefinition item)
        => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Label);

    private static bool IsValidTrait(CharacterTraitDefinition item)
        => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.RegionId) && !string.IsNullOrWhiteSpace(item.Label) && !string.IsNullOrWhiteSpace(item.ValueType);

    private static bool IsValidPromptRule(CharacterPromptRuleDefinition item)
        => !string.IsNullOrWhiteSpace(item.ProfileId) && !string.IsNullOrWhiteSpace(item.Label);

    private static bool IsValidPreset(CharacterPresetDefinition item)
        => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Label) && !string.IsNullOrWhiteSpace(item.PresetType);

    private static string GetRegionId(CharacterRegionDefinition item) => item.Id;
    private static string GetTraitId(CharacterTraitDefinition item) => item.Id;
    private static string GetPromptRuleId(CharacterPromptRuleDefinition item) => item.ProfileId;
    private static string GetPresetId(CharacterPresetDefinition item) => item.Id;
}