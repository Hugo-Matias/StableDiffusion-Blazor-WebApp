using BlazorWebApp.Attributes;
using BlazorWebApp.Components.Shared.Generation;
using BlazorWebApp.Components.Shared.Generation.Fragments;
using Microsoft.AspNetCore.Components;
using System.Reflection;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Registry for mapping fragment component names to Blazor component types.
    /// Components are registered at startup either manually or via [FragmentComponent] attribute.
    /// </summary>
    public class ComponentRegistry : IComponentRegistry
    {
        private readonly Dictionary<string, Type> _components = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger<ComponentRegistry> _logger;

        public ComponentRegistry(ILogger<ComponentRegistry> logger)
        {
            _logger = logger;
            DiscoverAndRegisterComponents();
        }

        /// <summary>
        /// Discovers and registers all components marked with [FragmentComponent] attribute.
        /// Falls back to manual registration for components without the attribute.
        /// </summary>
        private void DiscoverAndRegisterComponents()
        {
            var discoveredCount = 0;

            // Scan the current assembly for components with [FragmentComponent] attribute
            var assembly = Assembly.GetExecutingAssembly();
            var componentTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IComponent).IsAssignableFrom(t))
                .Where(t => t.GetCustomAttribute<FragmentComponentAttribute>() != null);

            foreach (var type in componentTypes)
            {
                var attr = type.GetCustomAttribute<FragmentComponentAttribute>()!;
                _components[attr.ComponentName] = type;
                discoveredCount++;
                _logger.LogTrace("Auto-discovered component '{ComponentName}' -> {ComponentType}", 
                    attr.ComponentName, type.Name);
            }

            // Log if no components were discovered (fall back to manual registration)
            if (discoveredCount == 0)
            {
                _logger.LogDebug("No [FragmentComponent] attributes found, using manual registration");
                RegisterManualComponents();
            }
            else
            {
                _logger.LogDebug("ComponentRegistry discovered {Count} components via [FragmentComponent] attribute", 
                    discoveredCount);
            }
        }

        /// <summary>
        /// Manual registration for components that don't use the attribute.
        /// This is the fallback if no attributed components are found.
        /// </summary>
        private void RegisterManualComponents()
        {
            // Fragment form components (in Fragments/ folder)
            Register("ConditioningVariationForm", typeof(ConditioningVariationForm));
            Register("SeedVarianceEnhancerForm", typeof(SeedVarianceEnhancerForm));
            Register("SamplerForm", typeof(SamplerForm));
            Register("LatentForm", typeof(LatentForm));
            Register("UpscaleForm", typeof(UpscaleForm));
            Register("SeedVR2Form", typeof(SeedVR2Form));
            Register("PromptsForm", typeof(PromptsForm));

            _logger.LogDebug("ComponentRegistry initialized with {Count} manually registered components", _components.Count);
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
