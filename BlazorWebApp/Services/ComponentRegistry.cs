using BlazorWebApp.Components.Shared.Generation;
using BlazorWebApp.Components.Shared.Generation.Fragments;
using Microsoft.AspNetCore.Components;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Registry for mapping fragment component names to Blazor component types.
    /// Components are registered at startup and resolved at runtime when rendering fragments.
    /// </summary>
    public class ComponentRegistry : IComponentRegistry
    {
        private readonly Dictionary<string, Type> _components = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger<ComponentRegistry> _logger;

        public ComponentRegistry(ILogger<ComponentRegistry> logger)
        {
            _logger = logger;
            RegisterDefaultComponents();
        }

        /// <summary>
        /// Registers the default set of form components for common fragments.
        /// </summary>
        private void RegisterDefaultComponents()
        {
            // Fragment form components (in Fragments/ folder)
            Register("ConditioningVariationForm", typeof(ConditioningVariationForm));
            Register("SeedVarianceEnhancerForm", typeof(SeedVarianceEnhancerForm));
            Register("SamplerForm", typeof(SamplerForm));
            Register("LatentForm", typeof(LatentForm));
            Register("UpscaleForm", typeof(UpscaleForm));
            Register("SeedVR2Form", typeof(SeedVR2Form));
            Register("PromptsForm", typeof(PromptsForm));

            _logger.LogDebug("ComponentRegistry initialized with {Count} default components", _components.Count);
        }

        /// <inheritdoc />
        public Type? GetComponent(string componentName)
        {
            if (string.IsNullOrEmpty(componentName))
                return null;

            if (_components.TryGetValue(componentName, out var componentType))
            {
                return componentType;
            }

            _logger.LogWarning("Component '{ComponentName}' not found in registry", componentName);
            return null;
        }

        /// <inheritdoc />
        public bool IsRegistered(string componentName)
        {
            return !string.IsNullOrEmpty(componentName) && _components.ContainsKey(componentName);
        }

        /// <inheritdoc />
        public IEnumerable<string> GetRegisteredComponents()
        {
            return _components.Keys;
        }

        /// <inheritdoc />
        public void Register(string componentName, Type componentType)
        {
            if (string.IsNullOrEmpty(componentName))
                throw new ArgumentException("Component name cannot be null or empty", nameof(componentName));

            if (componentType == null)
                throw new ArgumentNullException(nameof(componentType));

            if (!typeof(IComponent).IsAssignableFrom(componentType))
                throw new ArgumentException($"Type {componentType.Name} must implement IComponent", nameof(componentType));

            _components[componentName] = componentType;
            _logger.LogDebug("Registered component '{ComponentName}' -> {ComponentType}", componentName, componentType.FullName);
        }

        /// <inheritdoc />
        public void Register<TComponent>() where TComponent : IComponent
        {
            var componentType = typeof(TComponent);
            var componentName = componentType.Name;
            Register(componentName, componentType);
        }
    }
}
