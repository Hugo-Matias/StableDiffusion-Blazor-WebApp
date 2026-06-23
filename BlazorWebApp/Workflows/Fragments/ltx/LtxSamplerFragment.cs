using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// UI-only fragment that exposes seed and CFG controls for LTX sampling.
/// Parameters are consumed by the workflow's Build() method.
/// No nodes are created by this fragment.
/// </summary>
public class LtxSamplerFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_sampler",
        Type = FragmentType.Sampler,
        Title = "Sampler",
        Component = "LtxSamplerForm",
        Icon = "fa-solid fa-dice",
        Order = 50,
        Collapsible = true,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "seed",
                Label = "Seed",
                Type = ParameterType.Number,
                Min = -1,
                DefaultValue = -1L
            },
            new FragmentParameter
            {
                Name = "cfg",
                Label = "CFG Scale",
                Type = ParameterType.Slider,
                Min = 1.0,
                Max = 10.0,
                Step = 0.5,
                DefaultValue = 1.0
            }
        ]
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        // UI-only fragment - parameters are consumed by the workflow's Build() method
    }
}
