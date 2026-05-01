using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Enhancements;

/// <summary>
/// LTX 2.3 third-pass enhancement. Flag-only fragment: <see cref="Build"/> is a no-op.
/// The workflow class consumes <c>IsActive</c> and the fragment parameters to drive a
/// third sampling pass after the Phase 0 refinement pass.
///
/// Default <c>IsActive = false</c>. Activating this while
/// <c>ltx_refinement_pass</c> is off auto-enables refinement at build time
/// (the third pass needs the cropped conditioning and full-res latent that
/// the refinement pass produces).
/// </summary>
public class LtxThirdPassEnhancementFragment : IFragmentBuilder
{
    public const string FragmentId = "ltx_third_pass";

    public FragmentMetadata Metadata => new()
    {
        Id = FragmentId,
        Type = FragmentType.Enhancement,
        Title = "Third Pass (refinement extension)",
        Description = "Adds an optional third sampling pass on top of the Refinement Pass. " +
                      "This extra pass can push detail and temporal coherence further, at the cost " +
                      "of additional generation time. It is off by default and is only useful once " +
                      "you are satisfied with composition from Pass 1 and the Refinement Pass. " +
                      "Enabling the Third Pass automatically enables the Refinement Pass if it is " +
                      "currently off, since it relies on the cropped conditioning and full-res " +
                      "latent that the Refinement Pass produces.",
        Icon = "fa-solid fa-wand-magic-sparkles",
        Component = "LtxThirdPassForm",
        Order = 73,
        Collapsible = true,
        DefaultCollapsed = true,
        DefaultActive = false,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "sampler_name",
                Label = "Sampler",
                Type = ParameterType.Select,
                Source = new DynamicSource("KSamplerSelect", "sampler_name"),
                DefaultValue = "euler_cfg_pp"
            },
            new FragmentParameter
            {
                Name = "cfg",
                Label = "CFG",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 10,
                Step = 0.1,
                DefaultValue = 1.0
            },
            new FragmentParameter
            {
                Name = "seed",
                Label = "Seed",
                Type = ParameterType.Number,
                Min = -1,
                DefaultValue = 43L
            },
            new FragmentParameter
            {
                Name = "sigmas_mode",
                Label = "Sigmas",
                Type = ParameterType.Select,
                Options = ["auto", "manual"],
                DefaultValue = "auto",
                Description = "auto: rebuild LTXVScheduler at full resolution. manual: use the curve below."
            },
            new FragmentParameter
            {
                Name = "manual_sigmas",
                Label = "Manual Sigmas",
                Type = ParameterType.TextArea,
                DefaultValue = "0.85, 0.7250, 0.4219, 0.0",
                Description = "Used when Sigmas = manual. Matches upstream pass-3 ManualSigmas curve."
            }
        ]
    };

    /// <summary>No-op. Third pass is orchestrated by the workflow class.</summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
    }
}
