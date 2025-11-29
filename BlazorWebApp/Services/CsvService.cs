using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using Sylvan.Data;
using Sylvan.Data.Csv;

namespace BlazorWebApp.Services
{
    public class CsvService
    {
        private readonly ManagerService _m;
        private readonly IConfiguration _configuration;
        private readonly CacheService _cacheService;
        private readonly Schema _schema;
        private readonly CsvDataReaderOptions _options;
        private readonly string _path;
        private readonly string _fileName;

        public CsvService(ManagerService m, IConfiguration configuration, CacheService cacheService)
        {
            _m = m;
            _configuration = configuration;
            _cacheService = cacheService;
            _schema = Schema.Parse("Name,Color,Uses,Aliases");
            _options = new CsvDataReaderOptions() { Schema = new CsvSchema(_schema), HasHeaders = false };

            if (_m.IsWebuiUp)
            {
                _path = Path.Join(_m.CmdFlags.BaseDir, @"extensions\a1111-sd-webui-tagcomplete\tags\danbooru.csv");
            }
            else if (_m.IsComfyUIUp)
            {
                _path = Path.Join(_configuration["ComfyUIPath"], "danbooru.csv");
            }
            else { _path = ""; }

            _fileName = !string.IsNullOrWhiteSpace(_path) ? Path.GetFileNameWithoutExtension(_path) : string.Empty;
        }

        public async Task<IEnumerable<Tag>> SearchTags(string searchText, bool enableFuzzy = true)
        {
            if (string.IsNullOrWhiteSpace(_path) || !File.Exists(_path))
                return Enumerable.Empty<Tag>();

            using var reader = CsvDataReader.Create(_path, _options);
            searchText = searchText.Replace(" ", "_");

            var recentTags = _cacheService.GetRecentTags(10);
            var tags = reader.GetRecords<CsvTag>().Select(csvTag => new Tag
            {
                Name = csvTag.Name,
                Color = csvTag.Color,
                Uses = csvTag.Uses,
                Aliases = csvTag.Aliases,
                Source = _fileName
            }).ToList();

            List<Tag> result = new();

            foreach (var tag in tags)
            {
                var parsed = Parser.ParseCsvTag(tag);
                parsed.Source = _fileName;

                // Calculate fuzzy score
                if (enableFuzzy)
                {
                    parsed.FuzzyScore = CalculateFuzzyScore(searchText, tag.Name, tag.Aliases, recentTags.Contains(tag.Name));

                    if (parsed.FuzzyScore == 0) continue; // Skip if no match
                }
                else
                {
                    // Standard contains matching (fallback)
                    if (!tag.Name.Contains(searchText, StringComparison.InvariantCultureIgnoreCase) &&
                        !(tag.Aliases?.Contains(searchText, StringComparison.InvariantCultureIgnoreCase) ?? false))
                        continue;

                    parsed.FuzzyScore = tag.Name.StartsWith(searchText, StringComparison.InvariantCultureIgnoreCase) ? 500 : 250;
                }

                // Get local usage
                var localUsage = await _cacheService.GetLocalTagUsageCount(tag.Name);
                parsed.LocalUses = localUsage;
                parsed.IsRecentlyUsed = recentTags.Contains(tag.Name);

                result.Add(parsed);
            }

            // Multi-tier sorting:
            // 1. Recently used tags first
            // 2. Then by local usage
            // 3. Then by fuzzy score
            // 4. Finally by global popularity
            return result
                .OrderByDescending(t => t.IsRecentlyUsed ? 1 : 0)
                .ThenByDescending(t => t.LocalUses)
                .ThenByDescending(t => t.FuzzyScore)
                .ThenByDescending(t => t.Uses);
        }

        private int CalculateFuzzyScore(string searchText, string tagName, string aliases, bool isRecent)
        {
            int score = _cacheService.CalculateFuzzyScore(searchText, tagName);

            // Check aliases
            if (!string.IsNullOrWhiteSpace(aliases) && score < 500)
            {
                var aliasesList = aliases.ToLowerInvariant().Replace("\"", "").Split(',');
                foreach (var alias in aliasesList)
                {
                    int aliasScore = _cacheService.CalculateFuzzyScore(searchText, alias.Trim());
                    score = Math.Max(score, aliasScore - 50); // Slight penalty for alias match
                }
            }

            // Boost recently used tags
            if (isRecent) score += 200;

            return score;
        }

        public Tag? GetTag(string name)
        {
            if (string.IsNullOrWhiteSpace(_path) || !File.Exists(_path))
                return null;

            using var reader = CsvDataReader.Create(_path, _options);
            name = name.Replace(" ", "_");
            return reader.GetRecords<CsvTag>().Select(csvTag => new Tag
            {
                Name = csvTag.Name,
                Color = csvTag.Color,
                Uses = csvTag.Uses,
                Aliases = csvTag.Aliases,
                Source = _fileName
            }).FirstOrDefault(t => t.Name.Equals(name));
        }

        public bool CheckTagExists(string name)
        {
            if (string.IsNullOrWhiteSpace(_path) || !File.Exists(_path))
                return false;

            using var reader = CsvDataReader.Create(_path, _options);
            name = name.Replace(" ", "_");
            return reader.GetRecords<CsvTag>().Any(t => t.Name == name);
        }
    }
}
