using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Enhancements;
using BlazorWebApp.Workflows.Fragments.MiniMax;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.MiniMax;

/// <summary>
/// MiniMax H3 omni-modal workflow. Generates video with native stereo audio from a text prompt,
/// optionally guided by a first and/or last frame image (falls back to text-to-video when neither
/// is supplied). Pipeline: Load Models -> LoRA (optional) -> SageAttention patch (optional) ->
/// Load first/last frame (optional) -> MiniMaxH3ImageToVideo -> Sampler -> Decode (video+audio) -> Save.
/// </summary>
public class MiniMaxImg2VidWorkflow : IWorkflowBuilder
{
    private readonly MiniMaxLoadModelsFragment _loadModelsFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly MiniMaxSagePatchFragment _sagePatchFragment = new();
    private readonly MiniMaxLoadImageFragment _loadImageFragment = new();
    private readonly PromptsFragment _promptsFragment = new();
    private readonly MiniMaxVideoSettingsFragment _videoSettingsFragment = new();
    private readonly MiniMaxImageToVideoFragment _imageToVideoFragment = new();
    private readonly MiniMaxSamplerFragment _samplerFragment = new();
    private readonly MiniMaxDecodeFragment _decodeFragment = new();
    private readonly RTXUpscaleFragment _rtxUpscaleFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "H3 T/I2V",
        Description = "MiniMax's omni-modal generation model: produces video with native stereo audio (voice, SFX, music) " +
                      "in a single pass. Supply an optional first and/or last frame to guide the shot, or leave both empty " +
                      "for text-to-video. Negative prompts are not used by this model. For best results, use the \"MiniMax H3 Video\" " +
                      "LLM template in the Prompt Utils drawer to convert a plain scene idea into the required shot/soundscape format.",
        Base = Data.Enums.ModelBase.MiniMax,
        Mode = ModeType.Img2Vid,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "UNet",
                Label = "Diffusion Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "minimax_h3_fl2va_pruned_int8_convrot.safetensors",
                Order = 1,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = "Clip",
                Label = "CLIP",
                Type = AssetType.Clip,
                DefaultValue = "qwen3vl_32b_minimax_h3_nvfp4_awq.safetensors",
                Order = 2,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = "VideoVae",
                Label = "Video VAE",
                Type = AssetType.Vae,
                DefaultValue = "minimax_h3_video_vae_fp16.safetensors",
                Order = 3,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = "AudioVae",
                Label = "Audio VAE",
                Type = AssetType.Vae,
                DefaultValue = "minimax_h3_audio_vae_fp32.safetensors",
                Order = 4,
                ColumnSize = 6
            }
        ],
        Sources =
        [
            new WorkflowSource
            {
                Id = "first_frame",
                Label = "First Frame",
                Type = SourceType.Image,
                Required = false,
                Parameter = "first_frame"
            },
            new WorkflowSource
            {
                Id = "last_frame",
                Label = "Last Frame",
                Type = SourceType.Image,
                Required = false,
                Parameter = "last_frame"
            }
        ],
        CompatibleResourceBaseModels = ["MiniMax H3"]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _videoSettingsFragment;
        yield return _samplerFragment;
        yield return _sagePatchFragment;
        yield return _rtxUpscaleFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var videoSettings = parameters.GetFragment(_videoSettingsFragment.Metadata.Id);
        var videoDefaults = _videoSettingsFragment.DefaultValues;
        var resolutionMode = videoSettings?.GetString("resolution_mode", videoDefaults.ResolutionMode) ?? videoDefaults.ResolutionMode;
        var aspectRatio = videoSettings?.GetString("aspect_ratio", videoDefaults.AspectRatio) ?? videoDefaults.AspectRatio;
        var megapixels = videoSettings?.GetDouble("megapixels", videoDefaults.Megapixels) ?? videoDefaults.Megapixels;
        var multipleOf = videoSettings?.GetInt("multiple_of", videoDefaults.MultipleOf) ?? videoDefaults.MultipleOf;
        var manualWidth = videoSettings?.GetInt("width", videoDefaults.Width) ?? videoDefaults.Width;
        var manualHeight = videoSettings?.GetInt("height", videoDefaults.Height) ?? videoDefaults.Height;
        var duration = videoSettings?.GetDouble("duration", videoDefaults.Duration) ?? videoDefaults.Duration;

        var firstFrame = parameters.Sources.GetValueOrDefault("first_frame");
        var lastFrame = parameters.Sources.GetValueOrDefault("last_frame");
        var hasFirstFrame = firstFrame?.HasData == true;
        var hasLastFrame = lastFrame?.HasData == true;

        int width, height;
        if (string.Equals(resolutionMode, "manual", StringComparison.OrdinalIgnoreCase))
        {
            width = manualWidth;
            height = manualHeight;
        }
        else
        {
            // First frame takes precedence over last frame when both are set and mismatch.
            var ratioSource = hasFirstFrame ? firstFrame : hasLastFrame ? lastFrame : null;
            if (ratioSource?.Width is > 0 && ratioSource.Height is > 0)
            {
                var size = WorkflowSizing.ScaleToTotalPixels(ratioSource.Width, ratioSource.Height, megapixels, multipleOf);
                width = size.Width;
                height = size.Height;
            }
            else
            {
                (width, height) = BlazorWebApp.Models.ResolutionSelectorMath.Calculate(aspectRatio, megapixels, multipleOf);
            }
        }

        var length = CalculateLength(duration);
        var prompt = parameters.GetFragment("prompts")?.GetString("positive", "") ?? "";

        // 1. Load models (UNet, CLIP[minimax], video VAE, audio VAE)
        _loadModelsFragment.Build(builder, parameters, registry);

        // 2. LoRAs (optional, app-generated)
        _loraLoaderFragment.BuildAll(builder, registry, parameters.Loras);

        // 3. SageAttention patch (optional, default on)
        if (parameters.GetFragment(_sagePatchFragment.Metadata.Id)?.IsActive != false)
        {
            _sagePatchFragment.BuildPatch(builder, registry);
        }

        // 4. Load optional guiding frames (raw, unresized - the model node resizes internally)
        if (hasFirstFrame)
        {
            _loadImageFragment.Build(builder, registry, new MiniMaxLoadImageFragment.Parameters
            {
                NodePrefix = "minimax_first_frame",
                Title = "First Frame",
                ImagePath = firstFrame!.Filename ?? firstFrame.FilePath ?? "",
                OutputName = "minimax_first_frame_image"
            });
        }

        if (hasLastFrame)
        {
            _loadImageFragment.Build(builder, registry, new MiniMaxLoadImageFragment.Parameters
            {
                NodePrefix = "minimax_last_frame",
                Title = "Last Frame",
                ImagePath = lastFrame!.Filename ?? lastFrame.FilePath ?? "",
                OutputName = "minimax_last_frame_image"
            });
        }

        // 5. MiniMaxH3ImageToVideo (raw prompt string, no CLIPTextEncode)
        _imageToVideoFragment.Build(builder, registry, new MiniMaxImageToVideoFragment.Parameters
        {
            Prompt = prompt,
            Width = width,
            Height = height,
            Length = length,
            FirstFrameInputName = hasFirstFrame ? "minimax_first_frame_image" : null,
            LastFrameInputName = hasLastFrame ? "minimax_last_frame_image" : null
        });

        // 6. Sampler (RandomNoise -> KSamplerSelect -> BasicScheduler -> BasicGuider -> SamplerCustomAdvanced)
        _samplerFragment.Build(builder, parameters, registry);

        // 7. Decode (video + audio) -> [RTX Super Resolution] -> Create/Save video
        _decodeFragment.Build(builder, registry, new MiniMaxDecodeFragment.Parameters { SaveVideo = false });
        _rtxUpscaleFragment.Build(builder, parameters, registry);
        MiniMaxDecodeFragment.BuildSave(builder, registry, new MiniMaxDecodeFragment.Parameters());

        return builder.ToComfyWorkflow(registry);
    }

    /// <summary>
    /// Converts a duration in seconds to a valid MiniMaxH3ImageToVideo <c>length</c> (frames),
    /// snapping up to the model's 17-frame-per-block (17k+5) grid at 24fps. Preserves the exact
    /// formula from the source ComfyMathExpression node:
    /// <c>max(5, round(duration*24)) + (5 - (raw % 17)) % 17</c>.
    /// </summary>
    private static int CalculateLength(double durationSeconds)
    {
        var raw = Math.Max(5, (int)Math.Round(durationSeconds * 24, MidpointRounding.AwayFromZero));
        var mod = ((raw % 17) + 17) % 17;
        var length = raw + ((5 - mod) % 17 + 17) % 17;
        return Math.Clamp(length, 5, 3600);
    }
}
