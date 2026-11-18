using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Converters
{
    /// <summary>
    /// Handles backward compatibility when deserializing a property that was
    /// previously a single string (e.g. "All") but is now a List&lt;string&gt;.
    /// Reads both JSON strings and JSON arrays; always writes as a JSON array.
    /// </summary>
    public class StringOrListConverter : JsonConverter<List<string>>
    {
        public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (string.IsNullOrEmpty(value) || value == "All")
                    return new List<string>();
                return new List<string> { value };
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var list = new List<string>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                        break;
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        var item = reader.GetString();
                        if (!string.IsNullOrEmpty(item))
                            list.Add(item);
                    }
                }
                return list;
            }

            if (reader.TokenType == JsonTokenType.Null)
                return new List<string>();

            throw new JsonException($"Unexpected token type {reader.TokenType} for BaseModels");
        }

        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value ?? new List<string>(), options);
        }
    }
}
