using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    /// <summary>
    /// Holds parameter values for a single fragment instance.
    /// Provides type-safe access to dictionary values.
    /// </summary>
    public class FragmentParameters
    {
        /// <summary>
        /// The fragment identifier this instance uses (matches FragmentMetadata.Id).
        /// </summary>
        public string FragmentFile { get; set; } = string.Empty;

        /// <summary>
        /// Whether this fragment is active. Inactive fragments are skipped during workflow composition.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Order for UI display (lower values appear first).
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Parameter values. Keys match fragment parameter names.
        /// Values can be primitives, strings, or complex objects.
        /// </summary>
        public Dictionary<string, object?> Values { get; set; } = new();

        /// <summary>
        /// Pre-resolved options for parameters with dynamic sources.
        /// Keys are parameter names, values are lists of available options.
        /// Populated during workflow initialization, not serialized to database.
        /// UI components should read from this for synchronous option access.
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, List<string>> ResolvedOptions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets a typed value from the parameters dictionary.
        /// Returns default(T) if the key doesn't exist or conversion fails.
        /// Handles JsonElement values that result from JSON deserialization.
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
                var converted = ConvertJsonElement<T>(jsonElement);
                if (converted != null)
                {
                    // Optionally materialize the value to avoid repeated conversions
                    // Values[key] = converted; // Uncomment if performance is a concern
                    return converted;
                }
                return default;
            }

            // Try conversion for numeric types and other compatible types
            try
            {
                var targetType = typeof(T);
                var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                
                // Handle numeric conversions more robustly
                if (IsNumericType(underlyingType) && IsNumericType(value.GetType()))
                {
                    return (T)Convert.ChangeType(value, underlyingType);
                }
                
                // Handle string to numeric conversions
                if (IsNumericType(underlyingType) && value is string stringValue)
                {
                    return ParseStringToNumeric<T>(stringValue, underlyingType);
                }
                
                // Handle string to bool
                if (underlyingType == typeof(bool) && value is string boolString)
                {
                    if (bool.TryParse(boolString, out var boolResult))
                        return (T)(object)boolResult;
                }
                
                return (T)Convert.ChangeType(value, targetType);
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// Parses a string value to a numeric type.
        /// </summary>
        private static T? ParseStringToNumeric<T>(string value, Type underlyingType)
        {
            try
            {
                if (underlyingType == typeof(int) && int.TryParse(value, out var intVal))
                    return (T)(object)intVal;
                if (underlyingType == typeof(long) && long.TryParse(value, out var longVal))
                    return (T)(object)longVal;
                if (underlyingType == typeof(float) && float.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var floatVal))
                    return (T)(object)floatVal;
                if (underlyingType == typeof(double) && double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var doubleVal))
                    return (T)(object)doubleVal;
                if (underlyingType == typeof(decimal) && decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var decimalVal))
                    return (T)(object)decimalVal;
            }
            catch { }
            return default;
        }

        /// <summary>
        /// Gets a value with a fallback if not found or null.
        /// For value types, also returns default if the key doesn't exist.
        /// </summary>
        public T GetValueOrDefault<T>(string key, T defaultValue)
        {
            if (!Values.TryGetValue(key, out var value) || value == null)
                return defaultValue;

            // Direct type match
            if (value is T typedValue)
                return typedValue;

            // Handle JsonElement (from deserialization)
            if (value is JsonElement jsonElement)
            {
                var converted = ConvertJsonElement<T>(jsonElement);
                if (converted != null)
                    return converted;
                return defaultValue;
            }

            // Try conversion for numeric types
            try
            {
                var targetType = typeof(T);
                var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                
                // Handle numeric conversions more robustly
                if (IsNumericType(underlyingType) && IsNumericType(value.GetType()))
                {
                    return (T)Convert.ChangeType(value, underlyingType);
                }
                
                return (T)Convert.ChangeType(value, targetType);
            }
            catch
            {
                return defaultValue;
            }
        }
        
        /// <summary>
        /// Checks if a type is a numeric type.
        /// </summary>
        private static bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(float) ||
                   type == typeof(double) || type == typeof(decimal) || type == typeof(short) ||
                   type == typeof(byte) || type == typeof(uint) || type == typeof(ulong) ||
                   type == typeof(ushort) || type == typeof(sbyte);
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
        /// Note: ResolvedOptions is not cloned as it's transient runtime data.
        /// </summary>
        public FragmentParameters Clone()
        {
            return new FragmentParameters
            {
                FragmentFile = FragmentFile,
                IsActive = IsActive,
                Order = Order,
                Values = CloneValues(Values)
                // ResolvedOptions intentionally not cloned - transient data
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

                // Handle null
                if (element.ValueKind == JsonValueKind.Null)
                    return default;

                // Handle common type conversions based on target type and JsonElement kind
                return element.ValueKind switch
                {
                    // String conversions
                    JsonValueKind.String when underlyingType == typeof(string) 
                        => (T?)(object?)element.GetString(),
                    
                    // String to numeric (JSON might have quoted numbers)
                    JsonValueKind.String when IsNumericType(underlyingType)
                        => ParseStringToNumeric<T>(element.GetString() ?? "", underlyingType),
                    
                    // String to bool
                    JsonValueKind.String when underlyingType == typeof(bool)
                        => bool.TryParse(element.GetString(), out var b) ? (T?)(object?)b : default,
                    
                    // Numeric to int (handles both int and long sources)
                    JsonValueKind.Number when underlyingType == typeof(int) 
                        => element.TryGetInt32(out var i32) ? (T?)(object?)i32 : (T?)(object?)(int)element.GetDouble(),
                    
                    // Numeric to long
                    JsonValueKind.Number when underlyingType == typeof(long) 
                        => element.TryGetInt64(out var i64) ? (T?)(object?)i64 : (T?)(object?)(long)element.GetDouble(),
                    
                    // Numeric to float
                    JsonValueKind.Number when underlyingType == typeof(float) 
                        => (T?)(object?)element.GetSingle(),
                    
                    // Numeric to double
                    JsonValueKind.Number when underlyingType == typeof(double) 
                        => (T?)(object?)element.GetDouble(),
                    
                    // Numeric to decimal
                    JsonValueKind.Number when underlyingType == typeof(decimal) 
                        => (T?)(object?)element.GetDecimal(),
                    
                    // Numeric to string (for cases where we want string representation)
                    JsonValueKind.Number when underlyingType == typeof(string)
                        => (T?)(object?)element.GetRawText(),
                    
                    // Boolean
                    JsonValueKind.True or JsonValueKind.False when underlyingType == typeof(bool) 
                        => (T?)(object?)element.GetBoolean(),
                    
                    // Boolean to string
                    JsonValueKind.True or JsonValueKind.False when underlyingType == typeof(string)
                        => (T?)(object?)(element.GetBoolean() ? "true" : "false"),
                    
                    // Complex objects - use full deserialization
                    JsonValueKind.Object or JsonValueKind.Array 
                        => JsonSerializer.Deserialize<T>(element.GetRawText()),
                    
                    // Fallback
                    _ => JsonSerializer.Deserialize<T>(element.GetRawText())
                };
            }
            catch
            {
                return default;
            }
        }

        #endregion

        #region Convenience Methods

        /// <summary>
        /// Gets an integer value with a default fallback.
        /// </summary>
        public int GetInt(string key, int defaultValue = 0) => GetValueOrDefault(key, defaultValue);

        /// <summary>
        /// Gets a long value with a default fallback.
        /// </summary>
        public long GetLong(string key, long defaultValue = 0) => GetValueOrDefault(key, defaultValue);

        /// <summary>
        /// Gets a float value with a default fallback.
        /// </summary>
        public float GetFloat(string key, float defaultValue = 0f) => GetValueOrDefault(key, defaultValue);

        /// <summary>
        /// Gets a double value with a default fallback.
        /// </summary>
        public double GetDouble(string key, double defaultValue = 0d) => GetValueOrDefault(key, defaultValue);

        /// <summary>
        /// Gets a boolean value with a default fallback.
        /// </summary>
        public bool GetBool(string key, bool defaultValue = false) => GetValueOrDefault(key, defaultValue);

        /// <summary>
        /// Gets a string value with a default fallback.
        /// </summary>
        public string GetString(string key, string defaultValue = "") => GetValueOrDefault(key, defaultValue) ?? defaultValue;

        /// <summary>
        /// Gets a string value or null if not found.
        /// </summary>
        public string? GetString(string key) => GetValue<string>(key);

        /// <summary>
        /// Gets a string value, falling back to <paramref name="fallback"/> when the stored value
        /// is null, missing, empty, or whitespace only. Used for optional prompt-injection fields
        /// that should transparently reuse a default when the user leaves them blank.
        /// </summary>
        public string GetStringOrFallback(string key, string fallback)
        {
            var value = GetString(key, string.Empty);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        #endregion
    }
}
