using BlazorWebApp.Models.Fragments;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Registry that maps fragment template files to their strongly-typed C# classes.
    /// </summary>
    public interface IFragmentRegistry
    {
        /// <summary>
        /// Creates a new instance of the appropriate fragment type for the given file.
        /// </summary>
        /// <param name="fragmentFile">The fragment template file (e.g., "sampler.sbn")</param>
        /// <param name="id">The unique ID for this fragment instance</param>
        /// <returns>A new fragment instance</returns>
        FragmentBase CreateFragment(string fragmentFile, string id);

        /// <summary>
        /// Gets the C# type for a given fragment file.
        /// </summary>
        /// <param name="fragmentFile">The fragment template file</param>
        /// <returns>The fragment type, or null if not registered</returns>
        Type? GetFragmentType(string fragmentFile);

        /// <summary>
        /// Gets all registered fragment file patterns.
        /// </summary>
        IEnumerable<string> GetRegisteredFiles();

        /// <summary>
        /// Checks if a fragment file has a registered type.
        /// </summary>
        bool IsRegistered(string fragmentFile);
    }

    /// <summary>
    /// Default implementation of IFragmentRegistry.
    /// Registers all known fragment types at construction.
    /// </summary>
    public class FragmentRegistry : IFragmentRegistry
    {
        private readonly Dictionary<string, Type> _registry = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger<FragmentRegistry> _logger;

        public FragmentRegistry(ILogger<FragmentRegistry> logger)
        {
            _logger = logger;
            RegisterAllFragments();
        }

        private void RegisterAllFragments()
        {
            // Core fragments
            Register<SamplerFragment>("sampler.sbn");
            Register<SamplerFragment>("sampler-standard.sbn");
            Register<PromptsFragment>("prompts.sbn");
            Register<LatentFragment>("empty-latent.sbn");

            // Enhancement fragments
            Register<SeedVR2Fragment>("upscale-seedvr2.sbn");
            Register<UpscaleFragment>("upscale.sbn");
            Register<DetailerFragment>("detailer-core.sbn");
            Register<DetailerFragment>("detailer.sbn");
            Register<ConditioningVariationFragment>("conditioning-variation.sbn");
            Register<SeedVarianceEnhancerFragment>("seed-variance-enhancer.sbn");

            // Wan/Video fragments
            Register<WanSamplerFragment>("wan/sampler-wan.sbn");
            Register<WanLoadModelFragment>("wan/load-wan-model.sbn");
            Register<FrameInterpolationFragment>("wan/frame-interpolation.sbn");
            Register<WanPromptsFragment>("wan/prompts.sbn");

            _logger.LogDebug("FragmentRegistry initialized with {Count} fragment types", _registry.Count);
        }

        private void Register<T>(string fragmentFile) where T : FragmentBase, new()
        {
            _registry[fragmentFile] = typeof(T);
        }

        public FragmentBase CreateFragment(string fragmentFile, string id)
        {
            // Normalize the path (handle both forward and back slashes)
            var normalizedFile = fragmentFile.Replace('\\', '/');

            if (!_registry.TryGetValue(normalizedFile, out var type))
            {
                // Return a GenericFragment as fallback for unregistered fragment types
                _logger.LogDebug("No typed fragment registered for '{FragmentFile}', using GenericFragment", fragmentFile);
                var genericFragment = new GenericFragment(id, normalizedFile);
                return genericFragment;
            }

            var fragment = (FragmentBase)Activator.CreateInstance(type)!;
            fragment.Id = id;

            _logger.LogDebug("Created fragment {Type} with ID '{Id}' for file '{File}'",
                type.Name, id, fragmentFile);

            return fragment;
        }

        public Type? GetFragmentType(string fragmentFile)
        {
            var normalizedFile = fragmentFile.Replace('\\', '/');
            return _registry.GetValueOrDefault(normalizedFile);
        }

        public IEnumerable<string> GetRegisteredFiles() => _registry.Keys;

        public bool IsRegistered(string fragmentFile)
        {
            var normalizedFile = fragmentFile.Replace('\\', '/');
            return _registry.ContainsKey(normalizedFile);
        }
    }
}
