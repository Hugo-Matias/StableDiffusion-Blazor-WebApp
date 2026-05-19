using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Enhancements;

/// <summary>
/// Generic pixel-space enhancement that runs ImageUpscaleWithModel against the
/// current image_output and optionally blends the model output back over the source.
/// </summary>
public class UpscaleWithModelFragment : IFragmentBuilder
{
    public string Id { get; init; } = "upscale_with_model";
    public string Title { get; init; } = "Upscale With Model";
    public int Order { get; init; } = 125;
    public bool Collapsible { get; init; } = true;
    public bool DefaultCollapsed { get; init; } = true;

    public FragmentMetadata Metadata => new()
    {
        Id = Id,
        Type = FragmentType.Enhancement,
        Title = Title,
        Component = "UpscaleWithModelForm",
        Icon = "fa-solid fa-wand-magic-sparkles",
        Order = Order,
        Collapsible = Collapsible,
        DefaultCollapsed = DefaultCollapsed,
        Description = "Applies a selected pixel-space upscale model to the current image output. Use 1x models for refinement or contrast/detail boosts, and 2x or larger models when you want the model itself to increase image size.",
        Parameters =
        [
            new FragmentParameter
            {
                Name = "upscale_model",
                Label = "Upscale Model",
                Type = ParameterType.Select,
                Source = new DynamicSource("UpscaleModelLoader", "model_name"),
                DefaultValue = "1xSkinContrast-High-SuperUltraCompact.pth"
            },
            new FragmentParameter
            {
                Name = "blend_with_input",
                Label = "Blend With Input",
                Type = ParameterType.Checkbox,
                DefaultValue = true
            },
            new FragmentParameter
            {
                Name = "blend_factor",
                Label = "Blend Strength",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 1,
                Step = 0.01,
                DefaultValue = 0.4
            },
            new FragmentParameter
            {
                Name = "blend_mode",
                Label = "Blend Mode",
                Type = ParameterType.Select,
                Options = ["normal", "multiply", "screen", "overlay", "soft_light", "difference"],
                DefaultValue = "normal"
            }
        ]
    };

    public class Parameters
    {
        public string UpscaleModel { get; set; } = "1xSkinContrast-High-SuperUltraCompact.pth";
        public bool BlendWithInput { get; set; } = true;
        public double BlendFactor { get; set; } = 0.4;
        public string BlendMode { get; set; } = "normal";
        public string Scope { get; set; } = "";
        public string ScopeTitle { get; set; } = "";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Id);
        if (fragment?.IsActive != true)
        {
            return;
        }

        Build(builder, registry, new Parameters
        {
            UpscaleModel = fragment.GetString("upscale_model", "1xSkinContrast-High-SuperUltraCompact.pth"),
            BlendWithInput = fragment.GetBool("blend_with_input", true),
            BlendFactor = fragment.GetDouble("blend_factor", 0.4),
            BlendMode = fragment.GetString("blend_mode", "normal") ?? "normal",
            Scope = scope,
            ScopeTitle = scopeTitle
        });
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters parameters)
    {
        var nodePrefix = string.IsNullOrWhiteSpace(parameters.Scope)
            ? "upscale_with_model"
            : $"{parameters.Scope}upscale_with_model";
        var titlePrefix = parameters.ScopeTitle;
        var imageRef = registry.GetRef("image_output");

        var loaderNodeId = $"{nodePrefix}_loader";
        builder.AddNode(loaderNodeId, node => node
            .Type("UpscaleModelLoader")
            .Title($"{titlePrefix}Load Upscale Model")
            .Input("model_name", parameters.UpscaleModel));

        var upscaleNodeId = $"{nodePrefix}_image";
        builder.AddNode(upscaleNodeId, node => node
            .Type("ImageUpscaleWithModel")
            .Title($"{titlePrefix}Upscale Image With Model")
            .InputRef("image", imageRef)
            .InputFromNode("upscale_model", loaderNodeId, 0));

        if (!parameters.BlendWithInput)
        {
            registry.Register("image_output", upscaleNodeId, 0);
            return;
        }

        var blendNodeId = $"{nodePrefix}_blend";
        builder.AddNode(blendNodeId, node => node
            .Type("ImageBlend")
            .Title($"{titlePrefix}Blend Upscale Model Output")
            .Input("blend_factor", Math.Clamp(parameters.BlendFactor, 0, 1))
            .Input("blend_mode", NormalizeBlendMode(parameters.BlendMode))
            .InputRef("image1", imageRef)
            .InputFromNode("image2", upscaleNodeId, 0));

        registry.Register("image_output", blendNodeId, 0);
    }

    private static string NormalizeBlendMode(string? blendMode)
    {
        var value = blendMode?.Trim();
        return value is "multiply" or "screen" or "overlay" or "soft_light" or "difference"
            ? value
            : "normal";
    }
}
