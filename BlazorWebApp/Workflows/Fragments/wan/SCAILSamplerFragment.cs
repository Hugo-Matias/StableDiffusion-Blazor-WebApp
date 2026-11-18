using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that builds the SCAIL sampler chain including scheduler, extra args, context options, and sampling.
/// Pipeline: Scheduler -> ExtraArgs -> ContextOptions -> Samplerv2 -> Decode
/// Requires registry: model, image_embeds, text_embeds, vae
/// Registers latent_output (from decode).
/// </summary>
public class SCAILSamplerFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "scail_sampler",
        Type = FragmentType.Sampler,
        Title = "SCAIL Sampler",
        Component = "SCAILSamplerForm",
        IsHidden = false
    };

    public class Parameters
    {
        // Scheduler
        public string SchedulerName { get; set; } = "dpm++_sde";
        public int Steps { get; set; } = 6;
        public float Shift { get; set; } = 7.0f;
        public int StartStep { get; set; } = 0;
        public int EndStep { get; set; } = -1;

        // Sampler extra args
        public int RiflexFreqIndex { get; set; } = 0;
        public string RopeFunction { get; set; } = "comfy";

        // Context options
        public string ContextSchedule { get; set; } = "uniform_standard";
        public int ContextFrames { get; set; } = 81;
        public int ContextStride { get; set; } = 4;
        public int ContextOverlap { get; set; } = 48;
        public bool ContextFreenoise { get; set; } = true;
        public string FuseMethod { get; set; } = "linear";
        public bool Verbose { get; set; } = false;

        // Sampler
        public float Cfg { get; set; } = 1.0f;
        public long Seed { get; set; } = 42;
        public bool ForceOffload { get; set; } = true;
        public bool AddNoiseToSamples { get; set; } = false;

        // Decode
        public bool EnableVaeTiling { get; set; } = false;
        public int TileX { get; set; } = 272;
        public int TileY { get; set; } = 272;
        public int TileStrideX { get; set; } = 144;
        public int TileStrideY { get; set; } = 128;
        public string Normalization { get; set; } = "default";
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
            SchedulerName = fragment?.GetString("scheduler_name", "dpm++_sde") ?? "dpm++_sde",
            Steps = fragment?.GetInt("steps", 6) ?? 6,
            Shift = fragment?.GetFloat("shift", 7.0f) ?? 7.0f,
            StartStep = fragment?.GetInt("start_step", 0) ?? 0,
            EndStep = fragment?.GetInt("end_step", -1) ?? -1,
            RiflexFreqIndex = fragment?.GetInt("riflex_freq_index", 0) ?? 0,
            RopeFunction = fragment?.GetString("rope_function", "comfy") ?? "comfy",
            ContextSchedule = fragment?.GetString("context_schedule", "uniform_standard") ?? "uniform_standard",
            ContextFrames = fragment?.GetInt("context_frames", 81) ?? 81,
            ContextStride = fragment?.GetInt("context_stride", 4) ?? 4,
            ContextOverlap = fragment?.GetInt("context_overlap", 48) ?? 48,
            ContextFreenoise = fragment?.GetBool("context_freenoise", true) ?? true,
            FuseMethod = fragment?.GetString("fuse_method", "linear") ?? "linear",
            Cfg = fragment?.GetFloat("cfg", 1.0f) ?? 1.0f,
            Seed = fragment?.GetInt("seed", 42) ?? 42,
            ForceOffload = fragment?.GetBool("force_offload", true) ?? true,
            AddNoiseToSamples = fragment?.GetBool("add_noise_to_samples", false) ?? false,
            EnableVaeTiling = fragment?.GetBool("enable_vae_tiling", false) ?? false,
            TileX = fragment?.GetInt("tile_x", 272) ?? 272,
            TileY = fragment?.GetInt("tile_y", 272) ?? 272,
            TileStrideX = fragment?.GetInt("tile_stride_x", 144) ?? 144,
            TileStrideY = fragment?.GetInt("tile_stride_y", 128) ?? 128,
            Normalization = fragment?.GetString("normalization", "default") ?? "default"
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
        var schedulerId = $"{scope}scheduler";
        var extraArgsId = $"{scope}extra_args";
        var contextOptionsId = $"{scope}context_options";
        var samplerId = $"{scope}sampler";
        var decodeId = $"{scope}decode";

        var modelRef = registry.GetRef("model");
        var imageEmbedsRef = registry.GetRef($"{scope}image_embeds");
        var textEmbedsRef = registry.GetRef("text_embeds");
        var vaeRef = registry.GetRef("vae");

        // 1. WanVideoSchedulerv2 - creates sigma schedule
        builder.AddNode(schedulerId, node => node
            .Type("WanVideoSchedulerv2")
            .Title($"{scopeTitle}Scheduler")
            .Input("scheduler", p.SchedulerName)
            .Input("steps", p.Steps)
            .Input("shift", p.Shift)
            .Input("start_step", p.StartStep)
            .Input("end_step", p.EndStep));

        // 2. WanVideoContextOptions - builds context window options for long videos.
        builder.AddNode(contextOptionsId, node => node
            .Type("WanVideoContextOptions")
            .Title($"{scopeTitle}Context Options")
            .Input("context_schedule", p.ContextSchedule)
            .Input("context_frames", p.ContextFrames)
            .Input("context_stride", p.ContextStride)
            .Input("context_overlap", p.ContextOverlap)
            .Input("freenoise", p.ContextFreenoise)
            .Input("fuse_method", p.FuseMethod)
            .Input("verbose", p.Verbose));

        // 3. WanVideoSamplerExtraArgs - connects context options into the sampler.
        builder.AddNode(extraArgsId, node => node
            .Type("WanVideoSamplerExtraArgs")
            .Title($"{scopeTitle}Sampler Extra Args")
            .Input("riflex_freq_index", p.RiflexFreqIndex)
            .Input("rope_function", p.RopeFunction)
            .InputFromNode("context_options", contextOptionsId, 0));

        // 4. WanVideoSamplerv2 - runs the actual sampling
        builder.AddNode(samplerId, node => node
            .Type("WanVideoSamplerv2")
            .Title($"{scopeTitle}Sampler")
            .Input("cfg", p.Cfg)
            .Input("seed", p.Seed)
            .Input("force_offload", p.ForceOffload)
            .Input("add_noise_to_samples", p.AddNoiseToSamples)
            .InputRef("model", modelRef)
            .InputRef("image_embeds", imageEmbedsRef)
            .InputRef("scheduler", (schedulerId, 0))
            .InputRef("text_embeds", textEmbedsRef)
            .InputRef("extra_args", (extraArgsId, 0)));

        // 5. WanVideoDecode - decodes latents to pixel space
        builder.AddNode(decodeId, node => node
            .Type("WanVideoDecode")
            .Title($"{scopeTitle}VAE Decode")
            .Input("enable_vae_tiling", p.EnableVaeTiling)
            .Input("tile_x", p.TileX)
            .Input("tile_y", p.TileY)
            .Input("tile_stride_x", p.TileStrideX)
            .Input("tile_stride_y", p.TileStrideY)
            .Input("normalization", p.Normalization)
            .InputRef("vae", vaeRef)
            .InputFromNode("samples", samplerId, 0));

        registry.Register($"{scope}latent_output", decodeId, 0);
        registry.Register($"{scope}image_output", decodeId, 0);
    }
}
