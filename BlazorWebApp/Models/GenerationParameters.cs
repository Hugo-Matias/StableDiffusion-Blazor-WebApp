namespace BlazorWebApp.Models
{
    /// <summary>
    /// Unified parameter storage for all generation workflows.
    /// Uses a flexible dictionary-based structure driven by C# workflow builders.
    /// </summary>
    public class GenerationParameters
    {
        /// <summary>
        /// All parameters organized by fragment instance ID.
        /// Keys match FragmentMetadata.Id in C# fragment classes.
        /// Example: "main_sampler", "refiner_sampler", "prompts", "upscale"
        /// </summary>
        public Dictionary<string, FragmentParameters> Fragments { get; set; } = new();

        /// <summary>
        /// Workflow assets (models, VAEs, CLIPs).
        /// Keys match WorkflowMetadata.Assets[].Parameter names.
        /// Managed by existing AssetResolverService.
        /// Example: { "Model": "flux1.safetensors", "Vae": "ae.safetensors" }
        /// </summary>
        public Dictionary<string, string> Assets { get; set; } = new();

        /// <summary>
        /// Input images/videos for the workflow.
        /// Keys match WorkflowMetadata.Sources[].Id.
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
        /// Parses enabled loras into prompt strings in the format: &lt;lora:name:weight&gt;
        /// </summary>
        /// <returns>Tuple of (positive prompt loras, negative prompt loras)</returns>
        internal static (string positive, string negative) ParseLorasToPromptStrings(List<Lora> loras)
        {
            string positive = string.Empty;
            string negative = string.Empty;
            
            foreach (var lora in loras.Where(l => l.IsEnabled))
            {
                var loraString = $" <lora:{lora.Name}:{lora.Strength:N2}>";
                if (lora.IsNegative)
                    negative += loraString;
                else
                    positive += loraString;
            }
            
            return (positive, negative);
        }
    }
}
