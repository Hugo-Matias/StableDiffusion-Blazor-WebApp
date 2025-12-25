using BlazorWebApp.Models;

namespace BlazorWebApp.Services.Templating.Pipeline;

/// <summary>
/// Registry of compute functions for resolving $compute:name markers in pipeline parameters.
/// 
/// To add a new compute function:
/// 1. Add an entry to the _computers dictionary
/// 2. Document the function in FLUID_CONVENTIONS.md
/// </summary>
public class ComputeRegistry
{
    private readonly ILogger<ComputeRegistry> _logger;

    private readonly Dictionary<string, Func<GenerationParameters, object?>> _computers;

    public ComputeRegistry(ILogger<ComputeRegistry> logger)
    {
        _logger = logger;

        _computers = new Dictionary<string, Func<GenerationParameters, object?>>(StringComparer.OrdinalIgnoreCase)
        {
            // WAN img2vid: half of total steps for high/low noise split
            ["half_steps"] = p => 
            {
                var steps = GetFragmentValue<int?>(p, FragmentKeys.Fragments.MainSampler, FragmentKeys.Params.Steps) 
                         ?? GetFragmentValue<int?>(p, "sampler_high", FragmentKeys.Params.Steps)
                         ?? 8;
                return (int)Math.Round(steps / 2.0);
            },

            // WAN img2vid: frame rate multiplied by interpolation factor
            ["interpolated_framerate"] = p =>
            {
                var baseRate = GetFragmentValue<int?>(p, FragmentKeys.Fragments.Video, FragmentKeys.Params.FrameRate) ?? 16;
                var multiplier = GetFragmentValue<int?>(p, FragmentKeys.Fragments.FrameInterpolation, "frame_multiplier") ?? 2;
                return baseRate * multiplier;
            },

            // WAN img2vid: output frame rate (interpolated or base)
            ["output_frame_rate"] = p =>
            {
                var isActive = p.Fragments.TryGetValue("frame_interpolation", out var fi) && fi.IsActive;
                var baseRate = GetFragmentValue<int?>(p, "video_save", "frame_rate") 
                            ?? GetFragmentValue<int?>(p, FragmentKeys.Fragments.Video, FragmentKeys.Params.FrameRate) 
                            ?? 16;
                if (isActive)
                {
                    var multiplier = GetFragmentValue<int?>(p, FragmentKeys.Fragments.FrameInterpolation, "frame_multiplier") ?? 2;
                    return baseRate * multiplier;
                }
                return baseRate;
            },

            // WAN img2vid: video save input name (depends on frame interpolation)
            ["video_save_input"] = p =>
            {
                var isActive = p.Fragments.TryGetValue("frame_interpolation", out var fi) && fi.IsActive;
                return isActive ? "frames_output" : "image_output";
            },

            // WAN img2vid: high model input name (depends on whether high loras exist)
            ["high_model_input"] = p =>
            {
                // Check if any LoRAs with HighPath exist
                var hasHighLora = p.Loras?.Any(l => !string.IsNullOrEmpty(l.HighPath)) ?? false;
                return hasHighLora ? "high_lora_model_output" : "high_model_output";
            },

            // WAN img2vid: low model input name (depends on whether low loras exist)
            ["low_model_input"] = p =>
            {
                // Check if any LoRAs with LowPath exist
                var hasLowLora = p.Loras?.Any(l => !string.IsNullOrEmpty(l.LowPath)) ?? false;
                return hasLowLora ? "low_lora_model_output" : "low_model_output";
            },

            // Double the steps value
            ["double_steps"] = p => 
            {
                var steps = GetFragmentValue<int?>(p, FragmentKeys.Fragments.MainSampler, FragmentKeys.Params.Steps) ?? 20;
                return steps * 2;
            },

            // Calculate upscale dimensions based on scale factor
            ["upscale_width"] = p => 
            {
                var width = GetFragmentValue<int?>(p, FragmentKeys.Fragments.Latent, FragmentKeys.Params.Width) ?? 512;
                var scale = GetFragmentValue<double?>(p, FragmentKeys.Fragments.Upscale, "scale_factor") ?? 2.0;
                return (int)(width * scale);
            },
            
            ["upscale_height"] = p => 
            {
                var height = GetFragmentValue<int?>(p, FragmentKeys.Fragments.Latent, FragmentKeys.Params.Height) ?? 512;
                var scale = GetFragmentValue<double?>(p, FragmentKeys.Fragments.Upscale, "scale_factor") ?? 2.0;
                return (int)(height * scale);
            },
        };
    }

    /// <summary>
    /// Helper method to get a value from a specific fragment.
    /// </summary>
    private static T? GetFragmentValue<T>(GenerationParameters parameters, string fragmentId, string paramKey)
    {
        var fragment = parameters.GetFragment(fragmentId);
        if (fragment == null) return default;
        
        return fragment.GetValueOrDefault<T>(paramKey, default!);
    }

    /// <summary>
    /// Checks if a value is a compute marker (starts with "$compute:").
    /// </summary>
    public static bool IsComputeMarker(string? value)
    {
        return value != null && value.StartsWith("$compute:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the function name from a compute marker.
    /// </summary>
    public static string? GetComputeFunctionName(string marker)
    {
        if (!IsComputeMarker(marker))
            return null;

        return marker.Substring("$compute:".Length);
    }

    /// <summary>
    /// Computes a value using the registered function.
    /// </summary>
    /// <param name="functionName">The compute function name.</param>
    /// <param name="parameters">The generation parameters.</param>
    /// <returns>The computed value, or null if function not found.</returns>
    public object? Compute(string functionName, GenerationParameters parameters)
    {
        if (_computers.TryGetValue(functionName, out var func))
        {
            try
            {
                var result = func(parameters);
                _logger.LogDebug("Computed '{FunctionName}' = {Result}", functionName, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error computing '{FunctionName}'", functionName);
                return null;
            }
        }

        _logger.LogWarning("Unknown compute function: '{FunctionName}'", functionName);
        return null;
    }

    /// <summary>
    /// Resolves a value, computing it if it's a $compute: marker.
    /// </summary>
    /// <param name="value">The value to resolve (may be a compute marker or regular value).</param>
    /// <param name="parameters">The generation parameters.</param>
    /// <returns>The resolved value.</returns>
    public object? ResolveValue(object? value, GenerationParameters parameters)
    {
        if (value is string strValue && IsComputeMarker(strValue))
        {
            var functionName = GetComputeFunctionName(strValue);
            if (functionName != null)
            {
                return Compute(functionName, parameters);
            }
        }

        return value;
    }

    /// <summary>
    /// Gets the list of registered compute function names.
    /// </summary>
    public IEnumerable<string> GetRegisteredFunctions() => _computers.Keys;
}
