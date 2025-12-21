using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Models.Fragments
{
    /// <summary>
    /// A generic fragment that stores arbitrary key-value pairs.
    /// Used as a fallback for fragment types that don't have a dedicated strongly-typed class.
    /// </summary>
    public class GenericFragment : FragmentBase
    {
        private string _fragmentFile = "generic.sbn";

        /// <summary>
        /// The fragment template file path. Can be set dynamically for generic fragments.
        /// </summary>
        [JsonPropertyName("fragment_file")]
        public string FragmentFilePath
        {
            get => _fragmentFile;
            set => _fragmentFile = value;
        }

        /// <inheritdoc />
        [JsonIgnore]
        public override string FragmentFile => _fragmentFile;

        /// <summary>
        /// Storage for dynamic property values.
        /// </summary>
        [JsonPropertyName("values")]
        public Dictionary<string, object?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public GenericFragment() { }

        public GenericFragment(string id)
        {
            Id = id;
        }

        public GenericFragment(string id, string fragmentFile)
        {
            Id = id;
            _fragmentFile = fragmentFile;
        }

        /// <summary>
        /// Sets the fragment file for this generic fragment.
        /// </summary>
        public void SetFragmentFile(string fragmentFile)
        {
            _fragmentFile = fragmentFile;
        }

        public override Dictionary<string, object?> ToDictionary()
        {
            var result = base.ToDictionary();
            
            // Add all dynamic values
            foreach (var kvp in Values)
            {
                result[kvp.Key] = kvp.Value;
            }

            return result;
        }

        public override bool SetPropertyFromDictionary(string key, object? value)
        {
            // First try to set base properties
            if (base.SetPropertyFromDictionary(key, value))
                return true;

            // Otherwise store in dynamic values
            Values[key] = value;
            return true;
        }

        public override T? GetValue<T>(string key) where T : default
        {
            // First check dynamic values
            if (Values.TryGetValue(key, out var value) && value != null)
            {
                if (value is T typedValue)
                    return typedValue;

                // Handle JsonElement from deserialization
                if (value is JsonElement jsonElement)
                {
                    return ConvertJsonElement<T>(jsonElement);
                }

                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default;
                }
            }

            // Fall back to base implementation
            return base.GetValue<T>(key);
        }

        public override T GetValueOrDefault<T>(string key, T defaultValue)
        {
            var value = GetValue<T>(key);
            return value ?? defaultValue;
        }

        public override void SetValue<T>(string key, T value)
        {
            Values[key] = value;
        }

        public override bool HasValue(string key)
        {
            return Values.ContainsKey(key) || base.HasValue(key);
        }

        public override FragmentBase Clone(string newId)
        {
            var clone = new GenericFragment(newId, _fragmentFile)
            {
                IsActive = IsActive,
                Order = Order
            };

            foreach (var kvp in Values)
            {
                clone.Values[kvp.Key] = kvp.Value;
            }

            return clone;
        }

        private static T? ConvertJsonElement<T>(JsonElement element)
        {
            var targetType = typeof(T);

            try
            {
                return element.ValueKind switch
                {
                    JsonValueKind.String when targetType == typeof(string) => (T)(object)element.GetString()!,
                    JsonValueKind.Number when targetType == typeof(int) => (T)(object)element.GetInt32(),
                    JsonValueKind.Number when targetType == typeof(long) => (T)(object)element.GetInt64(),
                    JsonValueKind.Number when targetType == typeof(float) => (T)(object)element.GetSingle(),
                    JsonValueKind.Number when targetType == typeof(double) => (T)(object)element.GetDouble(),
                    JsonValueKind.Number when targetType == typeof(decimal) => (T)(object)element.GetDecimal(),
                    JsonValueKind.True or JsonValueKind.False when targetType == typeof(bool) => (T)(object)element.GetBoolean(),
                    _ => JsonSerializer.Deserialize<T>(element.GetRawText())
                };
            }
            catch
            {
                return default;
            }
        }
    }
}
