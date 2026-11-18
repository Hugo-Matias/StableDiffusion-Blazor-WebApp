using System.Reflection;

namespace BlazorWebApp.Extensions
{
    /// <summary>
    /// Marks a property as intentionally nullable and excludes it from normalization.
    /// Ex: Use for bool? tri-state scenarios filters where null has semantic meaning.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class PreserveNullAttribute : Attribute
    {
    }

    /// <summary>
    /// Automatically normalizes state objects by ensuring all nullable reference types and collections are initialized.
    /// Uses reflection to discover and initialize properties without manual tracking.
    /// </summary>
    public static class StateNormalizer
    {
        /// <summary>
        /// Normalizes an object by initializing all null properties with default values.
        /// Recursively processes nested objects up to a specified depth.
        /// </summary>
        /// <param name="obj">The object to normalize</param>
        /// <param name="maxDepth">Maximum recursion depth to prevent infinite loops (default: 3)</param>
        /// <param name="currentDepth">Current recursion depth (internal use)</param>
        public static void Normalize(object obj, int maxDepth = 3, int currentDepth = 0)
        {
            if (obj == null || currentDepth >= maxDepth)
                return;

            var type = obj.GetType();

            // Skip primitive types, strings, and value types
            if (type.IsPrimitive || type == typeof(string) || type.IsValueType)
                return;

            // Get all public instance properties that can be written to
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.CanRead);

            foreach (var property in properties)
            {
                try
                {
                    var value = property.GetValue(obj);
                    var propertyType = property.PropertyType;

                    // Handle null values
                    if (value == null)
                    {
                        // Skip normalization if property is marked with PreserveNullAttribute
                        if (property.GetCustomAttribute<PreserveNullAttribute>() != null)
                            continue;

                        var newValue = CreateDefaultValue(propertyType);
                        if (newValue != null)
                        {
                            property.SetValue(obj, newValue);
                            value = newValue;
                        }
                    }

                    // Recursively normalize complex objects
                    if (value != null && ShouldNormalizeRecursively(propertyType))
                    {
                        Normalize(value, maxDepth, currentDepth + 1);
                    }

                    // Normalize collection items
                    if (value != null && IsGenericList(propertyType))
                    {
                        NormalizeCollection(value, maxDepth, currentDepth);
                    }
                }
                catch
                {
                    // Skip properties that throw exceptions (e.g., computed properties)
                    continue;
                }
            }
        }

        /// <summary>
        /// Creates a default value for a given type.
        /// </summary>
        private static object CreateDefaultValue(Type type)
        {
            // Handle generic List<T>
            if (IsGenericList(type))
            {
                return Activator.CreateInstance(type);
            }

            // Handle nullable value types - initialize with underlying type's default value
            var underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != null)
            {
                // For nullable value types like bool?, int?, double?, etc.
                // Return the default value of the underlying type (false, 0, 0.0, etc.)
                return Activator.CreateInstance(underlyingType);
            }

            // Handle reference types with parameterless constructors
            if (type.IsClass && !type.IsAbstract && HasParameterlessConstructor(type))
            {
                return Activator.CreateInstance(type);
            }

            return null;
        }

        /// <summary>
        /// Determines if a type should be recursively normalized.
        /// </summary>
        private static bool ShouldNormalizeRecursively(Type type)
        {
            // Don't recurse into primitive types, strings, or value types
            if (type.IsPrimitive || type == typeof(string) || type.IsValueType)
                return false;

            // Don't recurse into collections (handled separately)
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
                return false;

            // Don't recurse into framework types that don't need normalization
            if (type.Namespace?.StartsWith("System") == true && !type.Namespace.StartsWith("System.Collections"))
                return false;

            // Don't recurse into MudBlazor or Microsoft types
            if (type.Namespace?.StartsWith("MudBlazor") == true ||
                type.Namespace?.StartsWith("Microsoft") == true)
                return false;

            return type.IsClass;
        }

        /// <summary>
        /// Normalizes items within a collection.
        /// </summary>
        private static void NormalizeCollection(object collection, int maxDepth, int currentDepth)
        {
            if (collection is not System.Collections.IEnumerable enumerable)
                return;

            foreach (var item in enumerable)
            {
                if (item != null && ShouldNormalizeRecursively(item.GetType()))
                {
                    Normalize(item, maxDepth, currentDepth + 1);
                }
            }
        }

        /// <summary>
        /// Checks if a type is a generic List.
        /// </summary>
        private static bool IsGenericList(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);
        }

        /// <summary>
        /// Checks if a type has a parameterless constructor.
        /// </summary>
        private static bool HasParameterlessConstructor(Type type)
        {
            return type.GetConstructor(Type.EmptyTypes) != null;
        }
    }
}