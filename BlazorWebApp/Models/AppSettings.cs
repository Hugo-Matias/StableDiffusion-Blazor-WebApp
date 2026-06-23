using BlazorWebApp.Services.Cleanup;

namespace BlazorWebApp.Models
{
    public class AppSettings
    {
        public bool ResetState { get; set; } = false;  // Set to true when implementing a new script to repopulate variables
        public bool IsDarkMode { get; set; } = true;
        public GenerationSettingsModel Generation { get; set; } = new();
        public ResourcesSettingsModel Resources { get; set; } = new();
        public PromptsSettingsModel Prompts { get; set; } = new();
        public CleanupSettingsModel Cleanup { get; set; } = new();
    }

    public class CleanupSettingsModel
    {
        public CleanupEmbeddingOptions Embeddings { get; set; } = new();
        public CleanupScoringOptions Scoring { get; set; } = new();
        public CleanupIndexingQueueOptions IndexingQueue { get; set; } = new();
        public CleanupGroupExplanationOptions VisionLanguage { get; set; } = new();
    }

    #region Generation
    public class GenerationSettingsModel
    {
        public RandomImagesSettingsModel RandomImages { get; set; } = new();
        public SharedSettingsModel Shared { get; set; } = new();
        public Txt2ImgSettingsModel Txt2Img { get; set; } = new();
        public Img2ImgSettingsModel Img2Img { get; set; } = new();
        public UpscaleSettingsModel Upscale { get; set; } = new();
        public Img2VidSettingsModel Img2Vid { get; set; } = new();
    }

    public class RandomImagesSettingsModel
    {
        public string Source { get; set; } = "generated";
        public int Value { get; set; } = 10;
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 100;
        public int Step { get; set; } = 1;
    }

    #region Shared
    public class SharedSettingsModel
    {
        public string Sampler { get; set; } = "DPM++ 2M Karras";
        public int Seed { get; set; } = -1;
        public bool FaceRestoration { get; set; } = false;
        public bool Tilling { get; set; } = false;
        public StepsSettingsModel Steps { get; set; } = new();
        public ResolutionSettingsModel Resolution { get; set; } = new();
        public BatchSettingsModel Batch { get; set; } = new();
        public CfgScaleSettingsModel CfgScale { get; set; } = new();
        public DistilledCfgSettingsModel DistilledCfg { get; set; } = new();
        public DenoisingSettingsModel Denoising { get; set; } = new();
        public LLMEnhancerSettingsModel LLMEnhancer { get; set; } = new();
        public List<QuickResolution> QuickResolutions { get; set; } = new()
        {
            new() { Width = 512, Height = 768 },
            new() { Width = 832, Height = 1248 },
            new() { Width = 1024, Height = 1024 },
            new() { Width = 1920, Height = 1088 },
        };
    }

    public class StepsSettingsModel
    {
        public int Value { get; set; } = 30;
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 150;
        public int Step { get; set; } = 1;
    }

    public class ResolutionSettingsModel
    {
        public int Width { get; set; } = 512;
        public int Height { get; set; } = 768;
        public int Min { get; set; } = 64;
        public int Max { get; set; } = 2048;
        public int Step { get; set; } = 32;
    }

    public class BatchSettingsModel
    {
        public BatchSizeSettingsModel Size { get; set; } = new();
        public BatchCountSettingsModel Count { get; set; } = new();
    }

    public class BatchCountSettingsModel
    {
        public int Value { get; set; } = 1;
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 8;
        public int Step { get; set; } = 1;
    }

    public class BatchSizeSettingsModel
    {
        public int Value { get; set; } = 4;
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 8;
        public int Step { get; set; } = 1;
    }

    public class CfgScaleSettingsModel
    {
        public float Value { get; set; } = 7.5f;
        public float Min { get; set; } = 1.0f;
        public float Max { get; set; } = 30.0f;
        public float Step { get; set; } = 0.5f;
    }

