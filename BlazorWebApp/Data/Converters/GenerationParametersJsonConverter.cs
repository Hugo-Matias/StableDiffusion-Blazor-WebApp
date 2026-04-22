using BlazorWebApp.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Converters
{
    /// <summary>
    /// Custom JSON converter for FragmentParameters that preserves value types.
    /// Handles the Dictionary&lt;string, object?&gt; Values property to prevent type loss on round-trip.
    /// </summary>
    public class FragmentParametersJsonConverter : JsonConverter<FragmentParameters>
    {
        public override FragmentParameters? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            var result = new FragmentParameters();
            
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    continue;

                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case nameof(FragmentParameters.FragmentFile):
                        result.FragmentFile = reader.GetString() ?? string.Empty;
                        break;
                    case nameof(FragmentParameters.IsActive):
                        result.IsActive = reader.GetBoolean();
                        break;
                    case nameof(FragmentParameters.Order):
                        result.Order = reader.GetInt32();
                        break;
                    case nameof(FragmentParameters.Values):
                        result.Values = ReadValuesDictionary(ref reader);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, FragmentParameters value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            
            writer.WriteString(nameof(FragmentParameters.FragmentFile), value.FragmentFile);
            writer.WriteBoolean(nameof(FragmentParameters.IsActive), value.IsActive);
            writer.WriteNumber(nameof(FragmentParameters.Order), value.Order);
            
            writer.WritePropertyName(nameof(FragmentParameters.Values));
            WriteValuesDictionary(writer, value.Values);
            
            writer.WriteEndObject();
        }

        /// <summary>
        /// Reads a dictionary and preserves value types instead of leaving them as JsonElement.
        /// </summary>
        private static Dictionary<string, object?> ReadValuesDictionary(ref Utf8JsonReader reader)
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            if (reader.TokenType != JsonTokenType.StartObject)
                return result;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    continue;

                var key = reader.GetString() ?? string.Empty;
                reader.Read();

                result[key] = ReadValue(ref reader);
            }

            return result;
        }

        /// <summary>
        /// Reads a single value, preserving its type.
        /// </summary>
        private static object? ReadValue(ref Utf8JsonReader reader)
        {
            return reader.TokenType switch
            {
                JsonTokenType.Null => null,
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => ReadNumber(ref reader),
                JsonTokenType.StartArray => ReadArray(ref reader),
                JsonTokenType.StartObject => ReadNestedObject(ref reader),
                _ => null
            };
        }

        /// <summary>
        /// Reads a number, preserving the most appropriate type.
        /// Prefers int > long > double to maintain precision.
        /// </summary>
        private static object ReadNumber(ref Utf8JsonReader reader)
        {
            // Try int first (most common for steps, seeds, etc.)
            if (reader.TryGetInt32(out var intValue))
                return intValue;

            // Try long for seeds and other large integers
            if (reader.TryGetInt64(out var longValue))
                return longValue;

            // Fall back to double for floating-point values
            return reader.GetDouble();
        }

        /// <summary>
        /// Reads an array, preserving element types.
        /// </summary>
        private static List<object?> ReadArray(ref Utf8JsonReader reader)
        {
            var result = new List<object?>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                result.Add(ReadValue(ref reader));
            }

            return result;
        }

        /// <summary>
        /// Reads a nested object as a dictionary.
        /// </summary>
        private static Dictionary<string, object?> ReadNestedObject(ref Utf8JsonReader reader)
        {
            return ReadValuesDictionary(ref reader);
        }

        /// <summary>
        /// Writes a dictionary with proper type handling.
        /// </summary>
        private static void WriteValuesDictionary(Utf8JsonWriter writer, Dictionary<string, object?> values)
        {
            writer.WriteStartObject();

            foreach (var kvp in values)
            {
                writer.WritePropertyName(kvp.Key);
                WriteValue(writer, kvp.Value);
            }

            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes a single value with proper type handling.
        /// </summary>
        private static void WriteValue(Utf8JsonWriter writer, object? value)
        {
            switch (value)
            {
                case null:
                    writer.WriteNullValue();
                    break;
                case bool b:
                    writer.WriteBooleanValue(b);
                    break;
                case string s:
                    writer.WriteStringValue(s);
                    break;
                case int i:
                    writer.WriteNumberValue(i);
                    break;
                case long l:
                    writer.WriteNumberValue(l);
                    break;
                case float f:
                    writer.WriteNumberValue(f);
                    break;
                case double d:
                    writer.WriteNumberValue(d);
                    break;
                case decimal dec:
                    writer.WriteNumberValue(dec);
                    break;
                case JsonElement je:
                    je.WriteTo(writer);
                    break;
                case Dictionary<string, object?> dict:
                    WriteValuesDictionary(writer, dict);
                    break;
                case List<object?> list:
                    WriteArray(writer, list);
                    break;
                case IEnumerable<object?> enumerable:
                    WriteArray(writer, enumerable.ToList());
                    break;
                default:
                    // Fallback to JSON serialization for complex types
                    JsonSerializer.Serialize(writer, value);
                    break;
            }
        }

        /// <summary>
        /// Writes an array with proper type handling.
        /// </summary>
        private static void WriteArray(Utf8JsonWriter writer, List<object?> values)
        {
            writer.WriteStartArray();
            foreach (var value in values)
            {
                WriteValue(writer, value);
            }
            writer.WriteEndArray();
        }
    }

    /// <summary>
    /// Custom JSON converter for GenerationParameters that uses FragmentParametersJsonConverter
    /// for the Fragments dictionary.
    /// </summary>
    public class GenerationParametersJsonConverter : JsonConverter<GenerationParameters>
    {
        private static readonly FragmentParametersJsonConverter _fragmentConverter = new();

        public override GenerationParameters? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            var result = new GenerationParameters();
            
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    continue;

                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case nameof(GenerationParameters.WorkflowId):
                        if (reader.TokenType == JsonTokenType.String && Guid.TryParse(reader.GetString(), out var guid))
                            result.WorkflowId = guid;
                        else if (reader.TokenType == JsonTokenType.Null)
                            result.WorkflowId = null;
                        break;

                    case nameof(GenerationParameters.Fragments):
                        result.Fragments = ReadFragmentsDictionary(ref reader);
                        break;

                    case nameof(GenerationParameters.Assets):
                        result.Assets = ReadStringDictionary(ref reader);
                        break;

                    case nameof(GenerationParameters.Sources):
                        result.Sources = ReadSourcesDictionary(ref reader, options);
                        break;

                    case nameof(GenerationParameters.Loras):
                        result.Loras = JsonSerializer.Deserialize<List<Lora>>(ref reader, options) ?? new List<Lora>();
                        break;

                    case nameof(GenerationParameters.DetailerLoras):
                        // Legacy pass-0-only shape: stored as a flat List<Lora>. Hydrate into pass 0 of the dictionary.
                        result.DetailerLorasByPass[0] = JsonSerializer.Deserialize<List<Lora>>(ref reader, options) ?? new List<Lora>();
                        break;

                    case nameof(GenerationParameters.DetailerLorasByPass):
                        var byPass = JsonSerializer.Deserialize<Dictionary<string, List<Lora>>>(ref reader, options);
                        if (byPass != null)
                        {
                            result.DetailerLorasByPass = byPass
                                .Where(kvp => int.TryParse(kvp.Key, out _))
                                .ToDictionary(
                                    kvp => int.Parse(kvp.Key),
                                    kvp => kvp.Value ?? new List<Lora>());
                        }
                        break;

                    default:
                        reader.Skip();
                        break;
                }
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, GenerationParameters value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            // WorkflowId
            if (value.WorkflowId.HasValue)
            {
                writer.WriteString(nameof(GenerationParameters.WorkflowId), value.WorkflowId.Value.ToString());
            }
            else
            {
                writer.WriteNull(nameof(GenerationParameters.WorkflowId));
            }

            // Fragments
            writer.WritePropertyName(nameof(GenerationParameters.Fragments));
            WriteFragmentsDictionary(writer, value.Fragments, options);

            // Assets
            writer.WritePropertyName(nameof(GenerationParameters.Assets));
            WriteStringDictionary(writer, value.Assets);

            // Sources
            writer.WritePropertyName(nameof(GenerationParameters.Sources));
            WriteSourcesDictionary(writer, value.Sources, options);

            // Loras
            writer.WritePropertyName(nameof(GenerationParameters.Loras));
            JsonSerializer.Serialize(writer, value.Loras, options);

            // DetailerLorasByPass - authoritative multi-pass store
            writer.WritePropertyName(nameof(GenerationParameters.DetailerLorasByPass));
            JsonSerializer.Serialize(
                writer,
                value.DetailerLorasByPass.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value),
                options);

            writer.WriteEndObject();
        }

        private static Dictionary<string, FragmentParameters> ReadFragmentsDictionary(ref Utf8JsonReader reader)
        {
            var result = new Dictionary<string, FragmentParameters>(StringComparer.OrdinalIgnoreCase);

            if (reader.TokenType != JsonTokenType.StartObject)
                return result;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    continue;

                var key = reader.GetString() ?? string.Empty;
                reader.Read();

                var fragment = _fragmentConverter.Read(ref reader, typeof(FragmentParameters), new JsonSerializerOptions());
                if (fragment != null)
                    result[key] = fragment;
            }

            return result;
        }

        private static void WriteFragmentsDictionary(Utf8JsonWriter writer, Dictionary<string, FragmentParameters> fragments, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var kvp in fragments)
            {
                writer.WritePropertyName(kvp.Key);
                _fragmentConverter.Write(writer, kvp.Value, options);
            }
            writer.WriteEndObject();
        }

        private static Dictionary<string, string> ReadStringDictionary(ref Utf8JsonReader reader)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (reader.TokenType != JsonTokenType.StartObject)
                return result;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    continue;

                var key = reader.GetString() ?? string.Empty;
                reader.Read();
                var value = reader.GetString() ?? string.Empty;
                result[key] = value;
            }

            return result;
        }

        private static void WriteStringDictionary(Utf8JsonWriter writer, Dictionary<string, string> dict)
        {
            writer.WriteStartObject();
            foreach (var kvp in dict)
            {
                writer.WriteString(kvp.Key, kvp.Value);
            }
            writer.WriteEndObject();
        }

        private static Dictionary<string, SourceAsset> ReadSourcesDictionary(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            var result = new Dictionary<string, SourceAsset>(StringComparer.OrdinalIgnoreCase);

            if (reader.TokenType != JsonTokenType.StartObject)
                return result;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    continue;

                var key = reader.GetString() ?? string.Empty;
                reader.Read();

                var source = JsonSerializer.Deserialize<SourceAsset>(ref reader, options);
                if (source != null)
                    result[key] = source;
            }

            return result;
        }

        private static void WriteSourcesDictionary(Utf8JsonWriter writer, Dictionary<string, SourceAsset> sources, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var kvp in sources)
            {
                writer.WritePropertyName(kvp.Key);
                JsonSerializer.Serialize(writer, kvp.Value, options);
            }
            writer.WriteEndObject();
        }
    }
}
