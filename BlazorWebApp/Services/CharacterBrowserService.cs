using System.Text.Json;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using ImageMagick;
using Microsoft.AspNetCore.Hosting;
using static BlazorWebApp.Models.FragmentKeys;

namespace BlazorWebApp.Services
{
    public class CharacterBrowserService : ICharacterBrowserService
    {
        // Known facet values (from animadex facets API)
        public static readonly IReadOnlyList<string> KnownHairColors = new[]
        {
            "black hair", "brown hair", "blonde hair", "white hair", "blue hair", "grey hair",
            "pink hair", "purple hair", "red hair", "green hair", "streaked hair", "two-tone hair",
            "orange hair", "gradient hair", "multicolored hair", "aqua hair", "light brown hair",
            "silver hair", "light blue hair", "split-color hair", "light purple hair",
            "dark blue hair", "light green hair"
        };

        public static readonly IReadOnlyList<string> KnownHairLengths = new[]
        {
            "very short hair", "short hair", "medium hair", "long hair", "very long hair", "absurdly long hair"
        };

        public static readonly IReadOnlyList<string> KnownEyeColors = new[]
        {
            "blue eyes", "red eyes", "brown eyes", "green eyes", "purple eyes", "yellow eyes",
            "pink eyes", "black eyes", "grey eyes", "orange eyes", "aqua eyes",
            "multicolored eyes", "two-tone eyes", "gradient eyes"
        };

        // Maps tag → display label
        public static readonly IReadOnlyDictionary<string, string> GenderTagMap = new Dictionary<string, string>
        {
            ["1girl"] = "Female",
            ["1boy"] = "Male",
            ["no humans"] = "Non-Human",
            ["1other"] = "Ambiguous",
        };

        private readonly IStateService _state;
        private readonly IRouterService _router;
        private readonly IWorkflowService _workflowService;
        private readonly ILogger<CharacterBrowserService> _logger;
        private readonly string _wwwrootPath;

        private List<AnimaCharacter> _allCharacters = new();
        private List<string> _allCopyrights = new();
        private readonly object _loadLock = new();
        private bool _loaded = false;

        public CharacterPreviewProgress PreviewProgress { get; } = new();

        public IReadOnlyList<string> HairColorFacets => KnownHairColors;
        public IReadOnlyList<string> HairLengthFacets => KnownHairLengths;
        public IReadOnlyList<string> EyeColorFacets => KnownEyeColors;
        public IReadOnlyList<string> GenderFacets => GenderTagMap.Keys.ToList();

        public CharacterBrowserService(IStateService state, IRouterService router, IWorkflowService workflowService, IWebHostEnvironment env, ILogger<CharacterBrowserService> logger)
        {
            _state = state;
            _router = router;
            _workflowService = workflowService;
            _logger = logger;
            _wwwrootPath = env.WebRootPath;
        }

        public int TotalCount
        {
            get
            {
                EnsureLoaded();
                return _allCharacters.Count;
            }
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            lock (_loadLock)
            {
                if (_loaded) return;
                LoadCharacters();
                _loaded = true;
            }
        }

        private void LoadCharacters()
        {
            var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "anima_characters.json");
            if (!File.Exists(dataPath))
            {
                _logger.LogWarning("anima_characters.json not found at {Path}", dataPath);
                return;
            }

