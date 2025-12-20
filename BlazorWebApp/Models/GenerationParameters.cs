namespace BlazorWebApp.Models
{
    /// <summary>
    /// Unified parameter storage for all generation workflows.
    /// Uses a flexible dictionary-based structure driven by workflow templates.
    /// </summary>
    public class GenerationParameters
    {
        /// <summary>
        /// All parameters organized by fragment instance ID.
        /// Keys match Pipeline[].id in the workflow template.
        /// Example: "main_sampler", "refiner_sampler", "prompts", "upscale"
        /// </summary>
        public Dictionary<string, FragmentParameters> Fragments { get; set; } = new();

        /// <summary>
        /// Workflow assets (models, VAEs, CLIPs).
        /// Keys match workflow Asset.Parameter names.
        /// Managed by existing AssetResolverService.
        /// Example: { "Model": "flux1.safetensors", "Vae": "ae.safetensors" }
        /// </summary>
        public Dictionary<string, string> Assets { get; set; } = new();

        /// <summary>
        /// Input images/videos for the workflow.
        /// Keys match workflow Sources[].id.
        /// Example: { "source_image": SourceAsset, "reference_pose": SourceAsset }
        /// </summary>
        public Dictionary<string, SourceAsset> Sources { get; set; } = new();

        /// <summary>
        /// LoRAs active for this generation.
        /// </summary>
        public List<Lora> Loras { get; set; } = new();

        /// <summary>
        /// Current workflow reference.
        /// </summary>
        public Guid? WorkflowId { get; set; }

        /// <summary>
        /// Creates a deep copy of the generation parameters.
        /// </summary>
        public GenerationParameters Clone()
        {
            return new GenerationParameters
            {
                WorkflowId = WorkflowId,
                Assets = new Dictionary<string, string>(Assets),
                Sources = Sources.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Clone()
                ),
                Loras = Loras.Select(l => new Lora
                {
                    Name = l.Name,
                    Path = l.Path,
                    Strength = l.Strength,
                    IsEnabled = l.IsEnabled,
                    IsNegative = l.IsNegative
                }).ToList(),
                Fragments = Fragments.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Clone()
                )
            };
        }

        /// <summary>
        /// Gets a fragment by ID, or null if not found.
        /// </summary>
        public FragmentParameters? GetFragment(string fragmentId)
        {
            return Fragments.GetValueOrDefault(fragmentId);
        }

        /// <summary>
        /// Gets or creates a fragment with the specified ID.
        /// </summary>
        public FragmentParameters GetOrCreateFragment(string fragmentId, string fragmentFile = "")
        {
            if (!Fragments.TryGetValue(fragmentId, out var fragment))
            {
                fragment = new FragmentParameters
                {
                    FragmentFile = fragmentFile,
                    IsActive = true,
                    Order = Fragments.Count
                };
                Fragments[fragmentId] = fragment;
            }
            return fragment;
        }

        /// <summary>
        /// Gets all active fragments ordered by their Order property.
        /// </summary>
        public IEnumerable<KeyValuePair<string, FragmentParameters>> GetActiveFragments()
        {
            return Fragments
                .Where(f => f.Value.IsActive)
                .OrderBy(f => f.Value.Order);
        }

        /// <summary>
        /// Finds the first fragment containing a specific parameter key.
        /// </summary>
        public FragmentParameters? FindFragmentByParameter(string parameterKey)
        {
            foreach (var fragment in Fragments.Values)
            {
                if (fragment.Values.ContainsKey(parameterKey))
                    return fragment;
            }
            return null;
        }

        /// <summary>
        /// Flattens all fragment values into a single dictionary for Scriban template rendering.
        /// Later fragments override earlier ones if keys conflict.
        /// Fragment parameters use snake_case which matches template expectations.
        /// </summary>
        public Dictionary<string, object?> FlattenForTemplateRendering()
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            // Add assets - these use PascalCase as defined in workflow templates
            foreach (var asset in Assets)
            {
                result[asset.Key] = asset.Value;
            }

            // Add fragment values (ordered, so later fragments can override)
            foreach (var fragment in GetActiveFragments())
            {
                // Add all parameter values from the fragment
                foreach (var value in fragment.Value.Values)
                {
                    result[value.Key] = value.Value;
                }

                // Expose the fragment itself for condition evaluation (e.g., SeedVR2.IsActive)
                result[fragment.Key] = fragment.Value;
            }
            
            // Also add fragments that are NOT active so conditions can check them
            foreach (var fragment in Fragments.Where(f => !f.Value.IsActive))
            {
                if (!result.ContainsKey(fragment.Key))
                    result[fragment.Key] = fragment.Value;
            }

            // Add Loras
            result["Loras"] = Loras.Where(l => l.IsEnabled).ToList();

            return result;
        }
    }
}
