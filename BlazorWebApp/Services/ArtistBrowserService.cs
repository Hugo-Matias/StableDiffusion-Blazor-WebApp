using BlazorWebApp.Models;
using ImageMagick;
using System.Text.Json;
using static BlazorWebApp.Models.FragmentKeys;

namespace BlazorWebApp.Services
{
    public class ArtistBrowserService : IArtistBrowserService
    {
        private readonly IStateService _state;
        private readonly IRouterService _router;
        private readonly IWorkflowService _workflowService;
        private readonly ILogger<ArtistBrowserService> _logger;
        private readonly string _wwwrootPath;
        private readonly string _userTagsPath;
        private List<ArtistTag> _artists = new();
        private Dictionary<string, List<string>> _userTags = new();
        private bool _loaded;
        private readonly object _loadLock = new();

        public int TotalCount => EnsureLoaded().Count;
        public ArtistPreviewProgress PreviewProgress { get; } = new();

        public ArtistBrowserService(
            IStateService state,
            IRouterService router,
            IWorkflowService workflowService,
            IWebHostEnvironment env,
            ILogger<ArtistBrowserService> logger)
        {
            _state = state;
            _router = router;
            _workflowService = workflowService;
            _logger = logger;
            _wwwrootPath = env.WebRootPath;
            _userTagsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "artist-user-tags.json");
            LoadUserTagsFromDisk();
        }

        private List<ArtistTag> EnsureLoaded()
        {
            if (_loaded) return _artists;
            lock (_loadLock)
            {
                if (_loaded) return _artists;
                try
                {
                    var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "artists.json");
                    var json = File.ReadAllText(path);
                    _artists = JsonSerializer.Deserialize<List<ArtistTag>>(json) ?? new();
                    _logger.LogInformation("Loaded {Count} artist tags", _artists.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load artist tags from artists.json");
                    _artists = new();
                }
                _loaded = true;
            }
            return _artists;
        }