    public class DistilledCfgSettingsModel
    {
        public float Value { get; set; } = 2.5f;
        public float Min { get; set; } = 1.0f;
        public float Max { get; set; } = 7.0f;
        public float Step { get; set; } = 0.1f;
    }

    public class DenoisingSettingsModel
    {
        public double Value { get; set; } = 0.52;
        public double Min { get; set; } = 0;
        public double Max { get; set; } = 1;
        public double Step { get; set; } = 0.01;
    }

    public class LLMEnhancerSettingsModel
    {
        public string Model { get; set; } = "Llama-3.2-3B-Instruct-abliterated.Q5_K_M.gguf";
        public string Instructions { get; set; } = "Expand this simple prompt into a detailed, descriptive image generation prompt: \"{prompt}\". Add artistic details, lighting, mood, and composition elements. Keep it concise with as few paragraphs as possible.";
        public string NegativeInstructions { get; set; } = "Expand this negative prompt with detailed descriptions of what to avoid: \"{prompt}\". Add specific undesired elements, artifacts, and quality issues. Keep it concise with as few paragraphs as possible.";
        public IntRange Seed { get; set; } = new() { Min = -1, Max = int.MaxValue, Value = -1, Step = 1 };
        public FloatRange Temperature { get; set; } = new() { Min = 0f, Max = 2f, Value = 1.0f, Step = 0.1f };
        public IntRange TopK { get; set; } = new() { Min = 1, Max = 100, Value = 50, Step = 1 };
        public FloatRange TopP { get; set; } = new() { Min = 0f, Max = 1f, Value = 0.9f, Step = 0.05f };
        public FloatRange MinP { get; set; } = new() { Min = 0f, Max = 1f, Value = 0.05f, Step = 0.01f };
        public IntRange NumCtx { get; set; } = new() { Min = 128, Max = 32768, Value = 2048, Step = 128 };
        public IntRange NumPredict { get; set; } = new() { Min = 50, Max = 8192, Value = 500, Step = 50 };
    }

    #endregion

    #region Txt2Img
    public class Txt2ImgSettingsModel
    {
        public HighresSettingsModel HighRes { get; set; } = new();
        public SeedVR2Settings SeedVR2 { get; set; } = new();
        public ConditioningVariationSettings ConditioningVariation { get; set; } = new();
        public SeedVarianceEnhancerSettings SeedVarianceEnhancer { get; set; } = new();
    }
    public class HighresSettingsModel
    {
        public bool Enabled { get; set; } = false;
        public string Upscaler { get; set; } = "Latent";
        public FirstPassSettingsModel FirstPass { get; set; } = new();
        public ResizeScaleSettingsModel Scale { get; set; } = new();
        public ResizeResolutionSettingsModel Resolution { get; set; } = new();
        public SecondPassStepsSettingsModel SecondPassSteps { get; set; } = new();
    }

    public class FirstPassSettingsModel
    {
        public int Width { get; set; } = 512;
        public int Height { get; set; } = 512;
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 2048;
        public int Step { get; set; } = 32;
    }

    public class ResizeScaleSettingsModel
    {
        public double Value { get; set; } = 2;
        public double Min { get; set; } = 1;
        public double Max { get; set; } = 4;
        public double Step { get; set; } = 0.05;
    }

    public class ResizeResolutionSettingsModel
    {
        public int Width { get; set; } = 0;
        public int Height { get; set; } = 0;
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 2048;
        public int Step { get; set; } = 8;
    }

    public class SecondPassStepsSettingsModel
    {
        public int Value { get; set; } = 0;
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 150;
        public int Step { get; set; } = 1;
    }

