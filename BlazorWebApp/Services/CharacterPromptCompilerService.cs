using BlazorWebApp.Models.CharacterCreator;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services;

public sealed class CharacterPromptCompilerService : ICharacterPromptCompilerService
{
    private const string DefaultProfileId = "identity_plus_wardrobe";

    public CharacterPromptCompilation Compile(CharacterBody body, CharacterCreatorCatalog catalog, CharacterPromptCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(catalog);
        options ??= new CharacterPromptCompileOptions();

        var profile = ResolveProfile(body, options.ProfileId);
        var profileId = profile?.Id ?? options.ProfileId ?? DefaultProfileId;
        var rule = catalog.PromptRules.FirstOrDefault(rule => string.Equals(rule.ProfileId, profileId, StringComparison.Ordinal));
        var detailBudget = options.DetailBudgetOverride ?? profile?.DetailBudget ?? ParseBudget(rule?.DetailBudget) ?? CharacterPromptDetailBudget.Balanced;
        var warnings = new List<string>();

        var candidates = CollectCandidates(body, catalog, options, warnings);
        candidates = ResolveConflicts(candidates, catalog, warnings);
        candidates = OrderAndBudget(candidates, catalog, rule, detailBudget).ToList();

        var groups = BuildGroups(body, catalog, options, candidates);
        var positiveTemplate = ResolvePositiveTemplate(profileId, rule);
        var negativeTemplate = ResolveNegativeTemplate(profileId, rule);
        var positivePrompt = profileId == "negative_guard" ? string.Empty : RenderTemplate(positiveTemplate, groups);
        var negativePrompt = RenderTemplate(negativeTemplate, groups);
        var fragments = BuildFragments(groups);

        return new CharacterPromptCompilation
        {
            ProfileId = profileId,
            DetailBudget = detailBudget,
            PositivePrompt = positivePrompt,
            NegativePrompt = negativePrompt,
            ComposedPrompt = options.ExistingPrompt is null ? null : ComposePrompt(options.ExistingPrompt, positivePrompt, options.CompositionMode),
            Fragments = fragments,
            Warnings = warnings
        };
    }

    public string ComposePrompt(string? existingPrompt, string compiledPrompt, CharacterPromptCompositionMode mode)
    {
        var existing = CleanupPrompt(existingPrompt ?? string.Empty);
        var compiled = CleanupPrompt(compiledPrompt ?? string.Empty);
        if (string.IsNullOrWhiteSpace(compiled))
        {
            return existing;
        }

        if (string.IsNullOrWhiteSpace(existing) || mode == CharacterPromptCompositionMode.Replace)
        {
            return compiled;
        }

        return mode == CharacterPromptCompositionMode.Prepend
            ? CleanupPrompt($"{compiled}, {existing}")
            : CleanupPrompt($"{existing}, {compiled}");
    }

    private static CharacterPromptProfile? ResolveProfile(CharacterBody body, string? profileId)
    {
        if (!string.IsNullOrWhiteSpace(profileId))
        {
            return body.PromptProfiles.FirstOrDefault(profile => string.Equals(profile.Id, profileId, StringComparison.Ordinal));
        }

        return body.PromptProfiles.FirstOrDefault(profile => profile.IsDefault)
            ?? body.PromptProfiles.FirstOrDefault(profile => string.Equals(profile.Id, DefaultProfileId, StringComparison.Ordinal))
            ?? body.PromptProfiles.FirstOrDefault();
    }

    private static CharacterPromptDetailBudget? ParseBudget(string? value)
    {
        return Enum.TryParse<CharacterPromptDetailBudget>(value, ignoreCase: true, out var budget) ? budget : null;
    }

