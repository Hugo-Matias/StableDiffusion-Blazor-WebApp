using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Enhancements;

/// <summary>
/// LTX 2.3 refinement-pass enhancement. Flag-only fragment: <see cref="Build"/> is a no-op.
/// The workflow class consumes <c>IsActive</c> and the fragment parameters to drive the
/// pass-2 path (half-res pass 1 -&gt; latent upsample -&gt; crop guides -&gt; pass 2).
///
/// Default <c>IsActive = true</c>, matching the upstream <c>LTX-2.3 - I2V_T2V_Basic.json</c>.
/// Toggling off collapses to a single full-resolution pass (matches the upstream
/// <c>*_Simple_single_pass.json</c> variant).
/// </summary>
public class LtxRefinementPassEnhancementFragment : IFragmentBuilder
{
    public const string FragmentId = "ltx_refinement_pass";

    public FragmentMetadata Metadata => new()
    {
        Id = FragmentId,
        Type = FragmentType.Enhancement,
        Title = "Refinement Pass (upscale + 2nd pass)",
        Icon = "fa-solid fa-wand-magic-sparkles",
        Component = "LtxRefinementPassForm",
        Order = 72,
        Collapsible = true,
        DefaultCollapsed = true,
        DefaultActive = true,
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
                DefaultValue = 42L
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
                Description = "Used when Sigmas = manual. Matches upstream Basic refinement curve."
            }
        ]
    };

    /// <summary>No-op. Refinement is orchestrated by the workflow class.</summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
    }
}
