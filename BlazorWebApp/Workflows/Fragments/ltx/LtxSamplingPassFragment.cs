using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Fragment that runs one sampling pass using SamplerCustomAdvanced with ManualSigmas.
/// Pipeline: RandomNoise -> KSamplerSelect -> ManualSigmas -> CFGGuider -> SamplerCustomAdvanced.
/// Called twice per workflow: pass 1 (generation) and pass 2 (refinement).
/// Reads: model_output, positive/negative conditioning, av_latent_output.
/// Overwrites: av_latent_output.
/// </summary>
public class LtxSamplingPassFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_sampling_pass",
        Type = FragmentType.Sampler,
        Title = "LTX Sampling Pass",
        IsHidden = true
    };

    public class Parameters
    {
        public string PassId { get; set; } = "pass1";
        public long Seed { get; set; } = 42;
        public double Cfg { get; set; } = 1.0;
        public string SamplerName { get; set; } = "euler_ancestral_cfg_pp";
        public string Sigmas { get; set; } = "1.0, 0.99375, 0.9875, 0.98125, 0.975, 0.909375, 0.725, 0.421875, 0.0";
        /// <summary>
        /// When true, the fragment skips emitting <c>ManualSigmas</c> and instead consumes
        /// <c>{scope}sigmas_output</c> from the registry (produced by <see cref="LtxSchedulerFragment"/>
        /// or by an explicit upstream sigmas node). Defaults to false so existing workflows
        /// that rely on <see cref="Sigmas"/> remain unchanged.
        /// </summary>
        public bool UseRegistrySigmas { get; set; } = false;
        /// <summary>
        /// Registry key (relative to <c>scope</c>) to look up when <see cref="UseRegistrySigmas"/> is true.
        /// </summary>
        public string SigmasInputName { get; set; } = "sigmas_output";
        public string PositiveInputName { get; set; } = "ltx_positive_output";
        public string NegativeInputName { get; set; } = "ltx_negative_output";
        public string LatentInputName { get; set; } = "av_latent_output";
        public string Title { get; set; } = "SamplerCustomAdvanced";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, new Parameters(), scope, scopeTitle);
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
        var noiseId = $"{scope}{p.PassId}_noise";
        var selectId = $"{scope}{p.PassId}_sampler_select";
        var sigmasId = $"{scope}{p.PassId}_sigmas";
        var guiderId = $"{scope}{p.PassId}_guider";
        var samplerId = $"{scope}{p.PassId}_sampler";

        var modelRef = registry.GetRef($"{scope}model_output");
        var positiveRef = registry.GetRef($"{scope}{p.PositiveInputName}");
        var negativeRef = registry.GetRef($"{scope}{p.NegativeInputName}");
        var latentRef = registry.GetRef($"{scope}{p.LatentInputName}");

        // RandomNoise
        builder.AddNode(noiseId, node => node
            .Type("RandomNoise")
            .Title($"{scopeTitle}RandomNoise")
            .Input("noise_seed", p.Seed));

        // KSamplerSelect
        builder.AddNode(selectId, node => node
            .Type("KSamplerSelect")
            .Title($"{scopeTitle}KSamplerSelect")
            .Input("sampler_name", p.SamplerName));

        // Sigmas: either consume from the registry (LtxSchedulerFragment / explicit upstream)
        // or emit a ManualSigmas node from the literal Parameters.Sigmas string.
        (string nodeId, int index) sigmasRef;
        if (p.UseRegistrySigmas)
        {
            sigmasRef = registry.GetRef($"{scope}{p.SigmasInputName}");
        }
        else
        {
            builder.AddNode(sigmasId, node => node
                .Type("ManualSigmas")
                .Title($"{scopeTitle}ManualSigmas")
                .Input("sigmas", p.Sigmas));
            sigmasRef = (sigmasId, 0);
        }

        // CFGGuider
        builder.AddNode(guiderId, node => node
            .Type("CFGGuider")
            .Title($"{scopeTitle}CFGGuider")
            .Input("cfg", p.Cfg)
            .InputRef("model", modelRef)
            .InputRef("positive", positiveRef)
            .InputRef("negative", negativeRef));

        // SamplerCustomAdvanced
        builder.AddNode(samplerId, node => node
            .Type("SamplerCustomAdvanced")
            .Title($"{scopeTitle}{p.Title}")
            .InputFromNode("noise", noiseId, 0)
            .InputFromNode("guider", guiderId, 0)
            .InputFromNode("sampler", selectId, 0)
            .InputRef("sigmas", sigmasRef)
            .InputRef("latent_image", latentRef));

        registry.Register($"{scope}av_latent_output", samplerId, 0);
    }
}
