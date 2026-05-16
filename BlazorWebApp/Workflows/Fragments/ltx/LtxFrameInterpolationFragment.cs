using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

public class LtxFrameInterpolationFragment : IFragmentBuilder
{
    public Parameters Defaults { get; init; } = new();

    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_frame_interpolation",
        Type = FragmentType.Enhancement,
        Title = "Frame Interpolation",
        Component = "LtxFrameInterpolationForm",
        Icon = "fa-solid fa-film",
        Order = 80,
        Collapsible = true,
        DefaultCollapsed = false,
        Parameters =
        [
            new FragmentParameter { Name = "rife_model", Label = "RIFE Model", Type = ParameterType.Select, Options = ["rife49.pth", "rife48.pth", "rife47.pth", "rife46.pth", "rife45.pth"], DefaultValue = Defaults.RifeModel },
            new FragmentParameter { Name = "frame_multiplier", Label = "Frame Multiplier", Type = ParameterType.Slider, Min = 1, Max = 4, Step = 1, DefaultValue = Defaults.FrameMultiplier },
            new FragmentParameter { Name = "scale_factor", Label = "Scale Factor", Type = ParameterType.Select, Options = ["0.25", "0.5", "1.0", "2.0", "4.0"], DefaultValue = Defaults.ScaleFactor }
        ]
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        var p = new Parameters
        {
            RifeModel = fragment?.GetString("rife_model", Defaults.RifeModel) ?? Defaults.RifeModel,
            FrameMultiplier = fragment?.GetInt("frame_multiplier", Defaults.FrameMultiplier) ?? Defaults.FrameMultiplier,
            ScaleFactor = fragment?.GetFloat("scale_factor", Defaults.ScaleFactor) ?? Defaults.ScaleFactor
        };

        Build(builder, registry, p, scope, scopeTitle);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope = "",
        string scopeTitle = "")
    {
        var nodeId = $"{scope}ltx_rife_interpolation";

        builder.AddNode(nodeId, node => node
            .Type("RIFE VFI")
            .Title($"{scopeTitle}RIFE VFI")
            .InputRef("frames", registry.GetRef($"{scope}{p.ImageInputName}"))
            .Input("ckpt_name", p.RifeModel)
            .Input("clear_cache_after_n_frames", 50)
            .Input("multiplier", p.FrameMultiplier)
            .Input("fast_mode", false)
            .Input("ensemble", true)
            .Input("scale_factor", p.ScaleFactor));

        registry.Register($"{scope}{p.ImageInputName}", nodeId, 0);
    }

    public class Parameters
    {
        public string RifeModel { get; set; } = "rife49.pth";
        public int FrameMultiplier { get; set; } = 2;
        public float ScaleFactor { get; set; } = 1.0f;
        public string ImageInputName { get; set; } = "image_output";
    }
}