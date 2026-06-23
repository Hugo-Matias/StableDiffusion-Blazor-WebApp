using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Optional native Wan video enhancement model patch applied before sampling.
/// </summary>
public class WanVideoEnhanceFragment : IFragmentBuilder
{
    public const string FragmentId = "wan_video_enhance";

    public FragmentMetadata Metadata => new()
    {
        Id = FragmentId,
        Type = FragmentType.Enhancement,
        Title = "Wan Video Enhance",
        Component = "WanVideoEnhanceForm",
        Icon = "fa-solid fa-wand-magic-sparkles",
        Order = 65,
        Collapsible = true,
        DefaultCollapsed = true,
        DefaultActive = false,
        Description = "Applies the native WanVideo Enhance-A-Video model patch during sampling. This is a video-time enhancement and may be more temporally stable than frame-by-frame detailer passes.",
        Parameters =
        [
            new FragmentParameter
            {
                Name = "weight",
                Label = "Weight",
                Type = ParameterType.Slider,
                Min = 0.0,
                Max = 10.0,
                Step = 0.001,
                DefaultValue = 2.0
            }
        ]
    };

    public class Parameters
    {
        public double Weight { get; set; } = 2.0;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        if (fragment?.IsActive != true)
        {
            return;
        }

        Build(builder, registry, new Parameters
        {
            Weight = fragment.GetDouble("weight", 2.0)
        });
    }

    public static void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters parameters)
    {
        builder.AddNode(FragmentId, node => node
            .Type("WanVideoEnhanceAVideoKJ")
            .Title("Wan Video Enhance A Video")
            .InputRef("model", registry.GetRef("model_output"))
            .InputRef("latent", registry.GetRef("latent_output"))
            .Input("weight", parameters.Weight));

        registry.Register("model_output", FragmentId, 0);
    }
}