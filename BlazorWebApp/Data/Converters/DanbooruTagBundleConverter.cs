using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace BlazorWebApp.Data.Converters
{
    /// <summary>
    /// EF Core ValueConverter that serializes <see cref="Entities.DanbooruTagBundle"/> to a JSON string
    /// and back. Registered on <c>AppDbContext.OnModelCreating</c> for the <c>TagsBundle</c> column.
    /// </summary>
    public class DanbooruTagBundleConverter : ValueConverter<Entities.DanbooruTagBundle, string>
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public DanbooruTagBundleConverter()
            : base(
                v => JsonSerializer.Serialize(v, _options),
                v => JsonSerializer.Deserialize<Entities.DanbooruTagBundle>(v, _options) ?? new Entities.DanbooruTagBundle())
        {
        }
    }
}
