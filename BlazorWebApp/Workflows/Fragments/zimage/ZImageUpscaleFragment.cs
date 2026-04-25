using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.zimage;

/// <summary>
/// Merged fragment that combines QwenImageDiffsynth ControlNet and UltimateSDUpscale into
/// a single Z-Image Turbo upscale pipeline. Applies ControlNet tile guidance then performs
/// tiled upscale. Overwrites model_output (ControlNet patch) and image_output (upscaled result).
/// </summary>
public class ZImageUpscaleFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "zimage_upscale",
        Type = FragmentType.Enhancement,
        Title = "Z-Image Turbo Upscale",
        Component = "ZImageUpscaleForm",
        Icon = "fa-solid fa-magnifying-glass-plus",
        Order = 60,
        Collapsible = true,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "upscale_by",
                Label = "Upscale Factor",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 4,
                Step = 0.1,
                DefaultValue = 1.5
            },
            new FragmentParameter
            {
                Name = "strength",
                Label = "ControlNet Strength",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 2,
                Step = 0.01,
                DefaultValue = 0.2
            }
        ]
    };

    /// <summary>
    /// Parameters for the Z-Image Turbo Upscale fragment.
    /// </summary>
    public class Parameters
    {
        public double UpscaleBy { get; set; } = 1.5;
        public double Strength { get; set; } = 0.2;
        public string Scope { get; set; } = "";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        var upscaleBy = fragment?.GetDouble("upscale_by", 1.5) ?? 1.5;
        var strength = fragment?.GetDouble("strength", 0.2) ?? 0.2;

        BuildInternal(builder, registry, new Parameters
        {
            UpscaleBy = upscaleBy,
            Strength = strength,
            Scope = scope
        }, scope, scopeTitle);
    }

    /// <summary>
    /// Builds the fragment with explicit parameters.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, fragmentParams, scope, scopeTitle);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters p,
        string scope,
        string scopeTitle)
    {
        // === ControlNet Phase ===
        var controlnetNodeId = $"{scope}qwen_controlnet";

        // Get references from registry
        var modelRef = registry.GetRef($"{p.Scope}model_output");
        var modelPatchRef = registry.GetRef($"{p.Scope}model_patch_output");
        var vaeRef = registry.GetRef($"{p.Scope}vae_output");
        var tileMapRef = registry.GetRef($"{p.Scope}tile_map_output");

        builder.AddNode(controlnetNodeId, node => node
            .Type("QwenImageDiffsynthControlnet")
            .Title($"{scopeTitle}Qwen Image Diffsynth ControlNet")
            .Input("strength", p.Strength)
            .InputRef("model", modelRef)
            .InputRef("model_patch", modelPatchRef)
            .InputRef("vae", vaeRef)
            .InputRef("image", tileMapRef));

        // Overwrite model_output with ControlNet-patched model
        registry.Register($"{p.Scope}model_output", controlnetNodeId, 0);

        // === UltimateSDUpscale Phase ===
        var upscaleNodeId = $"{scope}ultimate_sd_upscale";

        var imageRef = registry.GetRef("image_output");
        var patchedModelRef = registry.GetRef($"{p.Scope}model_output");
        var positiveRef = registry.GetRef($"{scope}positive_output");
        var negativeRef = registry.GetRef($"{scope}negative_output");
        var upscaleVaeRef = registry.GetRef($"{scope}vae_output");
        var upscaleModelRef = registry.GetRef($"{scope}upscale_model_output");

        // Calculate tile dimensions: a * b / 2 + 32 (from MathExpression nodes in workflow)
        // Base resolution is typically 872x1248 latent -> ~1568x1356 pixel
        // Tile size = upscale_by * base / 2 + 32
        var tileWidth = (int)(p.UpscaleBy * 1568 / 2 + 32);
        var tileHeight = (int)(p.UpscaleBy * 1356 / 2 + 32);

        // Ensure tile dimensions are multiples of 8
        tileWidth = (tileWidth / 8) * 8;
        tileHeight = (tileHeight / 8) * 8;

        builder.AddNode(upscaleNodeId, node => node
            .Type("UltimateSDUpscale")
            .Title($"{scopeTitle}Ultimate SD Upscale")
            .Input("upscale_model", "enabled")
            .Input("upscale_by", p.UpscaleBy)
            .Input("tile_width", tileWidth)
            .Input("tile_height", tileHeight)
            .Input("tile_overlap", 64)
            .Input("denoise_strength", 0.4)
            .Input("mask_blur", 8)
            .Input("mask_rounding", 0)
            .Input("padding", 32)
            .Input("seam_fix_mode", "None")
            .Input("force_tile", false)
            .InputRef("image", imageRef)
            .InputRef("model", patchedModelRef)
            .InputRef("positive", positiveRef)
            .InputRef("negative", negativeRef)
            .InputRef("vae", upscaleVaeRef)
            .InputRef("upscale_model", upscaleModelRef));

        // Overwrite image_output with upscaled image
        registry.Register("image_output", upscaleNodeId, 0);
    }
}
