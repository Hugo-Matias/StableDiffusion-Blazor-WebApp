using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Converters
{
    public class CaseInsensitiveEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var enumString = reader.GetString();
            if (Enum.TryParse<T>(enumString, ignoreCase: true, out var value))
                return value;

            throw new JsonException($"Unable to convert \"{enumString}\" to enum {typeof(T)}.");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
