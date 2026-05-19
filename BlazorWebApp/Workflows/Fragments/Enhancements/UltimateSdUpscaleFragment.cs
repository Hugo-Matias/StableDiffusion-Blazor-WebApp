using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Enhancements;

/// <summary>
/// Generic UltimateSDUpscale enhancement that upscales the current image with an
/// upscale model, then performs tiled diffusion refinement against the active model.
/// </summary>
public class UltimateSdUpscaleFragment : IFragmentBuilder
{
    public string Id { get; init; } = "ultimate_sd_upscale";
    public string Title { get; init; } = "Ultimate SD Upscale";
    public int Order { get; init; } = 110;
    public bool Collapsible { get; init; } = true;
    public bool DefaultCollapsed { get; init; } = true;

    public FragmentMetadata Metadata => new()
    {
        Id = Id,
        Type = FragmentType.Enhancement,
        Title = Title,
        Component = "UltimateSdUpscaleForm",
        Icon = "fa-solid fa-magnifying-glass-plus",
        Order = Order,
        Collapsible = Collapsible,
        DefaultCollapsed = DefaultCollapsed,
        Description = "Upscales the current image with the selected upscale model, then runs tiled diffusion refinement with the active prompts and model.",
        Parameters =
        [
            new FragmentParameter
            {
                Name = "upscale_model",
                Label = "Upscale Model",
                Type = ParameterType.Select,
                Source = new DynamicSource("UpscaleModelLoader", "model_name"),
                DefaultValue = "4x-UltraSharpV2.safetensors"
            },
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
                Name = "upscale_sampler_name",
                Label = "Upscale Sampler",
                Type = ParameterType.Select,
                DefaultValue = "dpmpp_2m",
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
                Type = ParameterType.Number,
                Min = 1,
                Max = 10000,
                Step = 1,
                DefaultValue = 20
            },
            new FragmentParameter
            {
                Name = "upscale_cfg",
                Label = "Upscale CFG",
                Type = ParameterType.Number,
                Min = 0,
                Max = 100,
                DefaultValue = 8.0
            },
            new FragmentParameter
            {
                Name = "upscale_denoise",
                Label = "Upscale Denoise",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 1,
                Step = 0.01,
                DefaultValue = 0.2
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
                Type = ParameterType.Number,
                Min = 64,
                Max = 8192,
                Step = 8,
                DefaultValue = 512
            },
            new FragmentParameter
            {
                Name = "tile_height",
                Label = "Tile Height",
                Type = ParameterType.Number,
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
                Type = ParameterType.Number,
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
                Type = ParameterType.Number,
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
                Type = ParameterType.Number,
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
                Type = ParameterType.Number,
                Min = 1,
                Max = 4096,
                Step = 1,
                DefaultValue = 1
            }
        ]
    };

    public class Parameters
    {
        public string UpscaleModel { get; set; } = "4x-UltraSharpV2.safetensors";
        public double UpscaleBy { get; set; } = 2.0;
        public string SamplerName { get; set; } = "dpmpp_2m";
        public string Scheduler { get; set; } = "beta";
        public int Steps { get; set; } = 20;
        public double Cfg { get; set; } = 8.0;
        public double Denoise { get; set; } = 0.2;
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
        public int BaseWidth { get; set; } = 1024;
        public int BaseHeight { get; set; } = 1024;
        public string Scope { get; set; } = "";
        public string ScopeTitle { get; set; } = "";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Id);
        if (fragment?.IsActive != true)
        {
            return;
        }

        var latentFragment = parameters.GetFragment("latent");
        var seed = fragment.GetLong("upscale_seed", -1);
        if (seed < 0)
        {
            seed = Random.Shared.NextInt64(0, int.MaxValue);
        }

        Build(builder, registry, new Parameters
        {
            UpscaleModel = fragment.GetString("upscale_model", "4x-UltraSharpV2.safetensors") ?? "4x-UltraSharpV2.safetensors",
            UpscaleBy = fragment.GetDouble("upscale_by", 2.0),
            SamplerName = fragment.GetString("upscale_sampler_name", "dpmpp_2m") ?? "dpmpp_2m",
            Scheduler = fragment.GetString("upscale_scheduler", "beta") ?? "beta",
            Steps = fragment.GetInt("upscale_steps", 20),
            Cfg = fragment.GetDouble("upscale_cfg", 8.0),
            Denoise = fragment.GetDouble("upscale_denoise", 0.2),
            Seed = seed,
            ModeType = fragment.GetString("mode_type", "Linear") ?? "Linear",
            AutoTileSize = fragment.GetBool("auto_tile_size", true),
            TileWidth = fragment.GetInt("tile_width", 512),
            TileHeight = fragment.GetInt("tile_height", 512),
            MaskBlur = fragment.GetInt("mask_blur", 8),
            TilePadding = fragment.GetInt("tile_padding", 32),
            SeamFixMode = fragment.GetString("seam_fix_mode", "None") ?? "None",
            SeamFixDenoise = fragment.GetDouble("seam_fix_denoise", 1.0),
            SeamFixWidth = fragment.GetInt("seam_fix_width", 64),
            SeamFixMaskBlur = fragment.GetInt("seam_fix_mask_blur", 8),
            SeamFixPadding = fragment.GetInt("seam_fix_padding", 16),
            ForceUniformTiles = fragment.GetBool("force_uniform_tiles", true),
            TiledDecode = fragment.GetBool("tiled_decode", false),
            BatchSize = fragment.GetInt("batch_size", 1),
            BaseWidth = latentFragment?.GetInt("width", 1024) ?? 1024,
            BaseHeight = latentFragment?.GetInt("height", 1024) ?? 1024,
            Scope = scope,
            ScopeTitle = scopeTitle
        });
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters parameters)
    {
        var scope = parameters.Scope;
        var scopeTitle = parameters.ScopeTitle;
        var nodePrefix = string.IsNullOrWhiteSpace(scope) ? "ultimate_sd_upscale" : $"{scope}ultimate_sd_upscale";
        var modelLoaderNodeId = $"{nodePrefix}_model_loader";
        var upscaleNodeId = nodePrefix;

        builder.AddNode(modelLoaderNodeId, node => node
            .Type("UpscaleModelLoader")
            .Title($"{scopeTitle}Load Ultimate SD Upscale Model")
            .Input("model_name", parameters.UpscaleModel));

        var tileWidth = parameters.AutoTileSize ? CalculateTileSize(parameters.BaseWidth, parameters.UpscaleBy) : AlignToStep(parameters.TileWidth, 8, 64, 8192);
        var tileHeight = parameters.AutoTileSize ? CalculateTileSize(parameters.BaseHeight, parameters.UpscaleBy) : AlignToStep(parameters.TileHeight, 8, 64, 8192);

        builder.AddNode(upscaleNodeId, node => node
            .Type("UltimateSDUpscale")
            .Title($"{scopeTitle}Ultimate SD Upscale")
            .Input("upscale_by", parameters.UpscaleBy)
            .Input("seed", parameters.Seed)
            .Input("steps", parameters.Steps)
            .Input("cfg", parameters.Cfg)
            .Input("sampler_name", parameters.SamplerName)
            .Input("scheduler", parameters.Scheduler)
            .Input("denoise", parameters.Denoise)
            .Input("mode_type", parameters.ModeType)
            .Input("tile_width", tileWidth)
            .Input("tile_height", tileHeight)
            .Input("mask_blur", parameters.MaskBlur)
            .Input("tile_padding", parameters.TilePadding)
            .Input("seam_fix_mode", parameters.SeamFixMode)
            .Input("seam_fix_denoise", parameters.SeamFixDenoise)
            .Input("seam_fix_width", parameters.SeamFixWidth)
            .Input("seam_fix_mask_blur", parameters.SeamFixMaskBlur)
            .Input("seam_fix_padding", parameters.SeamFixPadding)
            .Input("force_uniform_tiles", parameters.ForceUniformTiles)
            .Input("tiled_decode", parameters.TiledDecode)
            .Input("batch_size", parameters.BatchSize)
            .InputRef("image", registry.GetRef("image_output"))
            .InputRef("model", registry.GetRef($"{scope}model_output"))
            .InputRef("positive", registry.GetRef($"{scope}positive_output"))
            .InputRef("negative", registry.GetRef($"{scope}negative_output"))
            .InputRef("vae", registry.GetRef($"{scope}vae_output"))
            .InputFromNode("upscale_model", modelLoaderNodeId, 0));

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
