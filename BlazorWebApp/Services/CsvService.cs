using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using Sylvan.Data;
using Sylvan.Data.Csv;

namespace BlazorWebApp.Services
{
    public class CsvService
    {
        private readonly ManagerService _m;
        private readonly Schema _schema;
        private readonly CsvDataReaderOptions _options;
        private readonly string _path;

        public CsvService(ManagerService m)
        {
            _m  = m;
            _schema = Schema.Parse("Name,Color,Uses,Aliases");
            _options = new CsvDataReaderOptions() { Schema = new CsvSchema(_schema), HasHeaders = false };
            _path = Path.Join(_m.CmdFlags.BaseDir, @"extensions\a1111-sd-webui-tagcomplete\tags\danbooru.csv");
        }

        public IEnumerable<CsvTag> SearchTags(string searchText)
        {
            using var reader = CsvDataReader.Create(_path, _options);
            searchText = searchText.Replace(" ", "_");
            var tags = reader.GetRecords<CsvTag>().Where(t => t.Name.Contains(searchText, StringComparison.InvariantCultureIgnoreCase) || t.Aliases.Contains(searchText, StringComparison.InvariantCultureIgnoreCase)).OrderByDescending(t => t.Uses).ToArray();
            List<CsvTag> result = new();
            foreach (var tag in tags)
            {
                result.Add(Parser.ParseCsvTag(tag));
            }
            return result;
        }

        public CsvTag? GetTag(string name)
        {
            using var reader = CsvDataReader.Create(_path, _options);
            name = name.Replace(" ", "_");
            return reader.GetRecords<CsvTag>().FirstOrDefault(t => t.Name.Equals(name));
        }

        public bool CheckTagExists(string name)
        {
            using var reader = CsvDataReader.Create(_path, _options);
            name = name.Replace(" ", "_");
            return reader.GetRecords<CsvTag>().Any(t => t.Name == name);
        }

    }
}