    private static List<CandidateTerm> CollectCandidates(CharacterBody body, CharacterCreatorCatalog catalog, CharacterPromptCompileOptions options, List<string> warnings)
    {
        var traitDefinitions = catalog.TraitDefinitions.ToDictionary(trait => trait.Id, StringComparer.Ordinal);
        var regionDefinitions = catalog.Regions.ToDictionary(region => region.Id, StringComparer.Ordinal);
        var candidates = new List<CandidateTerm>();

        foreach (var region in body.Regions.Values)
        {
            foreach (var selectedTrait in region.SelectedTraits.Values)
            {
                var candidate = CreateCandidate(region.RegionId, selectedTrait.TraitId, selectedTrait.Value, selectedTrait.Locked, traitDefinitions, regionDefinitions, warnings);
                if (candidate is not null)
                {
                    candidates.Add(candidate);
                }
            }

            if (!string.IsNullOrWhiteSpace(region.FreeformNotes))
            {
                candidates.Add(CreateFreeformCandidate(region.RegionId, region.FreeformNotes, regionDefinitions));
            }
        }

        var wardrobe = ResolveWardrobe(body, options.WardrobeId);
        if (wardrobe is not null)
        {
            foreach (var assignment in wardrobe.TraitValues)
            {
                var candidate = CreateCandidate(assignment.RegionId, assignment.TraitId, assignment.Value, assignment.Locked, traitDefinitions, regionDefinitions, warnings, CharacterPromptFragmentKind.Wardrobe);
                if (candidate is not null)
                {
                    candidates.Add(candidate);
                }
            }
        }

        return Deduplicate(candidates, warnings);
    }

    private static CharacterWardrobePreset? ResolveWardrobe(CharacterBody body, string? wardrobeId)
    {
        if (!string.IsNullOrWhiteSpace(wardrobeId))
        {
            return body.Wardrobes.FirstOrDefault(wardrobe => string.Equals(wardrobe.Id, wardrobeId, StringComparison.Ordinal));
        }

        return body.Wardrobes.FirstOrDefault();
    }

    private static CandidateTerm? CreateCandidate(
        string regionId,
        string traitId,
        string value,
        bool locked,
        IReadOnlyDictionary<string, CharacterTraitDefinition> traitDefinitions,
        IReadOnlyDictionary<string, CharacterRegionDefinition> regionDefinitions,
        List<string> warnings,
        CharacterPromptFragmentKind? forcedKind = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        traitDefinitions.TryGetValue(traitId, out var traitDefinition);
        regionDefinitions.TryGetValue(regionId, out var regionDefinition);
        var promptTerm = ResolvePromptTerm(traitDefinition, value);
        if (string.IsNullOrWhiteSpace(promptTerm))
        {
            warnings.Add($"Trait '{traitId}' in region '{regionId}' did not produce prompt text.");
            return null;
        }

        var kind = forcedKind ?? ResolveKind(regionDefinition, traitDefinition);
        return new CandidateTerm(
            RegionId: regionId,
            TraitId: traitId,
            Value: value.Trim(),
            Text: promptTerm,
            Locked: locked,
            Kind: kind,
            RegionPriority: regionDefinition?.PromptPriority ?? int.MaxValue,
            RegionSortOrder: regionDefinition?.SortOrder ?? int.MaxValue);
    }

    private static CandidateTerm CreateFreeformCandidate(string regionId, string notes, IReadOnlyDictionary<string, CharacterRegionDefinition> regionDefinitions)
    {
        regionDefinitions.TryGetValue(regionId, out var regionDefinition);
        return new CandidateTerm(
            RegionId: regionId,
            TraitId: "__notes",
            Value: notes.Trim(),
            Text: notes.Trim(),
            Locked: false,
            Kind: ResolveKind(regionDefinition, null),
            RegionPriority: regionDefinition?.PromptPriority ?? int.MaxValue,
            RegionSortOrder: regionDefinition?.SortOrder ?? int.MaxValue);
    }

