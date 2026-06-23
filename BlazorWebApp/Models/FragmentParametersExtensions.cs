using System.Globalization;

namespace BlazorWebApp.Models;

/// <summary>
/// Extension methods for type-safe access to FragmentParameters.Values dictionary.
/// Provides compile-time type safety while maintaining dictionary flexibility.
/// </summary>
public static class FragmentParametersExtensions
{
    /// <summary>
    /// Gets a string value from the fragment parameters.
    /// </summary>
    /// <param name="fragment">The fragment parameters.</param>
    /// <param name="key">The parameter key.</param>
    /// <param name="defaultValue">Default value if key not found or conversion fails.</param>
    /// <returns>The string value or default.</returns>
    public static string GetString(this FragmentParameters? fragment, string key, string defaultValue = "")
    {
        if (fragment?.Values == null || !fragment.Values.TryGetValue(key, out var value))
            return defaultValue;

        return value?.ToString() ?? defaultValue;
    }

    /// <summary>
    /// Gets a string value from the fragment parameters, falling back to <paramref name="fallback"/>
    /// when the stored value is null, missing, empty, or whitespace only.
    /// Intended for optional prompt-injection fields (e.g. <c>detailer_prompt</c>) that should
    /// transparently reuse the main prompt when the user leaves them blank.
    /// </summary>
    public static string GetStringOrFallback(this FragmentParameters? fragment, string key, string fallback)
    {
        var value = fragment.GetString(key, string.Empty);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    /// <summary>
    /// Gets an integer value from the fragment parameters.
    /// Supports conversion from int, long, double, and string.
    /// </summary>
    /// <param name="fragment">The fragment parameters.</param>
    /// <param name="key">The parameter key.</param>
    /// <param name="defaultValue">Default value if key not found or conversion fails.</param>
    /// <returns>The integer value or default.</returns>
    public static int GetInt(this FragmentParameters? fragment, string key, int defaultValue = 0)
    {
        if (fragment?.Values == null || !fragment.Values.TryGetValue(key, out var value))
            return defaultValue;
        
        return value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            float f => (int)f,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) => i,
            _ => defaultValue
        };
    }

    /// <summary>
    /// Gets a double value from the fragment parameters.
    /// Supports conversion from double, float, int, long, and string.
    /// </summary>
    /// <param name="fragment">The fragment parameters.</param>
    /// <param name="key">The parameter key.</param>
    /// <param name="defaultValue">Default value if key not found or conversion fails.</param>
    /// <returns>The double value or default.</returns>
    public static double GetDouble(this FragmentParameters? fragment, string key, double defaultValue = 0.0)
    {
        if (fragment?.Values == null || !fragment.Values.TryGetValue(key, out var value))
            return defaultValue;
        
        return value switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            _ => defaultValue
        };
    }

    /// <summary>
    /// Gets a long value from the fragment parameters.
    /// Supports conversion from long, int, double, and string.
    /// </summary>
    /// <param name="fragment">The fragment parameters.</param>
    /// <param name="key">The parameter key.</param>
    /// <param name="defaultValue">Default value if key not found or conversion fails.</param>
    /// <returns>The long value or default.</returns>
    public static long GetLong(this FragmentParameters? fragment, string key, long defaultValue = 0L)
    {
        if (fragment?.Values == null || !fragment.Values.TryGetValue(key, out var value))
            return defaultValue;
        
        return value switch
        {
            long l => l,
            int i => i,
            double d => (long)d,
            float f => (long)f,
            string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) => l,
            _ => defaultValue
        };
    }

    /// <summary>
    /// Gets a boolean value from the fragment parameters.
    /// Supports conversion from bool, string ("true"/"false"), and int (0/non-zero).
    /// </summary>
    /// <param name="fragment">The fragment parameters.</param>
    /// <param name="key">The parameter key.</param>
    /// <param name="defaultValue">Default value if key not found or conversion fails.</param>
    /// <returns>The boolean value or default.</returns>
    public static bool GetBool(this FragmentParameters? fragment, string key, bool defaultValue = false)
    {
        if (fragment?.Values == null || !fragment.Values.TryGetValue(key, out var value))
            return defaultValue;
        
        return value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var b) => b,
            int i => i != 0,
            long l => l != 0,
            _ => defaultValue
        };
    }

    /// <summary>
    /// Gets a float value from the fragment parameters.
    /// Supports conversion from float, double, int, long, and string.
    /// </summary>
    /// <param name="fragment">The fragment parameters.</param>
    /// <param name="key">The parameter key.</param>
    /// <param name="defaultValue">Default value if key not found or conversion fails.</param>
    /// <returns>The float value or default.</returns>
    public static float GetFloat(this FragmentParameters? fragment, string key, float defaultValue = 0.0f)
    {
        if (fragment?.Values == null || !fragment.Values.TryGetValue(key, out var value))
            return defaultValue;
        
        return value switch
        {
            float f => f,
            double d => (float)d,
            int i => i,
            long l => l,
            string s when float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) => f,
            _ => defaultValue
        };
    }

    /// <summary>
    /// Sets a typed value in the fragment parameters.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="fragment">The fragment parameters.</param>
    /// <param name="key">The parameter key.</param>
    /// <param name="value">The value to set.</param>
    public static void SetValue<T>(this FragmentParameters fragment, string key, T value)
    {
        if (fragment?.Values == null)
            return;
        
        fragment.Values[key] = value!;
    }

    /// <summary>
    /// Checks if a parameter key exists in the fragment.
    /// </summary>
    public static bool HasValue(this FragmentParameters? fragment, string key)
    {
        return fragment?.Values?.ContainsKey(key) ?? false;
    }

    /// <summary>
    /// Gets a value of any type from the fragment parameters.
    /// Returns null if key not found.
    /// </summary>
    public static object? GetValue(this FragmentParameters? fragment, string key)
    {
        if (fragment?.Values == null || !fragment.Values.TryGetValue(key, out var value))
            return null;
        
        return value;
    }
}