    public class SeedVR2Settings
    {
        public bool Enabled { get; set; } = false;
        public string Model { get; set; } = "seedvr2_ema_7b-Q4_K_M.gguf";
        public string VaeModel { get; set; } = "ema_vae_fp16.safetensors";
        public IntRange BlocksToSwap { get; set; } = new() { Min = 0, Max = 36, Value = 36, Step = 1 };
        public IntRange VaeTileSize { get; set; } = new() { Min = 128, Max = 2048, Value = 1024, Step = 64 };
        public IntRange VaeTileOverlap { get; set; } = new() { Min = 0, Max = 512, Value = 128, Step = 32 };
        public IntRange Resolution { get; set; } = new() { Min = 512, Max = 4096, Value = 2048, Step = 64 };
        public DoubleRange Scale { get; set; } = new() { Min = 1, Max = 10, Value = 2.0, Step = 0.5 };
        public IntRange BatchSize { get; set; } = new() { Min = 1, Max = 10, Value = 1, Step = 1 };
        public DoubleRange InputNoiseScale { get; set; } = new() { Min = 0, Max = 1, Value = 0.0, Step = 0.01 };
        public DoubleRange LatentNoiseScale { get; set; } = new() { Min = 0, Max = 1, Value = 0.0, Step = 0.01 };
    }

    public class ConditioningVariationSettings
    {
        public bool Enabled { get; set; } = false;
        public DoubleRange SwitchPoint { get; set; } = new() { Min = 0, Max = 1, Value = 0.2, Step = 0.05 };
    }

    public class SeedVarianceEnhancerSettings
    {
        public bool Enabled { get; set; } = false;
        public IntRange RandomizePercent { get; set; } = new() { Min = 0, Max = 100, Value = 50, Step = 5 };
        public IntRange Strength { get; set; } = new() { Min = 0, Max = 100, Value = 20, Step = 1 };
        public List<string> NoiseInsertOptions { get; set; } = new() { "noise on beginning steps", "noise on ending steps", "noise on all steps", "disabled" };
        public string DefaultNoiseInsert { get; set; } = "noise on beginning steps";
        public IntRange StepsSwitchoverPercent { get; set; } = new() { Min = 0, Max = 100, Value = 20, Step = 5 };
        public List<string> MaskStartsAtOptions { get; set; } = new() { "beginning", "end" };
        public string DefaultMaskStartsAt { get; set; } = "beginning";
        public IntRange MaskPercent { get; set; } = new() { Min = 0, Max = 100, Value = 0, Step = 5 };
    }
    #endregion

    #region Img2Img
    public class Img2ImgSettingsModel
    {
        public BrushSettingsModel Brush { get; set; } = new();
        public string Mode { get; set; } = "Mask";
        public MaskBlurSettingsModel MaskBlur { get; set; } = new();
        public int ResizeMode { get; set; } = 1;
        public InpaintingSettingsModel Inpainting { get; set; } = new();
        public bool DownsizeInput { get; set; } = true;
        public Img2ImgInputResolution InputResolution { get; set; } = new();
    }

    public class BrushSettingsModel
    {
        public int Value { get; set; } = 50;
        public int Min { get; set; } = 5;
        public int Max { get; set; } = 70;
        public string Color { get; set; } = "#1fbe00";
        public string PointerOutline { get; set; } = "white";
    }

    public class MaskBlurSettingsModel
    {
        public int Value { get; set; } = 2;
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 64;
        public int Step { get; set; } = 1;
    }

    public class InpaintingSettingsModel
    {
        public int Fill { get; set; } = 1;
        public int MaskInvert { get; set; } = 0;
        public InpaintingFullResSettingsModel FullRes { get; set; } = new();
    }

    public class InpaintingFullResSettingsModel
    {
        public bool Value { get; set; } = true;
        public InpaintingFullResPaddingSettingsModel Padding { get; set; } = new();
    }
    public class InpaintingFullResPaddingSettingsModel
    {
        public int Value { get; set; } = 12;
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 40;
        public int Step { get; set; } = 1;
    }

    public class Img2ImgInputResolution
    {
        public int Width { get; set; } = 2048;
        public int Height { get; set; } = 2048;
        public int Min { get; set; } = 64;
        public int Max { get; set; } = 8192;
        public int Step { get; set; } = 32;
    }
    #endregion