        public List<ArtistTag> GetArtists(string? search = null, ArtistSortMode sort = ArtistSortMode.PostCount, bool descending = true, bool favoritesOnly = false, int? randomSeed = null, IEnumerable<string>? userTags = null)
        {
            var source = EnsureLoaded().AsEnumerable();

            if (favoritesOnly)
            {
                var favorites = GetFavoritesSet();
                source = source.Where(a => favorites.Contains(a.Tag));
            }

            if (userTags != null && userTags.Any())
            {
                var requiredTags = userTags.ToHashSet(StringComparer.OrdinalIgnoreCase);
                source = source.Where(a =>
                {
                    if (!_userTags.TryGetValue(a.Slug, out var tags)) return false;
                    return requiredTags.All(rt => tags.Contains(rt, StringComparer.OrdinalIgnoreCase));
                });
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                source = source.Where(a =>
                    a.Slug.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    a.Tag.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            source = sort switch
            {
                ArtistSortMode.PostCount => descending
                    ? source.OrderByDescending(a => a.PostCount)
                    : source.OrderBy(a => a.PostCount),
                ArtistSortMode.Name => descending
                    ? source.OrderByDescending(a => a.Slug)
                    : source.OrderBy(a => a.Slug),
                ArtistSortMode.Random => ShuffleWithSeed(source, randomSeed ?? 0),
                _ => source
            };

            return source.ToList();
        }

        public bool IsFavorite(string tag)
        {
            return GetFavoritesSet().Contains(tag);
        }

        public void ToggleFavorite(string tag)
        {
            var favorites = _state.State.Prompts.FavoriteArtists;
            if (favorites.Contains(tag))
                favorites.Remove(tag);
            else
                favorites.Add(tag);
        }

        public List<string> GetFavorites()
        {
            return _state.State.Prompts.FavoriteArtists;
        }

        public string GetPreviewImagePath(ArtistTag artist)
        {
            return Path.Combine(_wwwrootPath, "images", "artists", artist.Shard, $"{artist.Slug}.webp");
        }

        public bool HasPreviewImage(ArtistTag artist)
        {
            return File.Exists(GetPreviewImagePath(artist));
        }

        public string GetBaselineImagePath()
        {
            return Path.Combine(_wwwrootPath, "images", "artists", "baseline.webp");
        }

        public bool HasBaselineImage()
        {
            return File.Exists(GetBaselineImagePath());
        }

        // User tagging
        public List<string> GetUserTags(string slug)
        {
            return _userTags.TryGetValue(slug, out var tags) ? new List<string>(tags) : new();
        }

        public void SetUserTags(string slug, List<string> tags)
        {
            if (tags == null || tags.Count == 0)
                _userTags.Remove(slug);
            else
                _userTags[slug] = tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public void AddUserTag(string slug, string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return;
            tag = tag.Trim().ToLowerInvariant();
            if (!_userTags.ContainsKey(slug))
                _userTags[slug] = new();
            if (!_userTags[slug].Contains(tag, StringComparer.OrdinalIgnoreCase))
                _userTags[slug].Add(tag);
        }

        public void RemoveUserTag(string slug, string tag)
        {
            if (!_userTags.TryGetValue(slug, out var tags)) return;
            tags.RemoveAll(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));
            if (tags.Count == 0)
                _userTags.Remove(slug);
        }

        public List<string> GetAllUserTags()
        {
            return _userTags.Values
                .SelectMany(t => t)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t)
                .ToList();
        }

        public async Task SaveUserTagsAsync()
        {
            try
            {
                var dir = Path.GetDirectoryName(_userTagsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(_userTags, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_userTagsPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save user tags");
            }
        }

        private void LoadUserTagsFromDisk()
        {
            try
            {
                if (File.Exists(_userTagsPath))
                {
                    var json = File.ReadAllText(_userTagsPath);
                    _userTags = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json) ?? new();
                    _logger.LogInformation("Loaded user tags for {Count} artists", _userTags.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load user tags");
                _userTags = new();
            }
        }

        public async Task GenerateBatchPreviewsAsync(List<ArtistTag> artists, int batchSize, bool skipExisting, CancellationToken ct, Func<Task>? onProgress = null)
        {
            PreviewProgress.IsRunning = true;
            PreviewProgress.Current = 0;
            PreviewProgress.Skipped = 0;
            PreviewProgress.Failed = 0;
            var effectiveBatchSize = batchSize <= 0 ? artists.Count : batchSize;
            PreviewProgress.Total = Math.Min(effectiveBatchSize, artists.Count);
            PreviewProgress.CurrentSlug = string.Empty;

            var workflowId = _state.State.Generation.CurrentWorkflowId;
            if (workflowId == null)
            {
                _logger.LogError("No workflow selected for artist preview generation");
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

            // Always generate baseline first
            try
            {
                PreviewProgress.CurrentSlug = "baseline";
                if (onProgress != null)
                    await onProgress.Invoke();

                await GenerateBaselineAsync(workflow);

                if (onProgress != null)
                    await onProgress.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate baseline image");
            }

            if (ct.IsCancellationRequested)
            {
                PreviewProgress.IsRunning = false;
                return;
            }

            var generated = 0;

            try
            {
                foreach (var artist in artists)
                {
                    if (ct.IsCancellationRequested || generated >= effectiveBatchSize)
                        break;

                    PreviewProgress.CurrentSlug = artist.Slug;

                    if (skipExisting && HasPreviewImage(artist))
                    {
                        _logger.LogInformation("Skipping {Slug} (skipExisting={Skip})", artist.Slug, skipExisting);
                        PreviewProgress.Skipped++;
                        if (onProgress != null)
                            await onProgress.Invoke();
                        continue;
                    }

                    _logger.LogInformation("Generating preview for {Slug} (skipExisting={Skip})", artist.Slug, skipExisting);

                    try
                    {
                        var prepared = _state.GenerationParameters.Clone();

                        var promptsFragment = prepared.GetFragment(Fragments.Prompts);
                        if (promptsFragment != null)
                        {
                            var currentPrompt = promptsFragment.GetValueOrDefault<string>(Params.Positive, "") ?? "";
                            var artistPrompt = string.IsNullOrWhiteSpace(currentPrompt)
                                ? artist.Tag
                                : $"{artist.Tag}, {currentPrompt}";
                            promptsFragment.SetValue(Params.Positive, artistPrompt);
                        }

                        var samplerFragment = prepared.GetFragment(Fragments.MainSampler)
                            ?? prepared.GetFragment(Fragments.SamplerAdvanced);
                        if (samplerFragment != null)
                        {
                            var seed = samplerFragment.GetValueOrDefault(Params.Seed, -1L);
                            if (seed == -1 || seed <= 0)
                            {
                                samplerFragment.SetValue(Params.Seed, (long)Random.Shared.Next(0, int.MaxValue));
                            }
                        }

                        var result = await _router.PostGenerationAsync(prepared, workflow);

                        if (result?.Images != null && result.Images.Count > 0)
                        {
                            var imageBytes = Convert.FromBase64String(result.Images[0]);
                            var outputPath = GetPreviewImagePath(artist);

                            var dir = Path.GetDirectoryName(outputPath);
                            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                                Directory.CreateDirectory(dir);

                            using var image = new MagickImage(imageBytes);
                            await image.WriteAsync(outputPath, MagickFormat.WebP);

                            _logger.LogInformation("Generated preview for {Slug}", artist.Slug);
                        }
                        else
                        {
                            PreviewProgress.Failed++;
                            _logger.LogWarning("No image returned for {Slug}", artist.Slug);
                        }
                    }
                    catch (Exception ex)
                    {
                        PreviewProgress.Failed++;
                        _logger.LogError(ex, "Failed to generate preview for {Slug}", artist.Slug);
                    }

                    generated++;
                    PreviewProgress.Current = generated;

                    if (onProgress != null)
                        await onProgress.Invoke();
                }
            }
            finally
            {
                PreviewProgress.IsRunning = false;
                PreviewProgress.CurrentSlug = string.Empty;
                _logger.LogInformation("Preview generation complete: {Generated} generated, {Skipped} skipped, {Failed} failed",
                    generated, PreviewProgress.Skipped, PreviewProgress.Failed);
            }
        }

        private HashSet<string> GetFavoritesSet()
        {
            return _state.State.Prompts.FavoriteArtists.ToHashSet();
        }

        private async Task GenerateBaselineAsync(Workflow workflow)
        {
            var prepared = _state.GenerationParameters.Clone();

            var samplerFragment = prepared.GetFragment(Fragments.MainSampler)
                ?? prepared.GetFragment(Fragments.SamplerAdvanced);
            if (samplerFragment != null)
            {
                var seed = samplerFragment.GetValueOrDefault(Params.Seed, -1L);
                if (seed == -1 || seed <= 0)
                {
                    samplerFragment.SetValue(Params.Seed, (long)Random.Shared.Next(0, int.MaxValue));
                }
            }

            var result = await _router.PostGenerationAsync(prepared, workflow);

            if (result?.Images != null && result.Images.Count > 0)
            {
                var imageBytes = Convert.FromBase64String(result.Images[0]);
                var outputPath = GetBaselineImagePath();

                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using var image = new MagickImage(imageBytes);
                await image.WriteAsync(outputPath, MagickFormat.WebP);

                _logger.LogInformation("Generated baseline image");
            }
            else
            {
                _logger.LogWarning("No image returned for baseline");
            }
        }

        private static IEnumerable<ArtistTag> ShuffleWithSeed(IEnumerable<ArtistTag> source, int seed)
        {
            var rng = new Random(seed);
            return source.OrderBy(_ => rng.Next());
        }
    }
}
