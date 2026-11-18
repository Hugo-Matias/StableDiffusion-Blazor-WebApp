using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that runs the WanVideo sampler pipeline.
/// Pipeline: WanVideoScheduler -> WanVideoSamplerSettings -> WanVideoSamplerFromSettings
/// Requires registry: model_output, steadydancer_embeds, text_embeds, context_options.
/// Registers latent_output.
/// </summary>
public class SamplerWanFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "sampler_wan",
        Type = FragmentType.Sampler,
        Title = "WanVideo Sampler",
        IsHidden = true
    };

    public class Parameters
    {
        public string Scheduler { get; set; } = "dpm++_sde";
        public int Steps { get; set; } = 4;
        public double Cfg { get; set; } = 1;
        public int Shift { get; set; } = 5;
        public long Seed { get; set; } = 42;
        public double Denoise { get; set; } = 1;
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
        var schedulerId = $"{scope}scheduler";
        var samplerSettingsId = $"{scope}sampler_settings";
        var samplerId = $"{scope}sampler";

        var modelRef = registry.GetRef($"{scope}model_output");
        var embedsRef = registry.GetRef("steadydancer_embeds");
        var textRef = registry.GetRef($"{scope}text_embeds");
        var contextRef = registry.GetRef("context_options");

        // WanVideo Scheduler
        builder.AddNode(schedulerId, node => node
            .Type("WanVideoScheduler")
            .Title($"{scopeTitle}WanVideo Scheduler")
            .Input("scheduler", p.Scheduler)
            .Input("steps", p.Steps)
            .Input("shift", p.Shift)
            .Input("start_step", 0)
            .Input("end_step", -1));

        // WanVideo Sampler Settings
        builder.AddNode(samplerSettingsId, node => node
            .Type("WanVideoSamplerSettings")
            .Title($"{scopeTitle}WanVideo Sampler Settings")
            .Input("steps", p.Steps)
            .Input("cfg", p.Cfg)
            .Input("shift", p.Shift)
            .Input("seed", p.Seed)
            .Input("force_offload", true)
            .InputFromNode("scheduler", schedulerId, 3)
            .Input("riflex_freq_index", 0)
            .Input("denoise_strength", p.Denoise)
            .Input("batched_cfg", false)
            .Input("rope_function", "comfy")
            .Input("start_step", 0)
            .Input("end_step", -1)
            .Input("add_noise_to_samples", false)
            .InputRef("model", modelRef)
            .InputRef("image_embeds", embedsRef)
            .InputRef("text_embeds", textRef)
            .InputRef("context_options", contextRef));

        // WanVideo Sampler From Settings
        builder.AddNode(samplerId, node => node
            .Type("WanVideoSamplerFromSettings")
            .Title($"{scopeTitle}WanVideo Sampler")
            .InputFromNode("sampler_inputs", samplerSettingsId, 0));

        registry.Register($"{scope}latent_output", samplerId, 0);
    }
}