    #region Img2Vid
    public class Img2VidSettingsModel
    {
        public Img2VidModelSettingsModel Models { get; set; } = new();
        public Img2VidVideoSettingsModel Video { get; set; } = new();
        public Img2VidSamplingSettingsModel Sampling { get; set; } = new();
        public Img2VidFrameInterpolationSettingsModel FrameInterpolation { get; set; } = new();
    }

    public class Img2VidModelSettingsModel
    {
        public string HighModel { get; set; } = "wan22RemixT2VI2V_i2vHighV20.safetensors";
        public string LowModel { get; set; } = "wan22RemixT2VI2V_i2vLowV20.safetensors";
        public string Clip { get; set; } = "umt5_xxl_fp8_e4m3fn_scaled.safetensors";
        public string ClipVision { get; set; } = "clip_vision_h.safetensors";
        public string Vae { get; set; } = "wan_2.1_vae.safetensors";
    }

    public class Img2VidVideoSettingsModel
    {
        public IntRange Length { get; set; } = new() { Value = 81, Min = 17, Max = 257, Step = 8 };
        public IntRange FrameRate { get; set; } = new() { Value = 16, Min = 8, Max = 60, Step = 1 };
        public FloatRange MotionAmplitude { get; set; } = new() { Value = 1.1f, Min = 0.1f, Max = 3.0f, Step = 0.1f };
    }

    public class Img2VidSamplingSettingsModel
    {
        public IntRange Shift { get; set; } = new() { Value = 5, Min = 1, Max = 20, Step = 1 };
        public IntRange Steps { get; set; } = new() { Value = 8, Min = 1, Max = 50, Step = 1 };
        public FloatRange CfgScale { get; set; } = new() { Value = 1.0f, Min = 1.0f, Max = 15.0f, Step = 0.5f };
        public string Sampler { get; set; } = "euler";
        public string Scheduler { get; set; } = "simple";
    }

    public class Img2VidFrameInterpolationSettingsModel
    {
        public bool Enabled { get; set; } = true;
        public DoubleRange ScaleBy { get; set; } = new() { Value = 2.0, Min = 1.0, Max = 4.0, Step = 0.5 };
        public IntRange Multiplier { get; set; } = new() { Value = 2, Min = 1, Max = 8, Step = 1 };
        public string RifeModel { get; set; } = "rife49.pth";
        public List<string> RifeModels { get; set; } = new()
        {
            "rife49.pth",
            "rife48.pth",
            "rife47.pth",
            "rife46.pth"
        };
    }
    #endregion
    #endregion

    #region Upscale
    public class UpscaleSettingsModel
    {
        public int ResizeMode { get; set; } = 0;
        public bool ShowResults { get; set; } = true;
        public string UpscalerPrimary { get; set; } = "Remacri (foolhardy)";
        public UpscalerSecondarySettingsModel UpscalerSecondary { get; set; } = new();
        public UpscalingMultiplierSettingsModel UpscalingMultiplier { get; set; } = new();
        public UpscalingResolutionSettingsModel UpscalingResolution { get; set; } = new();
        public FaceRestorationSettingsModel FaceRestoration { get; set; } = new();

    }

    public class UpscalerSecondarySettingsModel
    {
        public string Name { get; set; } = "None";
        public double DefaultValue { get; set; } = 0.5;
        public double Min { get; set; } = 0;
        public double Max { get; set; } = 1;
        public double Step { get; set; } = 0.01;
    }

    public class UpscalingMultiplierSettingsModel
    {
        public double DefaultValue { get; set; } = 2;
        public double Min { get; set; } = 1;
        public double Max { get; set; } = 8;
        public double Step { get; set; } = 0.05;
    }

    public class UpscalingResolutionSettingsModel
    {
        public int Width { get; set; } = 1024;
        public int Height { get; set; } = 1024;
        public bool CropToFit { get; set; } = false;
    }