    private static string ResolvePromptTerm(CharacterTraitDefinition? traitDefinition, string value)
    {
        var trimmedValue = value.Trim();
        if (traitDefinition is null)
        {
            return NormalizePhrase(trimmedValue);
        }

        var option = traitDefinition.Options.FirstOrDefault(option =>
            string.Equals(option.Id, trimmedValue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(option.Label, trimmedValue, StringComparison.OrdinalIgnoreCase));

        if (option is not null)
        {
            return string.IsNullOrWhiteSpace(option.Prompt) ? option.Label.Trim() : option.Prompt.Trim();
        }

        return NormalizePhrase(trimmedValue);
    }

    private static string NormalizePhrase(string value)
    {
        return value.Replace('_', ' ').Trim();
    }

    private static CharacterPromptFragmentKind ResolveKind(CharacterRegionDefinition? regionDefinition, CharacterTraitDefinition? traitDefinition)
    {
        var scope = traitDefinition?.Scope ?? regionDefinition?.Group ?? string.Empty;
        if (string.Equals(scope, "wardrobe", StringComparison.OrdinalIgnoreCase))
        {
            return CharacterPromptFragmentKind.Wardrobe;
        }

        if (string.Equals(scope, "expression", StringComparison.OrdinalIgnoreCase))
        {
            return CharacterPromptFragmentKind.Expression;
        }

        return CharacterPromptFragmentKind.Identity;
    }

    private static List<CandidateTerm> Deduplicate(IEnumerable<CandidateTerm> candidates, List<string> warnings)
    {
        var result = new List<CandidateTerm>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (seen.Add(candidate.Text))
            {
                result.Add(candidate);
            }
            else
            {
                warnings.Add($"Duplicate prompt term '{candidate.Text}' was skipped.");
            }
        }

        return result;
    }

    private static List<CandidateTerm> ResolveConflicts(List<CandidateTerm> candidates, CharacterCreatorCatalog catalog, List<string> warnings)
    {
        var remaining = candidates.ToList();
        foreach (var conflict in catalog.ConflictRules.Where(rule => rule.Items.Count > 1))
        {
            var matches = remaining.Where(candidate => conflict.Items.Any(item => MatchesConflictItem(candidate, item))).ToList();
            if (matches.Count <= 1)
            {
                continue;
            }

            var lockedMatches = matches.Where(match => match.Locked).ToList();
            var keep = lockedMatches.Count == 1
                ? lockedMatches[0]
                : matches.OrderBy(match => match.RegionPriority).ThenBy(match => match.RegionSortOrder).ThenBy(match => match.TraitId, StringComparer.Ordinal).First();
            foreach (var remove in matches.Where(match => !ReferenceEquals(match, keep)))
            {
                remaining.Remove(remove);
            }

            warnings.Add($"Conflict '{conflict.Id}' kept '{keep.Text}' and skipped {matches.Count - 1} conflicting term{(matches.Count == 2 ? string.Empty : "s")}.");
        }

        return remaining;
    }

