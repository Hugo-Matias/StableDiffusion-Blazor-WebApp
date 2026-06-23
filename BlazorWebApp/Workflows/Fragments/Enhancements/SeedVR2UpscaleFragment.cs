using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Enhancements;

/// <summary>
/// Fragment that upscales images using SeedVR2 video upscaler.
/// Overwrites image_output in the node registry.
/// Conditional: Only builds when seed_vr2.IsActive is true.
/// </summary>
public class SeedVR2UpscaleFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "seed_vr2",
        Type = FragmentType.Enhancement,
        Title = "SeedVR2 Upscale",
        Component = "SeedVR2Form",
        Icon = "fa-solid fa-expand",
        Order = 100,
        Collapsible = true,
        DefaultCollapsed = true,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "seedvr2_model",
                Label = "Model",
                Type = ParameterType.Select,
                Source = new DynamicSource("SeedVR2LoadDiTModel", "model")
            },
            new FragmentParameter
            {
                Name = "seedvr2_vae_model",
                Label = "VAE Model",
                Type = ParameterType.Select,
                Source = new DynamicSource("SeedVR2LoadVAEModel", "model")
            },
            new FragmentParameter
            {
                Name = "seedvr2_seed",
                Label = "Seed",
                Type = ParameterType.Number,
                Min = -1,
                DefaultValue = 42L
            },
            new FragmentParameter
            {
                Name = "seedvr2_resolution",
                Label = "Resolution",
                Type = ParameterType.Slider,
                Min = 512,
                Max = 4096,
                Step = 64,
                DefaultValue = 2048
            },
            new FragmentParameter
            {
                Name = "seedvr2_batch_size",
                Label = "Batch Size",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 10,
                Step = 1,
                DefaultValue = 1
            },
            new FragmentParameter
            {
                Name = "seedvr2_input_noise_scale",
                Label = "Input Noise Scale",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 1,
                Step = 0.01,
                DefaultValue = 0.0
            },
            new FragmentParameter
            {
                Name = "seedvr2_latent_noise_scale",
                Label = "Latent Noise Scale",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 1,
                Step = 0.01,
                DefaultValue = 0.0
            },
            new FragmentParameter
            {
                Name = "blocks_to_swap",
                Label = "Blocks to Swap",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 36,
                Step = 1,
                DefaultValue = 36
            },
            new FragmentParameter
            {
                Name = "vae_tile_size",
                Label = "VAE Tile Size",
                Type = ParameterType.Slider,
                Min = 128,
                Max = 2048,
                Step = 64,
                DefaultValue = 1024
            },
            new FragmentParameter
            {
                Name = "vae_tile_overlap",
                Label = "VAE Tile Overlap",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 512,
                Step = 32,
                DefaultValue = 128
            }
        ]
    };

    /// <summary>
    /// Parameters for the SeedVR2 upscale fragment.
    /// </summary>
    public class Parameters
    {
        public string Model { get; set; } = "seedvr2_ema_7b-Q4_K_M.gguf";
        public string VaeModel { get; set; } = "ema_vae_fp16.safetensors";
        public long Seed { get; set; } = 42;
        public int Resolution { get; set; } = 2048;
        public int BatchSize { get; set; } = 1;
        public int AutoBatchSizeFrameCount { get; set; }
        public double InputNoiseScale { get; set; } = 0.0;
        public double LatentNoiseScale { get; set; } = 0.0;
        public int BlocksToSwap { get; set; } = 36;
        public int VaeTileSize { get; set; } = 1024;
        public int VaeTileOverlap { get; set; } = 128;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        Build(builder, parameters, registry, ResolveAutoBatchSizeFrameCount(parameters));
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        int autoBatchSizeFrameCount)
    {
        var fragment = parameters.GetFragment(Metadata.Id);

        // Check if fragment is active
        if (fragment?.IsActive != true)
            return;

        var p = new Parameters
        {
            Model = fragment.GetString("seedvr2_model", "seedvr2_ema_7b-Q4_K_M.gguf"),
            VaeModel = fragment.GetString("seedvr2_vae_model", "ema_vae_fp16.safetensors"),
            Seed = fragment.GetLong("seedvr2_seed", 42),
            Resolution = fragment.GetInt("seedvr2_resolution", 2048),
            BatchSize = fragment.GetInt("seedvr2_batch_size", 1),
            AutoBatchSizeFrameCount = autoBatchSizeFrameCount,
            InputNoiseScale = fragment.GetDouble("seedvr2_input_noise_scale", 0.0),
            LatentNoiseScale = fragment.GetDouble("seedvr2_latent_noise_scale", 0.0),
            BlocksToSwap = fragment.GetInt("blocks_to_swap", 36),
            VaeTileSize = fragment.GetInt("vae_tile_size", 1024),
            VaeTileOverlap = fragment.GetInt("vae_tile_overlap", 128)
        };

        BuildInternal(builder, registry, p);
    }

    /// <summary>
    /// Builds the fragment with explicit parameters.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters fragmentParams)
    {
        BuildInternal(builder, registry, fragmentParams);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters p)
    {
        // Get image reference
        var imageRef = registry.GetRef("image_output");
        var batchSize = ResolveBatchSize(p.BatchSize, p.AutoBatchSizeFrameCount);

        // Load DiT model
        builder.AddNode("seedvr2_load_dit", node => node
            .Type("SeedVR2LoadDiTModel")
            .Title("SeedVR2 (Down)Load DiT Model")
            .Input("model", p.Model)
            .Input("device", "cuda:0")
            .Input("blocks_to_swap", p.BlocksToSwap)
            .Input("swap_io_components", false)
            .Input("offload_device", "cpu")
            .Input("cache_model", false)
            .Input("attention_mode", "sageattn_2"));

        // Load VAE model
        builder.AddNode("seedvr2_load_vae", node => node
            .Type("SeedVR2LoadVAEModel")
            .Title("SeedVR2 (Down)Load VAE Model")
            .Input("model", p.VaeModel)
            .Input("device", "cuda:0")
            .Input("encode_tiled", true)
            .Input("encode_tile_size", p.VaeTileSize)
            .Input("encode_tile_overlap", p.VaeTileOverlap)
            .Input("decode_tiled", true)
            .Input("decode_tile_size", p.VaeTileSize)
            .Input("decode_tile_overlap", p.VaeTileOverlap)
            .Input("tile_debug", "false")
            .Input("offload_device", "cpu")
            .Input("cache_model", false));

        // Upscaler
        builder.AddNode("seedvr2_upscaler", node => node
            .Type("SeedVR2VideoUpscaler")
            .Title("SeedVR2 Video Upscaler (v2.5.18)")
            .Input("seed", p.Seed)
            .Input("resolution", p.Resolution)
            .Input("max_resolution", p.Resolution)
            .Input("batch_size", batchSize)
            .Input("uniform_batch_size", false)
            .Input("color_correction", "lab")
            .Input("temporal_overlap", 16)
            .Input("prepend_frames", 0)
            .Input("input_noise_scale", p.InputNoiseScale)
            .Input("latent_noise_scale", p.LatentNoiseScale)
            .Input("offload_device", "cpu")
            .Input("enable_debug", false)
            .InputRef("image", imageRef)
            .InputRef("dit", ("seedvr2_load_dit", 0))
            .InputRef("vae", ("seedvr2_load_vae", 0)));

        // Overwrite image_output with upscaled version
        registry.Register("image_output", "seedvr2_upscaler", 0);
    }

    private static int ResolveBatchSize(int requestedBatchSize, int autoBatchSizeFrameCount)
    {
        if (requestedBatchSize > 0)
            return requestedBatchSize;

        return autoBatchSizeFrameCount > 0 ? autoBatchSizeFrameCount : 1;
    }

    private static int ResolveAutoBatchSizeFrameCount(GenerationParameters parameters)
    {
        if (parameters.Sources == null)
            return 0;

        foreach (var source in parameters.Sources.Values)
        {
            if (!string.Equals(source.Type, "video", StringComparison.OrdinalIgnoreCase) && source.VideoOptions == null)
                continue;

            var frameLoadCap = source.VideoOptions?.FrameLoadCap ?? 0;
            if (frameLoadCap > 0)
                return frameLoadCap;
        }

        return 0;
    }
}