    public class FaceRestorationSettingsModel
    {
        public double GfpganVisibility { get; set; } = 0;
        public double CodeformerVisibility { get; set; } = 0;
        public double CodeformerWeight { get; set; } = 0.85;
        public bool UpscaleBeforeRestoration { get; set; } = true;
        public double Min { get; set; } = 0;
        public double Max { get; set; } = 1;
        public double Step { get; set; } = 0.01;
    }
    #endregion

    #region Resources

    public class ResourcesSettingsModel
    {
        public bool LoadTriggerWords { get; set; } = true;
        public ResourceSearchSettingsModel Search { get; set; } = new();
        public ResourceWeightSettingsModel Weight { get; set; } = new();
        public CivitaiSettingsModel Civitai { get; set; } = new();
        public DanbooruSettingsModel Danbooru { get; set; } = new();
        public bool OrderByDescending { get; set; } = false;
        public List<string> OrderByOptions { get; set; } = new()
        {
            "Title",
            "Author",
            "Creation Date",
            "Load Date",
            "Random"
        };
    }

    public class ResourceWeightSettingsModel
    {
        public float Value { get; set; } = 1f;
        public float Min { get; set; } = 0f;
        public float Max { get; set; } = 2f;
        public float Step { get; set; } = 0.05f;
    }

    public class ResourceSearchSettingsModel
    {
        public ResourceSearchLimitSettingsModel Limit { get; set; } = new();
    }

    public class ResourceSearchLimitSettingsModel
    {
        public int Value { get; set; } = 16;
        public int Min { get; set; } = 8;
        public int Max { get; set; } = 256;
        public int Step { get; set; } = 8;
    }

    public class CivitaiSettingsModel
    {
        public float ExistsButtonOpacity { get; set; } = 0.3f;
        public string ImageNoneFilter { get; set; } = "sepia(70%) saturate(200%) brightness(70%) hue-rotate(300deg)";
        public string ImageMissingFilter { get; set; } = "sepia(70%) saturate(200%) brightness(70%) hue-rotate(125deg)";
        public string ImageExtraFilter { get; set; } = "sepia(70%) saturate(200%) brightness(70%) hue-rotate(25deg)";
        public string ImageOkFilter { get; set; } = "grayscale(70%) brightness(70%)";
        public List<string> DisabledFamilies { get; set; } = new();
        public List<string> DisabledModels { get; set; } = new();
        public bool DownloadResourceImages { get; set; } = true;
        public CivitaiLimitSettingsModel Limit { get; set; } = new();
    }

    public class CivitaiLimitSettingsModel
    {
        public int Value { get; set; } = 15;
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 200;
        public int Step { get; set; } = 8;
    }

    public class DanbooruSettingsModel
    {
        public string BlacklistTags { get; set; } = "censored, bar censor, blur censor, mosaic censoring, character censor, novelty censor, convenient censoring, signature, artist name, twitter username, virtual youtuber";
        public List<string> SavedSearches { get; set; } = new() { "order:rank", "order:random score:>700 filetype:png,jpg" };
    }
    #endregion

    #region Prompts
    public class PromptsSettingsModel
    {
        public PromptsWildcardsSettingsModel Wildcards { get; set; } = new();
    }

    public class PromptsWildcardsSettingsModel
    {
        public int PromptTextfieldLines { get; set; } = 5;
        public PromptsWildcardsGenerationSettingsModel Generation { get; set; } = new();
    }

    public class PromptsWildcardsGenerationSettingsModel
    {
        public int Value { get; set; } = 10;
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 50;
        public int Step { get; set; } = 1;
    }
    #endregion

    #region Types
    public class IntRange
    {
        public int Min { get; set; }
        public int Max { get; set; }
        public int Value { get; set; }
        public int Step { get; set; }
    }

    public class FloatRange
    {
        public float Min { get; set; }
        public float Max { get; set; }
        public float Value { get; set; }
        public float Step { get; set; }
    }

    public class DoubleRange
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public double Value { get; set; }
        public double Step { get; set; }
    }

    public class QuickResolution
    {
        public string Label => $"{Width}x{Height}";
        public int Width { get; set; }
        public int Height { get; set; }
    }
    #endregion
}
