using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that applies frame interpolation using ImageScaleBy -> CleanVRAM -> RIFE VFI.
/// Conditional - only active when IsActive is true on the fragment parameters.
/// Registers frames_output when active.
/// </summary>
public class FrameInterpolationFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "frame_interpolation",
        Type = FragmentType.Enhancement,
        Title = "Frame Interpolation",
        Component = "FrameInterpolationForm",
        Icon = "fa-solid fa-film",
        Order = 70,
        Collapsible = true,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "rife_model",
                Label = "RIFE Model",
                Type = ParameterType.Select,
                DefaultValue = "rife49.pth",
                Options = ["rife49.pth", "rife48.pth", "rife47.pth", "rife46.pth"]
            },
            new FragmentParameter
            {
                Name = "frame_multiplier",
                Label = "Frame Multiplier",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 8,
                Step = 1,
                DefaultValue = 2
            },
            new FragmentParameter
            {
                Name = "scale_by",
                Label = "Scale By",
                Type = ParameterType.Slider,
                Min = 1.0,
                Max = 4.0,
                Step = 0.5,
                DefaultValue = 2.0
            }
        ]
    };

    public class Parameters
    {
        public string RifeModel { get; set; } = "rife49.pth";
        public int FrameMultiplier { get; set; } = 2;
        public double ScaleBy { get; set; } = 2.0;
        public string ImageInputName { get; set; } = "image_output";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);

        BuildInternal(builder, registry, new Parameters
        {
            RifeModel = fragment?.GetString("rife_model", "rife49.pth") ?? "rife49.pth",
            FrameMultiplier = fragment?.GetInt("frame_multiplier", 2) ?? 2,
            ScaleBy = fragment?.GetDouble("scale_by", 2.0) ?? 2.0
        }, scope, scopeTitle);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, fragmentParams, scope, scopeTitle);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope,
        string scopeTitle)
    {
        var upscaleId = $"{scope}upscale_frames";
        var cleanId = $"{scope}clean_upscale";
        var rifeId = $"{scope}frame_interpolation";
        var imageRef = registry.GetRef(p.ImageInputName);

        builder.AddNode(upscaleId, node => node
            .Type("ImageScaleBy")
            .Title($"{scopeTitle}Upscale Image By")
            .Input("upscale_method", "lanczos")
            .Input("scale_by", p.ScaleBy)
            .InputRef("image", imageRef));

        builder.AddNode(cleanId, node => node
            .Type("easy cleanGpuUsed")
            .Title($"{scopeTitle}Clean VRAM (Upscale)")
            .InputFromNode("anything", upscaleId, 0));

        builder.AddNode(rifeId, node => node
            .Type("RIFE VFI")
            .Title($"{scopeTitle}Frame Interpolation (RIFE)")
            .Input("ckpt_name", p.RifeModel)
            .Input("clear_cache_after_n_frames", 10)
            .Input("multiplier", p.FrameMultiplier)
            .Input("fast_mode", false)
            .Input("ensemble", true)
            .Input("scale_factor", 1)
            .Input("torch_compile", false)
            .Input("batch_size", 1)
            .Input("dtype", "bfloat16")
            .InputFromNode("frames", cleanId, 0));

        registry.Register($"{scope}frames_output", rifeId, 0);
    }
}
