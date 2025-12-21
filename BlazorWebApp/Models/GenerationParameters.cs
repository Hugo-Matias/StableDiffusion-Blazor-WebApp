using BlazorWebApp.Models.Fragments;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    /// <summary>
    /// Unified parameter storage for all generation workflows.
    /// Uses strongly-typed fragment classes for compile-time safety.
    /// </summary>
    public class GenerationParameters
    {
        /// <summary>
        /// All fragments organized by instance ID.
        /// Keys match Pipeline[].id in the workflow template.
        /// Example: "main_sampler", "refiner_sampler", "prompts", "upscale"
        /// 
        /// Uses polymorphic JSON serialization for derived fragment types.
        /// </summary>
        public Dictionary<string, FragmentBase> Fragments { get; set; } = new();

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

        #region Typed Fragment Accessors

        /// <summary>
        /// Gets the prompts fragment if present.
        /// </summary>
        [JsonIgnore]
        public PromptsFragment? Prompts => GetFragment<PromptsFragment>("prompts");

        /// <summary>
        /// Gets the main sampler fragment if present.
        /// </summary>
        [JsonIgnore]
        public SamplerFragment? MainSampler => GetFragment<SamplerFragment>("main_sampler");

        /// <summary>
        /// Gets the latent fragment if present.
        /// </summary>
        [JsonIgnore]
        public LatentFragment? Latent => GetFragment<LatentFragment>("empty_latent");

        /// <summary>
        /// Gets the SeedVR2 fragment if present.
        /// </summary>
        [JsonIgnore]
        public SeedVR2Fragment? SeedVR2 => GetFragment<SeedVR2Fragment>("seed_vr2");

        /// <summary>
        /// Gets the upscale fragment if present.
        /// </summary>
        [JsonIgnore]
        public UpscaleFragment? Upscale => GetFragment<UpscaleFragment>("upscale");

        /// <summary>
        /// Gets the detailer fragment if present.
        /// </summary>
        [JsonIgnore]
        public DetailerFragment? Detailer => GetFragment<DetailerFragment>("detailer");

        /// <summary>
        /// Gets the conditioning variation fragment if present.
        /// </summary>
        [JsonIgnore]
        public ConditioningVariationFragment? ConditioningVariation => 
            GetFragment<ConditioningVariationFragment>("conditioning_variation");

        /// <summary>
        /// Gets the seed variance enhancer fragment if present.
        /// </summary>
        [JsonIgnore]
        public SeedVarianceEnhancerFragment? SeedVarianceEnhancer => 
            GetFragment<SeedVarianceEnhancerFragment>("seed_variance_enhancer");

        /// <summary>
        /// Gets the frame interpolation fragment if present.
        /// </summary>
        [JsonIgnore]
        public FrameInterpolationFragment? FrameInterpolation => 
            GetFragment<FrameInterpolationFragment>("frame_interpolation");

        #endregion

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
                    kvp => kvp.Value.Clone(kvp.Key)
                )
            };
        }

        /// <summary>
        /// Gets a typed fragment by ID, or null if not found or wrong type.
        /// </summary>
        public T? GetFragment<T>(string fragmentId) where T : FragmentBase
        {
            return Fragments.TryGetValue(fragmentId, out var fragment) ? fragment as T : null;
        }

        /// <summary>
        /// Gets a fragment by ID (untyped), or null if not found.
        /// </summary>
        public FragmentBase? GetFragment(string fragmentId)
        {
            return Fragments.GetValueOrDefault(fragmentId);
        }

        /// <summary>
        /// Gets or creates a typed fragment with the specified ID.
        /// If a fragment exists but is the wrong type, it will be replaced.
        /// </summary>
        public T GetOrCreateFragment<T>(string fragmentId) where T : FragmentBase, new()
        {
            if (Fragments.TryGetValue(fragmentId, out var existing) && existing is T typed)
                return typed;

            var fragment = new T
            {
                Id = fragmentId,
                IsActive = true,
                Order = Fragments.Count
            };
            Fragments[fragmentId] = fragment;
            return fragment;
        }

        /// <summary>
        /// Gets all active fragments ordered by their Order property.
        /// </summary>
        public IEnumerable<KeyValuePair<string, FragmentBase>> GetActiveFragments()
        {
            return Fragments
                .Where(f => f.Value.IsActive)
                .OrderBy(f => f.Value.Order);
        }

        /// <summary>
        /// Flattens all fragment values into a single dictionary for Scriban template rendering.
        /// 
        /// Values flow in pipeline order. Each fragment can:
        /// - Set new values that weren't set before
        /// - Override values from earlier fragments (later fragments win for non-null values)
        /// 
        /// LoRAs are automatically appended to the positive/negative prompts in the format:
        /// &lt;lora:name:weight&gt;
        /// 
        /// Scriban templates handle their own defaults via the ?? operator.
        /// </summary>
        public Dictionary<string, object?> FlattenForTemplateRendering()
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            // Add assets first - these use PascalCase as defined in workflow templates
            foreach (var asset in Assets)
            {
                result[asset.Key] = asset.Value;
            }

            // Add fragment values in pipeline order
            foreach (var fragment in GetActiveFragments())
            {
                // Use the fragment's ToDictionary method to get all properties
                foreach (var kvp in fragment.Value.ToDictionary())
                {
                    // Only set if the value is meaningful
                    if (kvp.Value != null)
                    {
                        // For string values, skip if empty
                        if (kvp.Value is string strVal && string.IsNullOrEmpty(strVal))
                            continue;

                        result[kvp.Key] = kvp.Value;
                    }
                }

                // Expose the fragment itself for condition evaluation (e.g., seed_vr2.IsActive)
                result[fragment.Key] = fragment.Value;
            }

            // Also add inactive fragments so template conditions can check them
            foreach (var fragment in Fragments.Where(f => !f.Value.IsActive))
            {
                if (!result.ContainsKey(fragment.Key))
                    result[fragment.Key] = fragment.Value;
            }

            // Add Loras as list
            var enabledLoras = Loras.Where(l => l.IsEnabled).ToList();
            result["Loras"] = enabledLoras;

            // Parse loras into prompt strings and append to positive/negative prompts
            if (enabledLoras.Count > 0)
            {
                var (loraPositive, loraNegative) = ParseLorasToPromptStrings(enabledLoras);

                if (!string.IsNullOrEmpty(loraPositive))
                {
                    var currentPositive = result.TryGetValue("positive", out var posVal) ? posVal?.ToString() ?? "" : "";
                    result["positive"] = string.IsNullOrEmpty(currentPositive)
                        ? loraPositive.Trim()
                        : currentPositive + loraPositive;
                }

                if (!string.IsNullOrEmpty(loraNegative))
                {
                    var currentNegative = result.TryGetValue("negative", out var negVal) ? negVal?.ToString() ?? "" : "";
                    result["negative"] = string.IsNullOrEmpty(currentNegative)
                        ? loraNegative.Trim()
                        : currentNegative + loraNegative;
                }
            }

            return result;
        }

        /// <summary>
        /// Parses enabled loras into prompt strings in the format: &lt;lora:name:weight&gt;
        /// </summary>
        private static (string positive, string negative) ParseLorasToPromptStrings(List<Lora> loras)
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
