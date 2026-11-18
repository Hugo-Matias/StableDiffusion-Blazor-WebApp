using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.zimage;

/// <summary>
/// Merged fragment that combines QwenImageDiffsynth ControlNet and UltimateSDUpscale into
/// a single Z-Image Turbo upscale pipeline. Applies ControlNet tile guidance then performs
/// tiled upscale. Overwrites model_output (ControlNet patch) and image_output (upscaled result).
/// </summary>
public class ZImageUpscaleFragment : IFragmentBuilder
{
    public string Id { get; init; } = "zimage_upscale";
    public string Title { get; init; } = "Z-Image Turbo Upscale";
    public FragmentType Type { get; init; } = FragmentType.Enhancement;
    public int Order { get; init; } = 60;
    public bool Collapsible { get; init; } = true;
    public bool DefaultCollapsed { get; init; } = false;
    public bool? DefaultActive { get; init; }

    public FragmentMetadata Metadata => new()
    {
        Id = Id,
        Type = Type,
        Title = Title,
        Component = "ZImageUpscaleForm",
        Icon = "fa-solid fa-magnifying-glass-plus",
        Order = Order,
        Collapsible = Collapsible,
        DefaultCollapsed = DefaultCollapsed,
        DefaultActive = DefaultActive,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "upscale_by",
                Label = "Upscale Factor",
                Type = ParameterType.Slider,
                Min = 0.05,
                Max = 4,
                Step = 0.05,
                DefaultValue = 2.0
            },
            new FragmentParameter
            {
                Name = "strength",
                Label = "ControlNet Strength",
                Type = ParameterType.Slider,
                Min = -10,
                Max = 10,
                Step = 0.01,
                DefaultValue = 0.2
            },
            new FragmentParameter
            {
                Name = "controlnet_node_type",
                Label = "ControlNet Node",
                Type = ParameterType.Select,
                DefaultValue = "QwenImageDiffsynthControlnet",
                Options = ["QwenImageDiffsynthControlnet", "ZImageFunControlnet"]
            },
            new FragmentParameter
            {
                Name = "upscale_sampler_name",
                Label = "Upscale Sampler",
                Type = ParameterType.Select,
                DefaultValue = "deis_2m",
                Source = new DynamicSource("UltimateSDUpscale", "sampler_name")
            },
            new FragmentParameter
            {
                Name = "upscale_scheduler",
                Label = "Upscale Scheduler",
                Type = ParameterType.Select,
                DefaultValue = "beta",
                Source = new DynamicSource("UltimateSDUpscale", "scheduler")
            },
            new FragmentParameter
            {
                Name = "upscale_steps",
                Label = "Upscale Steps",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 150,
                Step = 1,
                DefaultValue = 6
            },
            new FragmentParameter
            {
                Name = "upscale_cfg",
                Label = "Upscale CFG",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 30,
                Step = 0.5,
                DefaultValue = 1.0
            },
            new FragmentParameter
            {
                Name = "upscale_denoise",
                Label = "Upscale Denoise",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 1,
                Step = 0.01,
                DefaultValue = 0.21
            },
            new FragmentParameter
            {
                Name = "upscale_seed",
                Label = "Upscale Seed",
                Type = ParameterType.Number,
                Min = -1,
                DefaultValue = -1L
            },
            new FragmentParameter
            {
                Name = "auto_tile_size",
                Label = "Auto Tile Size",
                Type = ParameterType.Checkbox,
                DefaultValue = true
            },
            new FragmentParameter
            {
                Name = "tile_width",
                Label = "Tile Width",
                Type = ParameterType.Slider,
                Min = 64,
                Max = 8192,
                Step = 8,
                DefaultValue = 512
            },
            new FragmentParameter
            {
                Name = "tile_height",
                Label = "Tile Height",
                Type = ParameterType.Slider,
                Min = 64,
                Max = 8192,
                Step = 8,
                DefaultValue = 512
            },
            new FragmentParameter
            {
                Name = "mode_type",
                Label = "Mode Type",
                Type = ParameterType.Select,
                DefaultValue = "Linear",
                Options = ["Linear", "Chess", "None"]
            },
            new FragmentParameter
            {
                Name = "mask_blur",
                Label = "Mask Blur",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 64,
                Step = 1,
                DefaultValue = 8
            },
            new FragmentParameter
            {
                Name = "tile_padding",
                Label = "Tile Padding",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 8192,
                Step = 8,
                DefaultValue = 32
            },
            new FragmentParameter
            {
                Name = "seam_fix_mode",
                Label = "Seam Fix Mode",
                Type = ParameterType.Select,
                DefaultValue = "None",
                Options = ["None", "Band Pass", "Half Tile", "Half Tile + Intersections"]
            },
            new FragmentParameter
            {
                Name = "seam_fix_denoise",
                Label = "Seam Fix Denoise",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 1,
                Step = 0.01,
                DefaultValue = 1.0
            },
            new FragmentParameter
            {
                Name = "seam_fix_width",
                Label = "Seam Fix Width",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 8192,
                Step = 8,
                DefaultValue = 64
            },
            new FragmentParameter
            {
                Name = "seam_fix_mask_blur",
                Label = "Seam Fix Mask Blur",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 64,
                Step = 1,
                DefaultValue = 8
            },
            new FragmentParameter
            {
                Name = "seam_fix_padding",
                Label = "Seam Fix Padding",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 8192,
                Step = 8,
                DefaultValue = 16
            },
            new FragmentParameter
            {
                Name = "force_uniform_tiles",
                Label = "Force Uniform Tiles",
                Type = ParameterType.Checkbox,
                DefaultValue = true
            },
            new FragmentParameter
            {
                Name = "tiled_decode",
                Label = "Tiled Decode",
                Type = ParameterType.Checkbox,
                DefaultValue = false
            },
            new FragmentParameter
            {
                Name = "batch_size",
                Label = "Tile Batch Size",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 16,
                Step = 1,
                DefaultValue = 1
            }
        ]
    };

    /// <summary>
    /// Parameters for the Z-Image Turbo Upscale fragment.
    /// </summary>
    public class Parameters
    {
        public double UpscaleBy { get; set; } = 2.0;
        public double Strength { get; set; } = 0.2;
        public string ControlNetNodeType { get; set; } = "QwenImageDiffsynthControlnet";
        public string SamplerName { get; set; } = "deis_2m";
        public string Scheduler { get; set; } = "beta";
        public int Steps { get; set; } = 6;
        public double Cfg { get; set; } = 1.0;
        public double Denoise { get; set; } = 0.21;
        public long Seed { get; set; } = 0;
        public string ModeType { get; set; } = "Linear";
        public bool AutoTileSize { get; set; } = true;
        public int TileWidth { get; set; } = 512;
        public int TileHeight { get; set; } = 512;
        public int MaskBlur { get; set; } = 8;
        public int TilePadding { get; set; } = 32;
        public string SeamFixMode { get; set; } = "None";
        public double SeamFixDenoise { get; set; } = 1.0;
        public int SeamFixWidth { get; set; } = 64;
        public int SeamFixMaskBlur { get; set; } = 8;
        public int SeamFixPadding { get; set; } = 16;
        public bool ForceUniformTiles { get; set; } = true;
        public bool TiledDecode { get; set; } = false;
        public int BatchSize { get; set; } = 1;
        public int BaseWidth { get; set; } = 872;
        public int BaseHeight { get; set; } = 1248;
        public string Scope { get; set; } = "";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Id);
        var upscaleBy = fragment?.GetDouble("upscale_by", 2.0) ?? 2.0;
        var strength = fragment?.GetDouble("strength", 0.2) ?? 0.2;
        var seed = fragment?.GetLong("upscale_seed", -1) ?? -1;
        if (seed < 0) seed = Random.Shared.NextInt64(0, int.MaxValue);

        BuildInternal(builder, registry, new Parameters
        {
            UpscaleBy = upscaleBy,
            Strength = strength,
            ControlNetNodeType = fragment?.GetString("controlnet_node_type", "QwenImageDiffsynthControlnet") ?? "QwenImageDiffsynthControlnet",
            SamplerName = fragment?.GetString("upscale_sampler_name", "deis_2m") ?? "deis_2m",
            Scheduler = fragment?.GetString("upscale_scheduler", "beta") ?? "beta",
            Steps = fragment?.GetInt("upscale_steps", 6) ?? 6,
            Cfg = fragment?.GetDouble("upscale_cfg", 1.0) ?? 1.0,
            Denoise = fragment?.GetDouble("upscale_denoise", 0.21) ?? 0.21,
            Seed = seed,
            ModeType = fragment?.GetString("mode_type", "Linear") ?? "Linear",
            AutoTileSize = fragment?.GetBool("auto_tile_size", true) ?? true,
            TileWidth = fragment?.GetInt("tile_width", 512) ?? 512,
            TileHeight = fragment?.GetInt("tile_height", 512) ?? 512,
            MaskBlur = fragment?.GetInt("mask_blur", 8) ?? 8,
            TilePadding = fragment?.GetInt("tile_padding", 32) ?? 32,
            SeamFixMode = fragment?.GetString("seam_fix_mode", "None") ?? "None",
            SeamFixDenoise = fragment?.GetDouble("seam_fix_denoise", 1.0) ?? 1.0,
            SeamFixWidth = fragment?.GetInt("seam_fix_width", 64) ?? 64,
            SeamFixMaskBlur = fragment?.GetInt("seam_fix_mask_blur", 8) ?? 8,
            SeamFixPadding = fragment?.GetInt("seam_fix_padding", 16) ?? 16,
            ForceUniformTiles = fragment?.GetBool("force_uniform_tiles", true) ?? true,
            TiledDecode = fragment?.GetBool("tiled_decode", false) ?? false,
            BatchSize = fragment?.GetInt("batch_size", 1) ?? 1,
            Scope = scope
        }, scope, scopeTitle);
    }

    /// <summary>
    /// Builds the fragment with explicit parameters.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, fragmentParams, scope, scopeTitle);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters p,
        string scope,
        string scopeTitle)
    {
        // === ControlNet Phase ===
        var controlnetNodeId = $"{scope}qwen_controlnet";

        // Get references from registry
        var modelRef = registry.GetRef($"{p.Scope}model_output");
        var modelPatchRef = registry.GetRef($"{p.Scope}model_patch_output");
        var vaeRef = registry.GetRef($"{p.Scope}vae_output");
        var tileMapRef = registry.GetRef($"{p.Scope}tile_map_output");

        var controlNetNodeType = p.ControlNetNodeType is "ZImageFunControlnet"
            ? "ZImageFunControlnet"
            : "QwenImageDiffsynthControlnet";

        builder.AddNode(controlnetNodeId, node => node
            .Type(controlNetNodeType)
            .Title($"{scopeTitle}Z-Image ControlNet")
            .Input("strength", p.Strength)
            .InputRef("model", modelRef)
            .InputRef("model_patch", modelPatchRef)
            .InputRef("vae", vaeRef)
            .InputRef("image", tileMapRef));

        // Overwrite model_output with ControlNet-patched model
        registry.Register($"{p.Scope}model_output", controlnetNodeId, 0);

        // === UltimateSDUpscale Phase ===
        var upscaleNodeId = $"{scope}ultimate_sd_upscale";

        var imageRef = registry.GetRef("image_output");
        var patchedModelRef = registry.GetRef($"{p.Scope}model_output");
        var positiveRef = registry.GetRef($"{scope}positive_output");
        var negativeRef = registry.GetRef($"{scope}negative_output");
        var upscaleVaeRef = registry.GetRef($"{scope}vae_output");
        var upscaleModelRef = registry.GetRef($"{scope}upscale_model_output");

        var tileWidth = p.AutoTileSize ? CalculateTileSize(p.BaseWidth, p.UpscaleBy) : AlignToStep(p.TileWidth, 8, 64, 8192);
        var tileHeight = p.AutoTileSize ? CalculateTileSize(p.BaseHeight, p.UpscaleBy) : AlignToStep(p.TileHeight, 8, 64, 8192);

        builder.AddNode(upscaleNodeId, node => node
            .Type("UltimateSDUpscale")
            .Title($"{scopeTitle}Ultimate SD Upscale")
            .Input("upscale_by", p.UpscaleBy)
            .Input("seed", p.Seed)
            .Input("steps", p.Steps)
            .Input("cfg", p.Cfg)
            .Input("sampler_name", p.SamplerName)
            .Input("scheduler", p.Scheduler)
            .Input("denoise", p.Denoise)
            .Input("mode_type", p.ModeType)
            .Input("tile_width", tileWidth)
            .Input("tile_height", tileHeight)
            .Input("mask_blur", p.MaskBlur)
            .Input("tile_padding", p.TilePadding)
            .Input("seam_fix_mode", p.SeamFixMode)
            .Input("seam_fix_denoise", p.SeamFixDenoise)
            .Input("seam_fix_width", p.SeamFixWidth)
            .Input("seam_fix_mask_blur", p.SeamFixMaskBlur)
            .Input("seam_fix_padding", p.SeamFixPadding)
            .Input("force_uniform_tiles", p.ForceUniformTiles)
            .Input("tiled_decode", p.TiledDecode)
            .Input("batch_size", p.BatchSize)
            .InputRef("image", imageRef)
            .InputRef("model", patchedModelRef)
            .InputRef("positive", positiveRef)
            .InputRef("negative", negativeRef)
            .InputRef("vae", upscaleVaeRef)
            .InputRef("upscale_model", upscaleModelRef));

        // Overwrite image_output with upscaled image
        registry.Register("image_output", upscaleNodeId, 0);
    }

    private static int CalculateTileSize(int baseSize, double upscaleBy)
        => AlignToStep((int)(upscaleBy * baseSize / 2 + 32), 8, 64, 8192);

    private static int AlignToStep(int value, int step, int min, int max)
    {
        var clamped = Math.Clamp(value, min, max);
        return Math.Max(min, (clamped / step) * step);
    }
}