    private static bool MatchesConflictItem(CandidateTerm candidate, string item)
    {
        var normalized = item.Trim();
        return string.Equals(candidate.TraitId, normalized, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Value, normalized, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Text, normalized, StringComparison.OrdinalIgnoreCase)
            || string.Equals($"{candidate.TraitId}:{candidate.Value}", normalized, StringComparison.OrdinalIgnoreCase)
            || string.Equals($"{candidate.RegionId}:{candidate.TraitId}", normalized, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<CandidateTerm> OrderAndBudget(
        IEnumerable<CandidateTerm> candidates,
        CharacterCreatorCatalog catalog,
        CharacterPromptRuleDefinition? rule,
        CharacterPromptDetailBudget detailBudget)
    {
        var ordered = candidates
            .OrderBy(candidate => ResolveRegionOrderIndex(candidate, catalog, rule))
            .ThenBy(candidate => candidate.RegionPriority)
            .ThenBy(candidate => candidate.RegionSortOrder)
            .ThenBy(candidate => candidate.TraitId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Text, StringComparer.Ordinal)
            .ToList();

        return ordered.Take(GetBudgetLimit(detailBudget));
    }

    private static int ResolveRegionOrderIndex(CandidateTerm candidate, CharacterCreatorCatalog catalog, CharacterPromptRuleDefinition? rule)
    {
        if (rule?.RegionOrder.Count > 0 != true)
        {
            return int.MaxValue;
        }

        for (var index = 0; index < rule.RegionOrder.Count; index++)
        {
            var item = rule.RegionOrder[index];
            if (string.Equals(candidate.RegionId, item, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.Kind.ToString(), item, StringComparison.OrdinalIgnoreCase)
                || string.Equals(GetPromptGroupName(candidate, catalog), item, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    private static int GetBudgetLimit(CharacterPromptDetailBudget detailBudget)
    {
        return detailBudget switch
        {
            CharacterPromptDetailBudget.Concise => 6,
            CharacterPromptDetailBudget.Balanced => 12,
            CharacterPromptDetailBudget.Rich => 24,
            _ => 12
        };
    }

    private static Dictionary<string, PromptGroup> BuildGroups(CharacterBody body, CharacterCreatorCatalog catalog, CharacterPromptCompileOptions options, IReadOnlyList<CandidateTerm> candidates)
    {
        var groups = new Dictionary<string, PromptGroup>(StringComparer.Ordinal)
        {
            ["identity"] = new(CharacterPromptFragmentKind.Identity, BuildIdentityCore(body.Identity), []),
            ["body"] = CreateGroup(CharacterPromptFragmentKind.Identity, candidates.Where(candidate => GetPromptGroupName(candidate, catalog) == "body")),
            ["head"] = CreateGroup(CharacterPromptFragmentKind.Identity, candidates.Where(candidate => GetPromptGroupName(candidate, catalog) == "head")),
            ["wardrobe"] = CreateGroup(CharacterPromptFragmentKind.Wardrobe, candidates.Where(candidate => candidate.Kind == CharacterPromptFragmentKind.Wardrobe)),
            ["marks"] = CreateGroup(CharacterPromptFragmentKind.Identity, candidates.Where(candidate => GetPromptGroupName(candidate, catalog) == "marks")),
            ["expression"] = CreateGroup(CharacterPromptFragmentKind.Expression, candidates.Where(candidate => candidate.Kind == CharacterPromptFragmentKind.Expression)),
            ["application"] = new(CharacterPromptFragmentKind.Application, BuildApplicationText(options.ApplicationContext), []),
            ["negative_guard"] = new(CharacterPromptFragmentKind.NegativeGuard, BuildNegativeGuard(options.ApplicationContext), [])
        };

        return groups;
    }

    private static PromptGroup CreateGroup(CharacterPromptFragmentKind kind, IEnumerable<CandidateTerm> candidates)
    {
        var list = candidates.ToList();
        return new PromptGroup(kind, JoinTerms(list.Select(candidate => candidate.Text)), list.Select(candidate => candidate.SourceKey).ToList());
    }

    private static string BuildIdentityCore(CharacterIdentity identity)
    {
        var descriptor = JoinDescriptor([identity.Species, identity.Archetype]);
        var summary = CleanupPrompt(identity.Summary);
        if (!string.IsNullOrWhiteSpace(identity.DisplayName) && !string.IsNullOrWhiteSpace(descriptor))
        {
            return CleanupPrompt($"{identity.DisplayName.Trim()}, {WithArticle(descriptor)}{(string.IsNullOrWhiteSpace(summary) ? string.Empty : ", " + summary)}");
        }

        if (!string.IsNullOrWhiteSpace(identity.DisplayName))
        {
            return CleanupPrompt($"{identity.DisplayName.Trim()}{(string.IsNullOrWhiteSpace(summary) ? string.Empty : ", " + summary)}");
        }

        if (!string.IsNullOrWhiteSpace(descriptor))
        {
            return CleanupPrompt($"{WithArticle(descriptor)}{(string.IsNullOrWhiteSpace(summary) ? string.Empty : ", " + summary)}");
        }

        return string.IsNullOrWhiteSpace(summary) ? "the character" : summary;
    }

    private static string WithArticle(string descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor))
        {
            return string.Empty;
        }

        var first = char.ToLowerInvariant(descriptor.Trim()[0]);
        var article = "aeiou".Contains(first) ? "an" : "a";
        return $"{article} {descriptor.Trim()}";
    }

    private static string JoinDescriptor(IEnumerable<string?> parts)
    {
        return CleanupPrompt(string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim())));
    }

    private static string GetPromptGroupName(CandidateTerm candidate, CharacterCreatorCatalog catalog)
    {
        if (candidate.Kind == CharacterPromptFragmentKind.Wardrobe)
        {
            return "wardrobe";
        }

        if (candidate.Kind == CharacterPromptFragmentKind.Expression)
        {
            return "expression";
        }

        if (candidate.RegionId is "marks" or "head_marks")
        {
            return "marks";
        }

        if (candidate.RegionId is "head" or "face_shape" or "eyes" or "brows" or "nose" or "mouth" or "ears" or "hair" or "makeup")
        {
            return "head";
        }

        var region = catalog.Regions.FirstOrDefault(region => string.Equals(region.Id, candidate.RegionId, StringComparison.Ordinal));
        if (string.Equals(region?.ParentId, "head", StringComparison.OrdinalIgnoreCase))
        {
            return "head";
        }

        return "body";
    }

    private static string BuildApplicationText(CharacterPromptApplicationContext? context)
    {
        if (context is null)
        {
            return string.Empty;
        }

        return JoinTerms([context.ExpressionOverride, context.Pose, context.Camera, context.Scene, context.Style, context.EditIntent, context.AdditionalPositive]);
    }

    private static string BuildNegativeGuard(CharacterPromptApplicationContext? context)
    {
        return JoinTerms([
            "inconsistent identity",
            "different face",
            "changed hairstyle",
            "distorted anatomy",
            context?.AdditionalNegative ?? string.Empty
        ]);
    }

    private static List<CharacterPromptFragment> BuildFragments(Dictionary<string, PromptGroup> groups)
    {
        return groups.Values
            .Where(group => !string.IsNullOrWhiteSpace(group.Text))
            .GroupBy(group => group.Kind)
            .Select(group => new CharacterPromptFragment
            {
                Kind = group.Key,
                Text = JoinSentences(group.Select(item => item.Text)),
                SourceKeys = group.SelectMany(item => item.SourceKeys).Distinct(StringComparer.Ordinal).ToList()
            })
            .ToList();
    }

    private static string ResolvePositiveTemplate(string profileId, CharacterPromptRuleDefinition? rule)
    {
        if (!string.IsNullOrWhiteSpace(rule?.Template))
        {
            return rule.Template;
        }

        return profileId switch
        {
            "identity_only" => "{identity}. {body}. {head}. {marks}",
            "image_edit_preservation" => "{identity}. {body}. {head}. {wardrobe}. {marks}. Preserve the same character identity",
            "rich_portrait" => "{identity}. {body}. {head}. {wardrobe}. {marks}. {expression}. {application}",
            _ => "{identity}. {body}. {head}. {wardrobe}. {marks}. {expression}. {application}"
        };
    }

    private static string ResolveNegativeTemplate(string profileId, CharacterPromptRuleDefinition? rule)
    {
        if (!string.IsNullOrWhiteSpace(rule?.NegativeTemplate))
        {
            return rule.NegativeTemplate;
        }

        return profileId == "negative_guard" ? "{negative_guard}" : string.Empty;
    }

    private static string RenderTemplate(string template, IReadOnlyDictionary<string, PromptGroup> groups)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        var rendered = template;
        foreach (var group in groups)
        {
            rendered = rendered.Replace("{" + group.Key + "}", group.Value.Text, StringComparison.Ordinal);
        }

        rendered = Regex.Replace(rendered, "\\{[a-zA-Z0-9_]+\\}", string.Empty);
        return CleanupPrompt(rendered);
    }

    private static string JoinTerms(IEnumerable<string?> terms)
    {
        return CleanupPrompt(string.Join(", ", terms.Where(term => !string.IsNullOrWhiteSpace(term)).Select(term => term!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)));
    }

    private static string JoinSentences(IEnumerable<string> sentences)
    {
        return CleanupPrompt(string.Join(". ", sentences.Where(sentence => !string.IsNullOrWhiteSpace(sentence)).Select(sentence => sentence.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)));
    }

    private static string CleanupPrompt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = Regex.Replace(value, "\\s+", " ").Trim();
        cleaned = Regex.Replace(cleaned, "\\s+([,.])", "$1");
        cleaned = Regex.Replace(cleaned, "([,.]){2,}", "$1");
        cleaned = Regex.Replace(cleaned, "(^|[.])\\s*[,]+\\s*", "$1 ");
        cleaned = Regex.Replace(cleaned, "(\\.\\s*)+", ". ");
        cleaned = cleaned.Trim(' ', ',', '.');
        return cleaned;
    }

    private sealed record CandidateTerm(
        string RegionId,
        string TraitId,
        string Value,
        string Text,
        bool Locked,
        CharacterPromptFragmentKind Kind,
        int RegionPriority,
        int RegionSortOrder)
    {
        public string SourceKey => $"{RegionId}:{TraitId}";
    }

    private sealed record PromptGroup(CharacterPromptFragmentKind Kind, string Text, IReadOnlyList<string> SourceKeys);
}