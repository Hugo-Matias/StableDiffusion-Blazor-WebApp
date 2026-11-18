using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Enhancements;

/// <summary>
/// LTX 2.3 Normalized Attention Guidance (NAG) enhancement.
/// When active, the workflow class calls <see cref="BuildPatch(ComfyWorkflowBuilder,NodeRegistry,GenerationParameters,string,string)"/>
/// right after the loader. The patch chains an <c>LTX2_NAG</c> node onto
/// <c>{scope}model_output</c> and re-registers the registry key so downstream
/// fragments use the patched model transparently.
/// </summary>
public class LtxNagEnhancementFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_nag",
        Type = FragmentType.Enhancement,
        Title = "Normalized Attention Guidance",
        Description = "NAG improves prompt adherence and composition sharpness by applying a normalized " +
                      "negative guidance correction inside the attention layers of the diffusion model. " +
                      "It is on by default with conservative values — turn it off only if you notice " +
                      "over-saturation or artefacts, or want a clean baseline for comparison. " +
                      "NAG Scale controls overall guidance strength (higher = stronger correction). " +
                      "NAG Alpha blends the guidance smoothly (lower = more subtle). " +
                      "NAG Tau clips extreme attention values to stabilise generation.",
        Component = "LtxNagEnhancementForm",
        Icon = "fa-solid fa-bullseye",
        Order = 70,
        Collapsible = true,
        DefaultCollapsed = true,
        DefaultActive = true,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "nag_scale",
                Label = "NAG Scale",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 100,
                Step = 0.5,
                DefaultValue = 11.0,
                Description = "Strength of the negative guidance effect. Higher values increase the NAG influence."
            },
            new FragmentParameter
            {
                Name = "nag_alpha",
                Label = "NAG Alpha",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 1,
                Step = 0.01,
                DefaultValue = 0.25,
                Description = "Mixing coefficient that controls the balance between the normalized guided representation and the original positive representation."
            },
            new FragmentParameter
            {
                Name = "nag_tau",
                Label = "NAG Tau",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 10,
                Step = 0.05,
                DefaultValue = 2.5,
                Description = "Clipping threshold that controls how much the guided attention can deviate from the positive attention."
            }
        ]
    };

    public class Parameters
    {
        public double NagScale { get; set; } = 11.0;
        public double NagAlpha { get; set; } = 0.25;
        public double NagTau { get; set; } = 2.5;
    }

    /// <summary>No-op. NAG is a workflow-driven model patch invoked via <see cref="BuildPatch(ComfyWorkflowBuilder,NodeRegistry,GenerationParameters,string,string)"/>.</summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
    }

    /// <summary>
    /// Patches <c>{scope}model_output</c> with <c>LTX2_NAG</c>, reading user values
    /// from <paramref name="parameters"/>.
    /// </summary>
    public void BuildPatch(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        GenerationParameters parameters,
        string scope = "",
        string scopeTitle = "")
    {
        var frag = parameters.GetFragment(Metadata.Id);
        BuildPatch(builder, registry, new Parameters
        {
            NagScale = frag?.GetDouble("nag_scale", 11.0) ?? 11.0,
            NagAlpha = frag?.GetDouble("nag_alpha", 0.25) ?? 0.25,
            NagTau = frag?.GetDouble("nag_tau", 2.5) ?? 2.5
        }, scope, scopeTitle);
    }

    /// <summary>
    /// Patches <c>{scope}model_output</c> with <c>LTX2_NAG</c> using explicit parameters.
    /// </summary>
    public void BuildPatch(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        var nodeId = $"{scope}ltx_nag_patch";
        var modelRef = registry.GetRef($"{scope}model_output");

        builder.AddNode(nodeId, node => node
            .Type("LTX2_NAG")
            .Title($"{scopeTitle}LTX2 NAG")
            .InputRef("model", modelRef)
            .Input("nag_alpha", fragmentParams.NagAlpha)
            .Input("nag_scale", fragmentParams.NagScale)
            .Input("nag_tau", fragmentParams.NagTau));

        registry.Register($"{scope}model_output", nodeId, 0);
    }
}