            try
            {
                var json = File.ReadAllText(dataPath);
                var raw = JsonSerializer.Deserialize<List<AnimaCharacter>>(json);
                if (raw == null) return;

                var hairColorSet = new HashSet<string>(KnownHairColors, StringComparer.OrdinalIgnoreCase);
                var hairLengthSet = new HashSet<string>(KnownHairLengths, StringComparer.OrdinalIgnoreCase);
                var eyeColorSet = new HashSet<string>(KnownEyeColors, StringComparer.OrdinalIgnoreCase);
                var genderSet = new HashSet<string>(GenderTagMap.Keys, StringComparer.OrdinalIgnoreCase);

                foreach (var c in raw)
                {
                    // Tags is a List<string> from JSON; scan each known facet phrase against the list.
                    c.HairColor = hairColorSet.FirstOrDefault(h => c.Tags.Contains(h, StringComparer.OrdinalIgnoreCase));
                    c.HairLength = hairLengthSet.FirstOrDefault(h => c.Tags.Contains(h, StringComparer.OrdinalIgnoreCase));
                    c.EyeColor = eyeColorSet.FirstOrDefault(e => c.Tags.Contains(e, StringComparer.OrdinalIgnoreCase));
                    c.Gender = genderSet.FirstOrDefault(g => c.Tags.Contains(g, StringComparer.OrdinalIgnoreCase));
                }

                _allCharacters = raw;
                _allCopyrights = raw
                    .Where(c => !string.IsNullOrWhiteSpace(c.CopyrightName))
                    .GroupBy(c => c.CopyrightName, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .ToList();

                _logger.LogInformation("Loaded {Count} characters from anima_characters.json", _allCharacters.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load anima_characters.json");
            }
        }

        public List<AnimaCharacter> GetCharacters(
            string? search = null,
            CharacterSortMode sort = CharacterSortMode.PostCount,
            bool descending = true,
            bool favoritesOnly = false,
            int? randomSeed = null,
            string? copyright = null,
            string? hairColor = null,
            string? hairLength = null,
            string? eyeColor = null,
            string? gender = null)
        {
            EnsureLoaded();

            var favorites = _state.State.Prompts.FavoriteCharacters.ToHashSet(StringComparer.OrdinalIgnoreCase);

            IEnumerable<AnimaCharacter> query = _allCharacters;

            if (favoritesOnly)
                query = query.Where(c => favorites.Contains(c.Slug));

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(c =>
                    c.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    c.CopyrightName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    c.Trigger.Contains(s, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(copyright))
                query = query.Where(c => c.CopyrightName.Equals(copyright, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(hairColor))
                query = query.Where(c => c.HairColor != null &&
                    c.HairColor.Equals(hairColor, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(hairLength))
                query = query.Where(c => c.HairLength != null &&
                    c.HairLength.Equals(hairLength, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(eyeColor))
                query = query.Where(c => c.EyeColor != null &&
                    c.EyeColor.Equals(eyeColor, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(gender))
                query = query.Where(c => c.Gender != null &&
                    c.Gender.Equals(gender, StringComparison.OrdinalIgnoreCase));

            query = sort switch
            {
                CharacterSortMode.Name => descending ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
                CharacterSortMode.Copyright => descending ? query.OrderByDescending(c => c.CopyrightName) : query.OrderBy(c => c.CopyrightName),
                CharacterSortMode.Random => query.OrderBy(_ => randomSeed.HasValue
                                                  ? new Random(randomSeed.Value).Next()
                                                  : Random.Shared.Next()),
                _ => descending ? query.OrderByDescending(c => c.Count) : query.OrderBy(c => c.Count),
            };

            return query.ToList();
        }

        public IReadOnlyList<string> GetAllCopyrights()
        {
            EnsureLoaded();
            return _allCopyrights;
        }

        public bool IsFavorite(string slug) =>
            _state.State.Prompts.FavoriteCharacters.Contains(slug, StringComparer.OrdinalIgnoreCase);

        public void ToggleFavorite(string slug)
        {
            var favs = _state.State.Prompts.FavoriteCharacters;
            if (favs.Contains(slug, StringComparer.OrdinalIgnoreCase))
                favs.RemoveAll(f => f.Equals(slug, StringComparison.OrdinalIgnoreCase));
            else
                favs.Add(slug);
            _ = _state.SaveState();
        }

        public List<string> GetFavorites()
        {
            EnsureLoaded();
            return _state.State.Prompts.FavoriteCharacters;
        }

        public string GetPreviewImagePath(AnimaCharacter character) =>
            Path.Combine(_wwwrootPath, "images", "characters", $"{character.Slug}.webp");

        public bool HasPreviewImage(AnimaCharacter character) =>
            File.Exists(GetPreviewImagePath(character));

        public async Task GenerateBatchPreviewsAsync(
            List<AnimaCharacter> characters,
            int batchSize,
            bool skipExisting,
            bool useTags,
            CancellationToken ct,
            Func<Task>? onProgress = null)
        {
            PreviewProgress.IsRunning = true;
            PreviewProgress.Current = 0;
            PreviewProgress.Skipped = 0;
            PreviewProgress.Failed = 0;
            var effectiveBatch = batchSize <= 0 ? characters.Count : batchSize;
            PreviewProgress.Total = Math.Min(effectiveBatch, characters.Count);
            PreviewProgress.CurrentSlug = string.Empty;

            var workflowId = _state.State.Generation.CurrentWorkflowId;
            if (workflowId == null)
            {
                _logger.LogError("No workflow selected for character preview generation");
                PreviewProgress.IsRunning = false;
                return;
            }

            var workflow = _workflowService.GetWorkflowById(workflowId.Value);
            if (workflow == null)
            {
                _logger.LogError("Workflow {Id} not found", workflowId);
                PreviewProgress.IsRunning = false;
                return;
            }

            var generated = 0;

            try
            {
                foreach (var character in characters)
                {
                    if (ct.IsCancellationRequested || generated >= effectiveBatch)
                        break;

                    PreviewProgress.CurrentSlug = character.Slug;

                    if (skipExisting && HasPreviewImage(character))
                    {
                        PreviewProgress.Skipped++;
                        if (onProgress != null) await onProgress.Invoke();
                        continue;
                    }

                    try
                    {
                        var prepared = _state.GenerationParameters.Clone();

                        // Baseline = trigger + series; tags are appended only when useTags is on.
                        var promptsFrag = prepared.GetFragment(Fragments.Prompts);
                        if (promptsFrag != null)
                        {
                            var trigger  = character.Trigger.EscapeParenthesis();
                            var series   = !string.IsNullOrWhiteSpace(character.Copyright)
                                               ? character.Copyright.EscapeParenthesis()
                                               : null;
                            var baseline = series != null ? $"{trigger}, {series}" : trigger;
                            var token    = useTags && character.Tags.Count > 0
                                               ? $"{baseline}, {string.Join(", ", character.Tags.Select(t => t.EscapeParenthesis()))}"
                                               : baseline;
                            var current = promptsFrag.GetValueOrDefault<string>(Params.Positive, "") ?? "";
                            promptsFrag.SetValue(Params.Positive, current.InjectPreviewTag(token));
                        }

                        // Randomize seed if unset
                        var samplerFrag = prepared.GetFragment(Fragments.MainSampler)
                            ?? prepared.GetFragment(Fragments.SamplerAdvanced);
                        if (samplerFrag != null)
                        {
                            var seed = samplerFrag.GetValueOrDefault(Params.Seed, -1L);
                            if (seed <= 0)
                                samplerFrag.SetValue(Params.Seed, (long)Random.Shared.Next(0, int.MaxValue));
                        }

                        var result = await _router.PostGenerationAsync(prepared, workflow);

                        if (result?.Images != null && result.Images.Count > 0)
                        {
                            var imageBytes = Convert.FromBase64String(result.Images[0]);
                            var outputPath = GetPreviewImagePath(character);

                            var dir = Path.GetDirectoryName(outputPath);
                            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                                Directory.CreateDirectory(dir);

                            using var image = new MagickImage(imageBytes);
                            await image.WriteAsync(outputPath, MagickFormat.WebP);
                        }
                        else
                        {
                            PreviewProgress.Failed++;
                            _logger.LogWarning("No image returned for character {Slug}", character.Slug);
                        }
                    }
                    catch (Exception ex)
                    {
                        PreviewProgress.Failed++;
                        _logger.LogError(ex, "Failed to generate preview for {Slug}", character.Slug);
                    }

                    generated++;
                    PreviewProgress.Current = generated;
                    if (onProgress != null) await onProgress.Invoke();
                }
            }
            finally
            {
                PreviewProgress.IsRunning = false;
                PreviewProgress.CurrentSlug = string.Empty;
                _logger.LogInformation(
                    "Character preview generation complete: {Generated} generated, {Skipped} skipped, {Failed} failed",
                    generated, PreviewProgress.Skipped, PreviewProgress.Failed);
            }
        }
    }
}
