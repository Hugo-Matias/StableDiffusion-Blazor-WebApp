using System.Text.Json;

namespace BlazorWebApp.Models
{
    /// <summary>
    /// Holds parameter values for a single fragment instance.
    /// Provides type-safe access to dictionary values.
    /// </summary>
    public class FragmentParameters
    {
        /// <summary>
        /// The fragment file this instance uses (e.g., "sampler.sbn").
        /// </summary>
        public string FragmentFile { get; set; } = string.Empty;

        /// <summary>
        /// Whether this fragment is active. Inactive fragments are skipped during rendering.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Order in the pipeline (for chainable fragments).
        /// Lower values render first.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Parameter values. Keys match fragment parameter names.
        /// Values can be primitives, strings, or complex objects.
        /// </summary>
        public Dictionary<string, object?> Values { get; set; } = new();

        /// <summary>
        /// Gets a typed value from the parameters dictionary.
        /// Returns default(T) if the key doesn't exist or conversion fails.
        /// </summary>
        public T? GetValue<T>(string key)
        {
            if (!Values.TryGetValue(key, out var value) || value == null)
                return default;

            // Direct type match
            if (value is T typedValue)
                return typedValue;

            // Handle JsonElement (from deserialization)
            if (value is JsonElement jsonElement)
            {
                return ConvertJsonElement<T>(jsonElement);
            }

            // Try conversion
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// Gets a value with a fallback if not found or null.
        /// </summary>
        public T GetValueOrDefault<T>(string key, T defaultValue)
        {
            var value = GetValue<T>(key);
            return value ?? defaultValue;
        }

        /// <summary>
        /// Sets a value in the parameters dictionary.
        /// </summary>
        public void SetValue<T>(string key, T value)
        {
            Values[key] = value;
        }

        /// <summary>
        /// Checks if a parameter exists and is not null.
        /// </summary>
        public bool HasValue(string key)
        {
            return Values.TryGetValue(key, out var value) && value != null;
        }

        /// <summary>
        /// Removes a parameter from the dictionary.
        /// </summary>
        public bool RemoveValue(string key)
        {
            return Values.Remove(key);
        }

        /// <summary>
        /// Creates a deep copy of this fragment parameters instance.
        /// </summary>
        public FragmentParameters Clone()
        {
            return new FragmentParameters
            {
                FragmentFile = FragmentFile,
                IsActive = IsActive,
                Order = Order,
                Values = CloneValues(Values)
            };
        }

        /// <summary>
        /// Merges values from another FragmentParameters instance.
        /// Existing values are overwritten.
        /// </summary>
        public void MergeFrom(FragmentParameters other)
        {
            foreach (var kvp in other.Values)
            {
                Values[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Sets multiple values at once from a dictionary.
        /// </summary>
        public void SetValues(Dictionary<string, object?> values)
        {
            foreach (var kvp in values)
            {
                Values[kvp.Key] = kvp.Value;
            }
        }

        #region Private Helpers

        private static Dictionary<string, object?> CloneValues(Dictionary<string, object?> source)
        {
            var result = new Dictionary<string, object?>();
            foreach (var kvp in source)
            {
                // Deep clone for known complex types
                if (kvp.Value is Dictionary<string, object?> nestedDict)
                {
                    result[kvp.Key] = CloneValues(nestedDict);
                }
                else if (kvp.Value is List<object?> list)
                {
                    result[kvp.Key] = new List<object?>(list);
                }
                else
                {
                    // Primitives and strings are immutable, safe to copy reference
                    result[kvp.Key] = kvp.Value;
                }
            }
            return result;
        }

        private static T? ConvertJsonElement<T>(JsonElement element)
        {
            try
            {
                var targetType = typeof(T);
                var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

                return element.ValueKind switch
                {
                    JsonValueKind.String when underlyingType == typeof(string) 
                        => (T?)(object?)element.GetString(),
                    
                    JsonValueKind.Number when underlyingType == typeof(int) 
                        => (T?)(object?)element.GetInt32(),
                    
                    JsonValueKind.Number when underlyingType == typeof(long) 
                        => (T?)(object?)element.GetInt64(),
                    
                    JsonValueKind.Number when underlyingType == typeof(float) 
                        => (T?)(object?)element.GetSingle(),
                    
                    JsonValueKind.Number when underlyingType == typeof(double) 
                        => (T?)(object?)element.GetDouble(),
                    
                    JsonValueKind.Number when underlyingType == typeof(decimal) 
                        => (T?)(object?)element.GetDecimal(),
                    
                    JsonValueKind.True or JsonValueKind.False when underlyingType == typeof(bool) 
                        => (T?)(object?)element.GetBoolean(),
                    
                    _ => JsonSerializer.Deserialize<T>(element.GetRawText())
                };
            }
            catch
            {
                return default;
            }
        }

        #endregion
    }
}
