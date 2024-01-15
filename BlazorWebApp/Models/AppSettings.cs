namespace BlazorWebApp.Models
{
    public class AppSettings
    {
        public bool ResetState { get; set; } = false;  // Set to true when implementing a new script to repopulate variables
        public bool IsDarkMode { get; set; } = true;
        public GenerationSettingsModel Generation { get; set; } = new();
        public ResourcesSettingsModel Resources { get; set; } = new();
        public WebuiSettingsModel Webui { get; set; } = new();
        public ScriptsSettingsModel Scripts { get; set; } = new();
        public PromptsSettingsModel Prompts { get; set; } = new();
    }

    #region Generation
    public class GenerationSettingsModel
    {
        public RandomImagesSettingsModel RandomImages { get; set; } = new();
        public SharedSettingsModel Shared { get; set; } = new();
        public Txt2ImgSettingsModel Txt2Img { get; set; } = new();
        public Img2ImgSettingsModel Img2Img { get; set; } = new();
        public UpscaleSettingsModel Upscale { get; set; } = new();
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
        public DenoisingSettingsModel Denoising { get; set; } = new();
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

    public class DenoisingSettingsModel
    {
        public double Value { get; set; } = 0.52;
        public double Min { get; set; } = 0;
        public double Max { get; set; } = 1;
        public double Step { get; set; } = 0.01;
    }
    #endregion

    #region Txt2Img
    public class Txt2ImgSettingsModel
    {
        public HighresSettingsModel HighRes { get; set; } = new();
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
    #endregion

    #region Scripts
    public class ScriptsSettingsModel
    {
        public ControlNetSettingsModel ControlNet { get; set; } = new();
        public CutoffSettingsModel Cutoff { get; set; } = new();
        public DynamicPromptsSettingsModel DynamicPrompts { get; set; } = new();
        public UltimateUpscaleSettingsModel UltimateUpscale { get; set; } = new();
        public MultiDiffusionSettingsModel MultiDiffusion { get; set; } = new();
        public RegionalPrompterSettingsModel RegionalPrompter { get; set; } = new();
        public XYZPlotSettingsModel XYZPlot { get; set; } = new();
        public ADetailerSettingsModel ADetailer { get; set; } = new();
    }

    #region ControlNet
    public class ControlNetSettingsModel
    {
        public bool IsEnabled { get; set; } = false;
        public ControlNetPreprocessor Preprocessor { get; set; } = ControlNetPreprocessor.none;
        public string Model { get; set; } = "None";
        public bool IsLowVRam { get; set; } = false;
        public List<string> ControlModes { get; set; } = new() { "Balanced", "My prompt is more important", "ControlNet is more important" };
        public List<string> ResizeModes { get; set; } = new() { "Just Resize", "Inner Fit (Scale to Fit)", "Outer Fit (Shrink to Fit)" };
        public ControlNetWeightSettingsModel Weight { get; set; } = new();
        public ControlNetGuidanceSettingsModel Guidance { get; set; } = new();
        public Dictionary<ControlNetPreprocessor, ControlNetProcessorSettingsModel?> PreprocessorSettings { get; set; } = new()
        {
            { ControlNetPreprocessor.none, null },
            { ControlNetPreprocessor.canny, new() { Resolution = new() { Label = "Annotator Resolution" }, Threshold = new() { A = new() { Label= "Low Threshold", Value = 100f, Min = 1f, Max = 255f, Step = 1 }, B = new() { Label = "High Threshold", Value = 200f, Min = 1f, Max = 255f, Step = 1f } } } },
            { ControlNetPreprocessor.depth_leres, new() { Resolution = new() { Label = "LeReS Resolution" }, Threshold = new() { A = new() { Label= "Remove Near %", Value = 0f, Min = 0f, Max = 100f, Step = 0.1f }, B = new() { Label = "Remove Background %", Value = 0f, Min = 0f, Max = 100f, Step = 0.1f } } } },
            { ControlNetPreprocessor.depth_midas, new() { Resolution = new() { Label = "Midas Resolution" } } },
            { ControlNetPreprocessor.depth_zoe, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.inpaint_global_harmonious, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.inpaint_only, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.lineart_anime, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.lineart_coarse, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.lineart_realistic, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.mediapipe_face, new() { Resolution = new() { Label = "Annotator Resolution" }, Threshold = new() { A = new() { Label= "Max Faces", Value = 1, Min = 1, Max = 10, Step = 1 }, B = new() { Label = "Min Face Confidence", Value = 0.5f, Min = 0.01f, Max = 1, Step = 0.01f } } } },
            { ControlNetPreprocessor.mlsd, new() { Resolution = new() { Label = "Hough Resolution" }, Threshold = new() { A = new() { Label= "Hough Value", Value = 0.1f, Min = 0.01f, Max = 2f, Step = 0.01f }, B = new() { Label = "Hough Distance", Value = 0.1f, Min = 0.01f, Max = 20f, Step = 0.01f } } } },
            { ControlNetPreprocessor.normal_bae, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.normal_midas, new() { Resolution = new() { Label = "Normal Resolution" }, Threshold = new() { A = new() { Label = "Background Threshold", Min = 0, Max = 1, Value = 0.4f, Step = 0.01f } } } },
            { ControlNetPreprocessor.openpose, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.openpose_face, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.openpose_faceonly, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.openpose_full, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.openpose_hand, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.reference_only, new() { Threshold = new() { A = new() { Label = "Style Fidelity (\"Balanced\" mode only)", Value = 0.5f, Min = 0.0f, Max = 1.0f, Step = 0.01f } } } },
            { ControlNetPreprocessor.reference_adain, new() { Threshold = new() { A = new() { Label = "Style Fidelity (\"Balanced\" mode only)", Value = 0.5f, Min = 0.0f, Max = 1.0f, Step = 0.01f } } } },
            { ControlNetPreprocessor.scribble_hed, new() { Resolution = new() { Label = "HED Resolution" } } },
            { ControlNetPreprocessor.scribble_pidinet, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.scribble_xdog, new() { Resolution = new() { Label = "Annotator Resolution" }, Threshold = new() { A = new() { Label = "XDoG Threshold", Value = 32, Min = 1, Max = 64, Step = 1 } } } },
            { ControlNetPreprocessor.seg_ofade20k, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.seg_ofcoco, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.seg_ufade20k, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.shuffle, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.softedge_hed, new() { Resolution = new() { Label = "HED Resolution" } } },
            { ControlNetPreprocessor.softedge_hedsafe, new() { Resolution = new() { Label = "HED Resolution" } } },
            { ControlNetPreprocessor.softedge_pidinet, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.softedge_pidisafe, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.t2ia_color_grid, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.t2ia_sketch_pidi, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.t2ia_style_clipvision, new() { Resolution = new() { Label = "Annotator Resolution" } } },
            { ControlNetPreprocessor.threshold, new() { Resolution = new() { Label = "Annotator Resolution" }, Threshold = new() { A = new() { Label = "Binarization Threshold", Value = 127, Min = 0, Max = 255, Step = 1 } } } },
            { ControlNetPreprocessor.tile_resample, new() { Threshold = new() { A = new() { Label = "Downsampling Rate", Value = 1.0f, Min = 1.0f, Max = 8.0f, Step = 0.01f } } } },
            { ControlNetPreprocessor.tile_colorfix, new() { Threshold = new() { A = new() { Label = "Variation", Value = 8.0f, Min = 3.0f, Max = 32.0f, Step = 1.0f } } } },
        };
    }

    public class ControlNetWeightSettingsModel
    {
        public float Value { get; set; } = 1f;
        public float Min { get; set; } = 0f;
        public float Max { get; set; } = 2f;
        public float Step { get; set; } = 0.05f;
    }

    public class ControlNetGuidanceSettingsModel
    {
        public float Strenght { get; set; } = 1f;
        public float Start { get; set; } = 0f;
        public float End { get; set; } = 1f;
        public float Min { get; set; } = 0f;
        public float Max { get; set; } = 1f;
        public float Step { get; set; } = 0.01f;
    }

    public class ControlNetProcessorSettingsModel
    {
        public ControlNetProcessorResolutionSettingsModel Resolution { get; set; } = new();
        public ControlNetProcessorThresholdSettingsModel? Threshold { get; set; }
    }

    public class ControlNetProcessorResolutionSettingsModel
    {
        public string Label { get; set; } = string.Empty;
        public int Value { get; set; } = 512;
        public int Min { get; set; } = 64;
        public int Max { get; set; } = 2048;
        public int Step { get; set; } = 8;
    }

    public class ControlNetProcessorThresholdSettingsModel
    {
        public ControlNetThresholdSettingsModel A { get; set; } = new();
        public ControlNetThresholdSettingsModel? B { get; set; }
    }

    public class ControlNetThresholdSettingsModel
    {
        public string Label { get; set; }
        public float Value { get; set; }
        public float Min { get; set; }
        public float Max { get; set; }
        public float Step { get; set; }
    }
    #endregion

    #region Cutoff
    public class CutoffSettingsModel
    {
        public bool IsEnabled { get; set; } = false;
        public string Targets { get; set; } = string.Empty;
        public CutoffWeightSettingsModel Weight { get; set; } = new();
        public bool DisableNegative { get; set; } = false;
        public bool Strong { get; set; } = false;
        public string Padding { get; set; } = string.Empty;
        public string Interpolation { get; set; } = "Lerp";
        public bool Debug { get; set; } = false;
    }

    public class CutoffWeightSettingsModel
    {
        public float Value { get; set; } = 1f;
        public float Min { get; set; } = -1f;
        public float Max { get; set; } = 2f;
        public float Step { get; set; } = 0.01f;
    }
    #endregion

    #region Dynamic Prompts
    public class DynamicPromptsSettingsModel
    {
        public bool IsEnabled { get; set; } = false;
        public DPCombinatorialSettingsModel Combinatorial { get; set; } = new();
        public DPPromptMagicSettingsModel PromptMagic { get; set; } = new();
        public bool UseFixedSeed { get; set; } = false;
        public bool UnlinkSeedFromPrompt { get; set; } = false;
        public bool DisableNegativePrompt { get; set; } = false;
        public bool EnableJinjaTemplates { get; set; } = false;
        public bool NoImageGeneration { get; set; } = false;
    }

    public class DPCombinatorialSettingsModel
    {
        public bool IsEnabled { get; set; } = false;
        public DPCombinatorialBatchSettingsModel Batches { get; set; } = new();
        public DPCombinatorialMaxGenSettingsModel MaxGenerations { get; set; } = new();
    }

    public class DPCombinatorialBatchSettingsModel
    {
        public int Value { get; set; } = 1;
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 100;
        public int Step { get; set; } = 1;
    }

    public class DPCombinatorialMaxGenSettingsModel
    {
        public int Value { get; set; } = 0;
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 1000;
        public int Step { get; set; } = 1;
    }

    public class DPPromptMagicSettingsModel
    {
        public bool IsEnabled { get; set; } = false;
        public bool IsFeelingLucky { get; set; } = false;
        public DPPromptMagicAttentionGrabberSettingsModel AttentionGrabber { get; set; } = new();
        public DPPromptMagicLengthSettingsModel Length { get; set; } = new();
        public DPPromptMagicCreativitySettingsModel Creativity { get; set; } = new();
        public string? MagicBlocklistRegex { get; set; } = null;
        public List<string> MagicModelList { get; set; } = new()
        {
            "Gustavosta/MagicPrompt-Stable-Diffusion",
            "daspartho/prompt-extend",
            "succinctly/text2image-prompt-generator",
            "microsoft/Promptist",
            "AUTOMATIC/promptgen-lexart",
            "AUTOMATIC/promptgen-majinai-safe",
            "AUTOMATIC/promptgen-majinai-unsafe",
            "kmewhort/stable-diffusion-prompt-bolster",
            "Gustavosta/MagicPrompt-Dalle",
            "Ar4ikov/gpt2-650k-stable-diffusion-prompt-generator",
            "Ar4ikov/gpt2-medium-650k-stable-diffusion-prompt-generator",
            "crumb/bloom-560m-RLHF-SD2-prompter-aesthetic",
            "Meli/GPT2-Prompt",
            "DrishtiSharma/StableDiffusion-Prompt-Generator-GPT-Neo-125M",
        };
    }

    public class DPPromptMagicAttentionGrabberSettingsModel
    {
        public bool IsEnabled { get; set; } = false;
        public float ValueMin { get; set; } = 1.1f;
        public float ValueMax { get; set; } = 1.5f;
        public float Min { get; set; } = -1f;
        public float Max { get; set; } = 2f;
        public float Step { get; set; } = 0.1f;
    }

    public class DPPromptMagicLengthSettingsModel
    {
        public int Value { get; set; } = 100;
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 300;
        public int Step { get; set; } = 10;
    }

    public class DPPromptMagicCreativitySettingsModel
    {
        public float Value { get; set; } = 0.7f;
        public float Min { get; set; } = 0.1f;
        public float Max { get; set; } = 3f;
        public float Step { get; set; } = 0.1f;
    }
    #endregion

    #region Ultimate Upscale
    public class UltimateUpscaleSettingsModel
    {
        public bool IsEnabled { get; set; } = false;
        public int UpscalerIndex { get; set; } = 0;
        public int SeamFixType { get; set; } = 0;
        public int TargetSizeType { get; set; } = 2;
        public int RedrawMode { get; set; } = 1;
        public bool SaveUpscaledImage { get; set; } = false;
        public bool SaveSeamFixImage { get; set; } = false;
        public UUSeamFixSettingsModel SeamFix { get; set; } = new();
        public UUTileResolutionSettingsModel TileResolution { get; set; } = new();
        public UUMaskBlurSettingsModel MaskBlur { get; set; } = new();
        public UUPaddingSettingsModel Padding { get; set; } = new();
        public UUTargetSizeResolutionSettingsModel TargetResolution { get; set; } = new();
        public UUTargetSizeScaleSettingsModel TargetScale { get; set; } = new();
        public List<string> TargetSizeTypes { get; set; } = new()
        {
            "Img2Img Settings",
            "Resolution",
            "Scale"
        };
        public List<string> SeamFixTypes { get; set; } = new()
        {
            "None",
            "Band pass",
            "Half tile offset pass",
            "Half tile offset pass + intersections"
        };
        public List<string> RedrawModes { get; set; } = new()
        {
            "Linear",
            "Chess",
            "None"
        };
    }
    public class UUTileResolutionSettingsModel
    {
        public int Width { get; set; } = 512;
        public int Heigth { get; set; } = 512;
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 2048;
        public int Step { get; set; } = 64;
    }
    public class UUMaskBlurSettingsModel
    {
        public int Value { get; set; } = 16;
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 64;
        public int Step { get; set; } = 1;
    }
    public class UUPaddingSettingsModel
    {
        public int Value { get; set; } = 32;
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 128;
        public int Step { get; set; } = 1;
    }
    public class UUTargetSizeResolutionSettingsModel
    {
        public int CustomWidth { get; set; } = 2048;
        public int CustomHeight { get; set; } = 2048;
        public int Min { get; set; } = 64;
        public int Max { get; set; } = 8192;
        public int Step { get; set; } = 64;
    }
    public class UUTargetSizeScaleSettingsModel
    {
        public float Value { get; set; } = 4f;
        public float Min { get; set; } = 1f;
        public float Max { get; set; } = 16f;
        public float Step { get; set; } = 0.1f;
    }
    public class UUSeamFixSettingsModel
    {
        public UUPaddingSettingsModel Padding { get; set; } = new();
        public UUMaskBlurSettingsModel MaskBlur { get; set; } = new();
        public UUSeamFixWidthSettingsModel Width { get; set; } = new();
        public UUSeamFixDenoiseSettingsModel Denoise { get; set; } = new();
    }
    public class UUSeamFixWidthSettingsModel
    {
        public int Value { get; set; } = 64;
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 128;
        public int Step { get; set; } = 1;
    }
    public class UUSeamFixDenoiseSettingsModel
    {
        public float Value { get; set; } = 0.25f;
        public float Min { get; set; } = 0f;
        public float Max { get; set; } = 1f;
        public float Step { get; set; } = 0.01f;
    }
    #endregion

    #region MultiDiffusion
    public class MultiDiffusionSettingsModel
    {
        public MultiDiffusionTiledDiffusionSettingsModel TiledDiffusion { get; set; } = new();
        public MultiDiffusionTiledVaeSettingsModel TiledVae { get; set; } = new();
    }

    #region Tiled Diffusion
    public class MultiDiffusionTiledDiffusionSettingsModel
    {
        public bool IsEnabled { get; set; } = false;
        public List<string> Methods { get; set; } = new()
        {
            "MultiDiffusion",
            "Mixture of Diffusers"
        };
        public string UpscalerIndex { get; set; } = "None";
        public bool ControlTensorCpu { get; set; } = false;
        public bool EnableBBoxControl { get; set; } = false;
        public bool DrawBackground { get; set; } = false;
        public bool CasualLayers { get; set; } = false;
        public MultiDiffusionNoiseInverseSettingsModel NoiseInverse { get; set; } = new();
        public MultiDiffusionLatentTileSettingsModel LatentTile { get; set; } = new();
        public MultiDiffusionImageSettingsModel Image { get; set; } = new();
        public MultiDiffusionBBoxSettingsModel BBoxControl { get; set; } = new();
    }

    public class MultiDiffusionNoiseInverseSettingsModel
    {
        public bool IsEnabled { get; set; } = false;
        public MultiDiffusionNoiseInverseStepsSettingsModel Steps { get; set; } = new();
        public MultiDiffusionNoiseInverseRetouchSettingsModel Retouch { get; set; } = new();
        public MultiDiffusionNoiseInverseRenoiseSettingsModel Renoise { get; set; } = new();
    }

    public class MultiDiffusionNoiseInverseStepsSettingsModel
    {
        public int Value { get; set; } = 10;
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 100;
        public int Step { get; set; } = 1;
    }

    public class MultiDiffusionNoiseInverseRetouchSettingsModel
    {
        public float Value { get; set; } = 1;
        public float Min { get; set; } = 1;
        public float Max { get; set; } = 100;
        public float Step { get; set; } = 0.1f;
    }

    public class MultiDiffusionNoiseInverseRenoiseSettingsModel
    {
        public MultiDiffusionNoiseInverseRenoiseStrengthSettingsModel Strength { get; set; } = new();
        public MultiDiffusionNoiseInverseRenoiseKernelSettingsModel Kernel { get; set; } = new();
    }

    public class MultiDiffusionNoiseInverseRenoiseStrengthSettingsModel
    {
        public float Value { get; set; } = 1;
        public float Min { get; set; } = 0;
        public float Max { get; set; } = 2;
        public float Step { get; set; } = 0.01f;
    }

    public class MultiDiffusionNoiseInverseRenoiseKernelSettingsModel
    {
        public int Value { get; set; } = 64;
        public int Min { get; set; } = 2;
        public int Max { get; set; } = 512;
        public int Step { get; set; } = 1;
    }

    public class MultiDiffusionLatentTileSettingsModel
    {
        public MultiDiffusionLatentTileResolutionSettingsModel Resolution { get; set; } = new();
        public MultiDiffusionLatentTileOverlapSettingsModel Overlap { get; set; } = new();
        public MultiDiffusionLatentTileBatchSettingsModel Batch { get; set; } = new();
    }

    public class MultiDiffusionLatentTileResolutionSettingsModel
    {
        public int Width { get; set; } = 96;
        public int Height { get; set; } = 96;
        public int Min { get; set; } = 16;
        public int Max { get; set; } = 256;
        public int Step { get; set; } = 16;
    }

    public class MultiDiffusionLatentTileOverlapSettingsModel
    {
        public int Value { get; set; } = 48;
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 256;
        public int Step { get; set; } = 4;
    }

    public class MultiDiffusionLatentTileBatchSettingsModel
    {
        public int Value { get; set; } = 1;
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 8;
        public int Step { get; set; } = 1;
    }

    public class MultiDiffusionImageSettingsModel
    {
        // Used in Txt2Img
        public bool OverwriteImageSize { get; set; } = false;
        public MultiDiffusionImageResolutionSettingsModel Resolution { get; set; } = new();
        // Used in Img2Img
        public bool KeepInputSize { get; set; } = true;
        public MultiDiffusionScaleSettingsModel Scale { get; set; } = new();
    }

    public class MultiDiffusionImageResolutionSettingsModel
    {
        public int Width { get; set; } = 1024;
        public int Height { get; set; } = 1024;
        public int Min { get; set; } = 256;
        public int Max { get; set; } = 8192;
        public int Step { get; set; } = 32;
    }

    public class MultiDiffusionScaleSettingsModel
    {
        public float Value { get; set; } = 2;
        public float Min { get; set; } = 1;
        public float Max { get; set; } = 8;
        public float Step { get; set; } = 0.1f;
    }

    public class MultiDiffusionBBoxSettingsModel
    {
        public bool IsEnabled { get; set; } = false;
        public string Prompt { get; set; } = string.Empty;
        public string NegativePrompt { get; set; } = string.Empty;
        public int Seed { get; set; } = -1;
        public MultiDiffusionBBoxMultiplierSettingsModel Multiplier { get; set; } = new();
        public MultiDiffusionBBoxRegionSettingsModel Region { get; set; } = new();
    }

    public class MultiDiffusionBBoxMultiplierSettingsModel
    {
        public float Value { get; set; } = 1f;
        public float Min { get; set; } = 0f;
        public float Max { get; set; } = 10f;
        public float Step { get; set; } = 0.1f;
    }

    public class MultiDiffusionBBoxRegionSettingsModel
    {
        public float CoordX { get; set; } = 0.4f;
        public float CoordY { get; set; } = 0.4f;
        public float Width { get; set; } = 0.2f;
        public float Height { get; set; } = 0.2f;
        public float Min { get; set; } = 0f;
        public float Max { get; set; } = 1f;
        public float Step { get; set; } = 0.01f;
    }
    #endregion

    #region Tiled Vae
    public class MultiDiffusionTiledVaeSettingsModel
    {
        public bool IsEnabled { get; set; } = false;
        public bool VaeToGpu { get; set; } = false;
        public bool FastDecoder { get; set; } = true;
        public bool FastEncoder { get; set; } = true;
        public bool ColorFix { get; set; } = false;
        public MultiDiffusionVaeEncoderSettingsModel Encoder { get; set; } = new();
        public MultiDiffusionVaeDecoderSettingsModel Decoder { get; set; } = new();
    }

    public class MultiDiffusionVaeEncoderSettingsModel
    {
        public int Value { get; set; } = 1536;
        public int Min { get; set; } = 256;
        public int Max { get; set; } = 4096;
        public int Step { get; set; } = 16;
    }

    public class MultiDiffusionVaeDecoderSettingsModel
    {
        public int Value { get; set; } = 96;
        public int Min { get; set; } = 48;
        public int Max { get; set; } = 512;
        public int Step { get; set; } = 16;
    }
    #endregion
    #endregion

    #region Regional Prompter
    public class RegionalPrompterSettingsModel
    {
        public bool IsEnabled { get; set; } = false;
        public bool IsDebug { get; set; } = false;
        public string DivideRatio { get; set; } = "1,1";
        public string BaseRatio { get; set; } = "0.2";
        public bool UseBasePrompt { get; set; } = false;
        public bool UseCommonPrompt { get; set; } = false;
        public bool UseNegativeCommonPrompt { get; set; } = false;
        public bool DisableConvertAND { get; set; } = false;
        public List<string> MatrixModes { get; set; } = new()
        {
            "Horizontal",
            "Vertical"
        };
        public List<string> GenerationModes { get; set; } = new()
        {
            "Attention",
            "Latent"
        };
    }
    #endregion

    #region XYZ Plot
    public class XYZPlotSettingsModel
    {
        public bool DrawLegend { get; set; } = true;
        public bool IncludeSubImages { get; set; } = false;
        public bool IncludeSubGrids { get; set; } = false;
        public bool RandomSeed { get; set; } = false;
        public XYZPlotMarginSettingsModel Margin { get; set; } = new();

        public List<XYZPlotType> AxisTypes { get; set; } = new()
        {
            new() { Name = "None", IsImg2Img = null },
            new() { Name = "Seed", IsImg2Img = null },
            new() { Name = "Var. Seed", IsImg2Img = null },
            new() { Name = "Var. Strenght", IsImg2Img = null },
            new() { Name = "Steps", IsImg2Img = null },
            new() { Name = "Hires Steps", IsImg2Img = false },
            new() { Name = "CFG Scale", IsImg2Img = null },
            new() { Name = "Image CFG Scale", IsImg2Img = true },
            new() { Name = "Prompt S/R", IsImg2Img = null },
            new() { Name = "Prompt Order", IsImg2Img = null },
            new() { Name = "Sampler", IsImg2Img = null },
            new() { Name = "Checkpoint Name", IsImg2Img = null },
            new() { Name = "Sigma Churn", IsImg2Img = null },
            new() { Name = "Sigma Min", IsImg2Img = null },
            new() { Name = "Sigma Max", IsImg2Img = null },
            new() { Name = "Sigma Noise", IsImg2Img = null },
            new() { Name = "ETA", IsImg2Img = null },
            new() { Name = "Clip Skip", IsImg2Img = null },
            new() { Name = "Denoising", IsImg2Img = null },
            new() { Name = "Hires Upscaler", IsImg2Img = false },
            new() { Name = "Cond. Image Mask Weight", IsImg2Img = true },
            new() { Name = "VAE", IsImg2Img = null },
            new() { Name = "Styles", IsImg2Img = null },
            new() { Name = "UniPC Order", IsImg2Img = null },
            new() { Name = "Face Restore", IsImg2Img = null },
            new() { Name = "ControlNet Enabled", IsImg2Img = null },
            new() { Name = "ControlNet Model", IsImg2Img = null },
            new() { Name = "ControlNet Weight", IsImg2Img = null },
            new() { Name = "ControlNet Guidance Start", IsImg2Img = null },
            new() { Name = "ControlNet Guidance End", IsImg2Img = null },
            new() { Name = "ControlNet Resize Mode", IsImg2Img = null },
            new() { Name = "ControlNet Preprocessor", IsImg2Img = null },
            new() { Name = "ControlNet Pre Resolution", IsImg2Img = null },
            new() { Name = "ControlNet Pre Threshold A", IsImg2Img = null },
            new() { Name = "ControlNet Pre Threshold B", IsImg2Img = null },
            new() { Name = "Cutoff Enabled", IsImg2Img = null },
            new() { Name = "Cutoff Targets", IsImg2Img = null },
            new() { Name = "Cutoff Weight", IsImg2Img = null },
            new() { Name = "Cutoff Disable Negative Prompt", IsImg2Img = null },
            new() { Name = "Cutoff Strong", IsImg2Img = null },
            new() { Name = "Cutoff Padding", IsImg2Img = null },
            new() { Name = "Cutoff Interpolation", IsImg2Img = null },
        };
    }

    public class XYZPlotType
    {
        public string Name { get; set; }
        // Null == both modes | False == Txt2Img only | True == Img2Img only
        public bool? IsImg2Img { get; set; }
    }

    public class XYZPlotMarginSettingsModel
    {
        public int Value { get; set; } = 0;
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 100;
        public int Step { get; set; } = 1;
    }
    #endregion

    #region ADetailer
    public class ADetailerSettingsModel
    {
        public List<string> Models { get; set; } = new()
        {
            "None",
            "face_yolov8n.pt",
            "face_yolov8s.pt",
            "hand_yolov8n.pt",
            "person_yolov8n-seg.pt",
            "person_yolov8s-seg.pt",
            "mediapipe_face_full",
            "mediapipe_face_short",
            "mediapipe_face_mesh",
            "mediapipe_face_mesh_eyes_only"
        };
        public List<string> MaskMergeModes { get; set; } = new() { "None", "Merge", "Merge and Invert" };

        public bool IsEnabled { get; set; } = false;
        public string Model { get; set; } = "None";
        public string Prompt { get; set; } = string.Empty;
        public string NegativePrompt { get; set; } = string.Empty;
        public ADetailerConfidenceSettings Confidence { get; set; } = new();
        public ADetailerMaskKLargestSettings MaskKLargest { get; set; } = new();
        public ADetailerMaskRatioSettings MaskRatio { get; set; } = new();
        public ADetailerMaskOffsetSettings MaskOffset { get; set; } = new();
        public ADetailerErosionDilationSettings MaskErosionDilation { get; set; } = new();
        public ADetailerMaskBlurSettings MaskBlur { get; set; } = new();
        public ADetailerDenoisingStrengthSettings DenoisingStrength { get; set; } = new();
        public bool InpaintOnlyMasked { get; set; } = true;
        public ADetailerInpaintMaskedPaddingSettings InpaintMaskedPadding { get; set; } = new();
        public bool UseInpaintWidthHeight { get; set; } = false;
        public bool UseSteps { get; set; } = false;
        public bool UseCFGScale { get; set; } = false;
        public bool UseCheckpoint { get; set; } = false;
        public string Checkpoint { get; set; } = "Use same checkpoint";
        public bool UseVAE { get; set; } = false;
        public string VAE { get; set; } = "Use same VAE";
        public bool UseSampler { get; set; } = false;
        public string Sampler { get; set; } = "DPM++ 2M Karras";
        public bool UseNoiseMultiplier { get; set; } = false;
        public ADetailerNoiseMultiplierSettings NoiseMultiplier { get; set; } = new();
        public bool UseClipSkip { get; set; } = false;
        public bool RestoreFace { get; set; } = false;
        public string ControlNetModel { get; set; } = "None";
        public string ControlNetModule { get; set; } = "None";
        public float ControlNetWeight { get; set; } = 1.0f;

        public class ADetailerConfidenceSettings
        {
            public float Value { get; set; } = 0.3f;
            public float Min { get; set; } = 0f;
            public float Max { get; set; } = 1f;
            public float Step { get; set; } = 0.01f;
        }

        public class ADetailerMaskKLargestSettings
        {
            public int Value { get; set; } = 0;
            public int Min { get; set; } = 0;
            public int Max { get; set; } = 10;
            public int Step { get; set; } = 1;
        }

        public class ADetailerMaskRatioSettings
        {
            public float ValueMin { get; set; } = 0f;
            public float ValueMax { get; set; } = 1f;
            public float Min { get; set; } = 0f;
            public float Max { get; set; } = 1f;
            public float Step { get; set; } = 0.001f;
        }

        public class ADetailerMaskOffsetSettings
        {
            public int ValueX { get; set; } = 0;
            public int ValueY { get; set; } = 0;
            public int Min { get; set; } = -200;
            public int Max { get; set; } = 200;
            public int Step { get; set; } = 1;
        }

        public class ADetailerErosionDilationSettings
        {
            public int Value { get; set; } = 4;
            public int Min { get; set; } = -128;
            public int Max { get; set; } = 128;
            public int Step { get; set; } = 4;
        }

        public class ADetailerMaskBlurSettings
        {
            public int Value { get; set; } = 4;
            public int Min { get; set; } = 0;
            public int Max { get; set; } = 64;
            public int Step { get; set; } = 1;
        }

        public class ADetailerDenoisingStrengthSettings
        {
            public float Value { get; set; } = 0.4f;
            public float Min { get; set; } = 0f;
            public float Max { get; set; } = 1f;
            public float Step { get; set; } = 0.01f;
        }

        public class ADetailerInpaintMaskedPaddingSettings
        {
            public int Value { get; set; } = 32;
            public int Min { get; set; } = 0;
            public int Max { get; set; } = 256;
            public int Step { get; set; } = 4;
        }

        public class ADetailerNoiseMultiplierSettings
        {
            public float Value { get; set; } = 1f;
            public float Min { get; set; } = 0.5f;
            public float Max { get; set; } = 1.5f;
            public float Step { get; set; } = 0.01f;

        }
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
        public CivitaiLimitSettingsModel Limit { get; set; } = new();
    }

    public class CivitaiLimitSettingsModel
    {
        public int Value { get; set; } = 15;
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 200;
        public int Step { get; set; } = 8;
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

    #region WebUI
    public class WebuiSettingsModel
    {
        public ClipSkipSettingsModel ClipSkip { get; set; } = new();
    }

    public class ClipSkipSettingsModel
    {
        public int Value { get; set; } = 1;
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 12;
        public int Step { get; set; } = 1;
    }
    #endregion
}
