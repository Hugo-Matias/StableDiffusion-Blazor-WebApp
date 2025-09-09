using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos
{
    public class DanbooruPost
    {
        public int Id { get; set; }
        public int Score { get; set; }
        public string Rating { get; set; }
        [JsonPropertyName("image_width")]
        public int Width { get; set; }
        [JsonPropertyName("image_height")]
        public int Height { get; set; }
        [JsonPropertyName("file_ext")]
        public string Extension { get; set; }
        [JsonPropertyName("tag_string"), JsonConverter(typeof(DanbooruTagListConverter))]
        public List<string> Tags { get; set; }
        [JsonPropertyName("tag_string_general"), JsonConverter(typeof(DanbooruTagListConverter))]
        public List<string> TagsGeneral { get; set; }
        [JsonPropertyName("tag_string_artist"), JsonConverter(typeof(DanbooruTagListConverter))]
        public List<string> TagsArtist { get; set; }
        [JsonPropertyName("tag_string_copyright"), JsonConverter(typeof(DanbooruTagListConverter))]
        public List<string> TagsCopyright { get; set; }
        [JsonPropertyName("tag_string_character"), JsonConverter(typeof(DanbooruTagListConverter))]
        public List<string> TagsCharacter { get; set; }
        [JsonPropertyName("tag_string_meta"), JsonConverter(typeof(DanbooruTagListConverter))]
        public List<string> TagsMeta { get; set; }
        [JsonPropertyName("file_url")]
        public string Url { get; set; }
        [JsonPropertyName("large_file_url")]
        public string SampleUrl { get; set; }
        [JsonPropertyName("preview_file_url")]
        public string PreviewUrl { get; set; }
        public bool IsVideo { get; set; }
    }
    public class DanbooruTagListConverter : JsonConverter<List<string>>
    {
        public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string stringValue = reader.GetString();
            if (stringValue == null) return null;

            var tags = stringValue.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Replace("_", " ").Replace("(", "\\(").Replace(")", "\\)"))
                .ToList();
            return tags;
        }

        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
        {
            if (value != null)
            {
                string stringValue = string.Join(" ", value);
                writer.WriteStringValue(stringValue);
            }
            else writer.WriteNullValue();
        }
    }
}
