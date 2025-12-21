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
        /// 
        /// Values flow in pipeline order. Each fragment can:
        /// - Set new values that weren't set before
        /// - Override values from earlier fragments (later fragments win for non-null values)
        /// 
        /// LoRAs are automatically appended to the positive/negative prompts in the format:
        /// &lt;lora:name:weight&gt;
        /// 
        /// Scriban templates handle their own defaults via the ?? operator, so we don't need
        /// to protect certain keys here - just provide all available values.
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
            // Later fragments can override earlier ones (this is intentional for things like
            // detailer having its own sampler settings that differ from main sampler)
            foreach (var fragment in GetActiveFragments())
            {
                foreach (var kvp in fragment.Value.Values)
                {
                    // Only set if the value is meaningful (not null, not empty string for strings)
                    if (kvp.Value != null)
                    {
                        // For string values, skip if empty - let earlier fragments or template defaults apply
                        if (kvp.Value is string strVal && string.IsNullOrEmpty(strVal))
                            continue;
                            
                        result[kvp.Key] = kvp.Value;
                    }
                }

                // Expose the fragment itself for condition evaluation (e.g., upscale_seedvr2.IsActive)
                result[fragment.Key] = fragment.Value;
            }
            
            // Also add inactive fragments so template conditions can check them
            foreach (var fragment in Fragments.Where(f => !f.Value.IsActive))
            {
                if (!result.ContainsKey(fragment.Key))
                    result[fragment.Key] = fragment.Value;
            }

            // Add Loras as list (for templates that iterate over them)
            var enabledLoras = Loras.Where(l => l.IsEnabled).ToList();
            result["Loras"] = enabledLoras;

            // Parse loras into prompt strings and append to positive/negative prompts
            if (enabledLoras.Count > 0)
            {
                var (loraPositive, loraNegative) = ParseLorasToPromptStrings(enabledLoras);
                
                // Append lora strings to existing prompts
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
        /// <returns>Tuple of (positive prompt loras, negative prompt loras)</returns>
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
