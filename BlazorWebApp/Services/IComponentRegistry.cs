namespace BlazorWebApp.Services
{
    /// <summary>
    /// Registry for mapping fragment component names to Blazor component types.
    /// </summary>
    public interface IComponentRegistry
    {
        /// <summary>
        /// Gets the component type for the given name.
        /// Returns null if no component is registered.
        /// </summary>
        Type? GetComponent(string componentName);

        /// <summary>
        /// Checks if a component is registered.
        /// </summary>
        bool IsRegistered(string componentName);

        /// <summary>
        /// Gets all registered component names.
        /// </summary>
        IEnumerable<string> GetRegisteredComponents();

        /// <summary>
        /// Registers a component type with a name.
        /// </summary>
        void Register(string componentName, Type componentType);

        /// <summary>
        /// Registers a component type using its class name.
        /// </summary>
        void Register<TComponent>() where TComponent : Microsoft.AspNetCore.Components.IComponent;
    }
}
