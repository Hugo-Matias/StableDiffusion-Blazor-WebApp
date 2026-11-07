using System.Text.Json;
using BlazorWebApp.Data.Dtos;
using Microsoft.Extensions.Logging;
using Sylvan.Data.Csv;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Loads, caches and filters caption records from captions.csv.
    /// Mirrors the lazy-load + in-memory cache pattern used by CsvService.
    /// </summary>
    public class CaptionLibraryService : ICaptionLibraryService
    {
        private readonly ILogger<CaptionLibraryService> _logger;
        private readonly string _csvPath;

        // Lazy in-memory cache of all caption records. Loaded on first access.
        private List<CaptionRecord>? _cachedCaptions;
        private readonly object _cacheLock = new();
        private readonly Random _random = new();

        public int TotalCount => _cachedCaptions?.Count ?? 0;

        public CaptionLibraryService(IWebHostEnvironment env, ILogger<CaptionLibraryService> logger)
        {
            _logger = logger;
            // Resolve CSV path relative to the content root (where Data/ lives).
            _csvPath = Path.Combine(env.ContentRootPath, "Data", "captions.csv");
        }

        #region Loading

        private List<CaptionRecord> LoadCached()
        {
            if (_cachedCaptions != null)
                return _cachedCaptions;

            lock (_cacheLock)
            {
                // Double-check after acquiring lock.
                if (_cachedCaptions != null)
                    return _cachedCaptions;

                _cachedCaptions = LoadFromCsv();
                return _cachedCaptions;
            }
        }

        private List<CaptionRecord> LoadFromCsv()
        {
            try
            {
                if (!File.Exists(_csvPath))
                {
                    _logger.LogWarning("captions.csv not found at {Path}", _csvPath);
                    return new List<CaptionRecord>();
                }

                var captions = new List<CaptionRecord>();
                var options = new CsvDataReaderOptions { HasHeaders = true };
                int rowId = 0;
                int failures = 0;

                using var reader = CsvDataReader.Create(_csvPath, options);
                while (reader.Read())
                {
                    rowId++;
                    try
                    {
                        captions.Add(new CaptionRecord
                        {
                            Id = rowId,
                            Caption = GetStringField(reader, "Caption") ?? string.Empty,
                            AspectRatio = GetStringField(reader, "Aspect Ratio") ?? string.Empty,
                            Nudity = GetStringField(reader, "Nudity") ?? string.Empty,
                            ArtisticQuality = GetDoubleField(reader, "Artistic Quality"),
                            EroticScore = GetDoubleField(reader, "Erotic Score"),
                            PornographicScore = GetDoubleField(reader, "Pornographic Score"),
                            Males = reader.GetInt32(reader.GetOrdinal("Males")),
                            Females = reader.GetInt32(reader.GetOrdinal("Females")),
                            Clothing = ParseClothingArray(GetStringField(reader, "Clothing"))
                        });
                    }
                    catch (Exception ex)
                    {
                        failures++;
                        if (failures <= 5)
                        {
                            _logger.LogWarning(ex, "Failed to parse caption row {RowId}", rowId);
                        }
                        else if (failures == 6)
                        {
                            _logger.LogWarning("Suppressing further row parsing warnings...");
                        }
                    }
                }

                _logger.LogInformation("Loaded {Count} captions from captions.csv", captions.Count);
                return captions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading captions.csv from {Path}", _csvPath);
                return new List<CaptionRecord>();
            }
        }

        private string? GetStringField(CsvDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        private double GetDoubleField(CsvDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? 0.0 : reader.GetDouble(ordinal);
        }

        private List<string> ParseClothingArray(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<string>();

            try
            {
                // The CSV stores clothing as a JSON array string like "['blanket', 'sheet']"
                // which uses single quotes. Normalize to double quotes for JsonDocument.
                var normalized = json.Replace('\'', '"');
                using var doc = JsonDocument.Parse(normalized);
                return doc.RootElement.EnumerateArray()
                    .Select(e => e.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse clothing array: {Json}", json);
                return new List<string>();
            }
        }

        #endregion

        #region Distinct Values

        public Task<List<string>> GetAspectRatiosAsync()
        {
            var captions = LoadCached();
            var ratios = captions
                .Select(c => c.AspectRatio)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct()
                .OrderBy(r => r)
                .ToList();
            return Task.FromResult(ratios);
        }

        public Task<List<string>> GetNudityLevelsAsync()
        {
            var captions = LoadCached();
            var levels = captions
                .Select(c => c.Nudity)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            return Task.FromResult(levels);
        }

        public Task<List<string>> GetClothingItemsAsync()
        {
            var captions = LoadCached();
            var items = captions
                .SelectMany(c => c.Clothing)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct()
                .OrderBy(item => item)
                .ToList();
            return Task.FromResult(items);
        }

        #endregion

        #region Filtering and Search

        public async Task<(List<CaptionRecord> Items, int TotalCount)> SearchAsync(
            CaptionFilter filter, int page = 1, int pageSize = 24)
        {
            var captions = LoadCached();
            var query = ApplyFilters(captions.AsEnumerable(), filter);

            // Apply sorting
            query = ApplySorting(query, filter);

            var totalCount = query.Count();
            var items = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (items, totalCount);
        }

        public async Task<CaptionRecord?> GetRandomCaptionAsync(CaptionFilter? filter = null)
        {
            var captions = LoadCached();
            var filtered = ApplyFilters(captions.AsEnumerable(), filter ?? new CaptionFilter());
            var count = filtered.Count();
            if (count == 0)
                return null;

            var index = _random.Next(0, count);
            // Use Skip to avoid materializing the full list.
            return filtered.Skip(index).FirstOrDefault();
        }

        public async Task<CaptionRecord?> GetByIdAsync(int id)
        {
            var captions = LoadCached();
            return captions.FirstOrDefault(c => c.Id == id);
        }

        #endregion

        #region Filter Logic

        private static IEnumerable<CaptionRecord> ApplyFilters(
            IEnumerable<CaptionRecord> source, CaptionFilter filter)
        {
            var searchText = string.IsNullOrWhiteSpace(filter.SearchText)
                ? null
                : filter.SearchText.ToLowerInvariant();

            var negativeSearchText = string.IsNullOrWhiteSpace(filter.NegativeSearchText)
                ? null
                : filter.NegativeSearchText.ToLowerInvariant();

            var aspectRatio = string.IsNullOrWhiteSpace(filter.AspectRatio)
                ? null
                : filter.AspectRatio;

            var nudity = string.IsNullOrWhiteSpace(filter.Nudity)
                ? null
                : filter.Nudity;

            var clothingTags = (filter.ClothingTags != null && filter.ClothingTags.Count > 0)
                ? filter.ClothingTags.Select(t => t.ToLowerInvariant()).ToHashSet()
                : null;

            foreach (var caption in source)
            {
                // Positive text search
                if (searchText != null && !caption.Caption.ToLowerInvariant().Contains(searchText))
                    continue;

                // Negative text search — exclude captions containing any negative terms (AND logic with positive)
                if (negativeSearchText != null)
                {
                    var captionLower = caption.Caption.ToLowerInvariant();
                    bool matchesNegative = false;
                    foreach (var term in negativeSearchText.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (captionLower.Contains(term))
                        {
                            matchesNegative = true;
                            break;
                        }
                    }
                    if (matchesNegative)
                        continue;
                }

                // Aspect ratio
                if (aspectRatio != null && caption.AspectRatio != aspectRatio)
                    continue;

                // Nudity level
                if (nudity != null && caption.Nudity != nudity)
                    continue;

                // Artistic quality minimum
                if (filter.MinArtisticQuality.HasValue && caption.ArtisticQuality < filter.MinArtisticQuality.Value)
                    continue;

                // Erotic score minimum
                if (filter.MinEroticScore.HasValue && caption.EroticScore < filter.MinEroticScore.Value)
                    continue;

                // Pornographic score maximum
                if (filter.MaxPornographicScore.HasValue && caption.PornographicScore > filter.MaxPornographicScore.Value)
                    continue;

                // Male count exact match
                if (filter.Males.HasValue && caption.Males != filter.Males.Value)
                    continue;

                // Female count exact match
                if (filter.Females.HasValue && caption.Females != filter.Females.Value)
                    continue;

                // Clothing tags multi-select (match entries containing all selected items)
                if (clothingTags != null)
                {
                    var normalizedClothing = caption.Clothing.Select(c => c.ToLowerInvariant()).ToHashSet();
                    bool hasAll = clothingTags.All(t => normalizedClothing.Contains(t));
                    if (!hasAll)
                        continue;
                }

                yield return caption;
            }
        }

        private static IEnumerable<CaptionRecord> ApplySorting(IEnumerable<CaptionRecord> source, CaptionFilter filter)
        {
            return filter.SortBy switch
            {
                CaptionSortBy.Id => source.OrderBy(c => c.Id),
                CaptionSortBy.CaptionLength => source.OrderByDescending(c => c.Caption.Length),
                CaptionSortBy.ArtisticQuality => source.OrderByDescending(c => c.ArtisticQuality),
                CaptionSortBy.EroticScore => source.OrderByDescending(c => c.EroticScore),
                CaptionSortBy.PornographicScore => source.OrderByDescending(c => c.PornographicScore),
                CaptionSortBy.Males => source.OrderByDescending(c => c.Males),
                CaptionSortBy.Females => source.OrderByDescending(c => c.Females),
                CaptionSortBy.Random or _ => source, // Random and Default return as-is (random handled by consumer)
            };
        }

        #endregion
    }
}
