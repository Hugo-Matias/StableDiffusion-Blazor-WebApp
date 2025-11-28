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
        private readonly CacheService _tagUsage;
        private readonly Schema _schema;
        private readonly CsvDataReaderOptions _options;
        private readonly string _path;

        public CsvService(ManagerService m, IConfiguration configuration, CacheService tagUsage)
        {
            _m = m;
            _configuration = configuration;
            _tagUsage = tagUsage;
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
        }

        public async Task<IEnumerable<Tag>> SearchTags(string searchText)
        {
            using var reader = CsvDataReader.Create(_path, _options);
            searchText = searchText.Replace(" ", "_");

            var tags = reader.GetRecords<CsvTag>()
                .Where(t => t.Name.Contains(searchText, StringComparison.InvariantCultureIgnoreCase) ||
                           (t.Aliases != null && t.Aliases.Contains(searchText, StringComparison.InvariantCultureIgnoreCase)))
                .OrderByDescending(t => t.Uses)
                .Select(dto => new Tag
                {
                    Name = dto.Name,
                    Color = dto.Color,
                    Uses = dto.Uses,
                    Aliases = dto.Aliases,
                    LocalUses = 0
                })
                .ToArray();

            List<Tag> result = new();
            foreach (var tag in tags)
            {
                var parsed = Parser.ParseCsvTag(tag);

                var localUsage = await _tagUsage.GetLocalTagUsageCount(tag.Name);
                parsed.LocalUses = localUsage;

                result.Add(parsed);
            }

            // Sort with priority:
            // 1. Tags with local usage first (ordered by LocalUses descending)
            // 2. Then tags without local usage (ordered by global Uses descending)
            return result
                .OrderByDescending(t => t.LocalUses > 0) // Tags with local usage first
                .ThenByDescending(t => t.LocalUses)       // Among local tags, most used first
                .ThenByDescending(t => t.Uses);           // Among non-local tags, most popular first
        }

        public Tag? GetTag(string name)
        {
            using var reader = CsvDataReader.Create(_path, _options);
            name = name.Replace(" ", "_");
            var dto = reader.GetRecords<CsvTag>().FirstOrDefault(t => t.Name.Equals(name));
            return dto == null ? null : new Tag
            {
                Name = dto.Name,
                Color = dto.Color,
                Uses = dto.Uses,
                Aliases = dto.Aliases,
                LocalUses = 0
            };
        }

        public bool CheckTagExists(string name)
        {
            using var reader = CsvDataReader.Create(_path, _options);
            name = name.Replace(" ", "_");
            return reader.GetRecords<Tag>().Any(t => t.Name == name);
        }
    }
}
