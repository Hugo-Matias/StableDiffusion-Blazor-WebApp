using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Enhancements;
using BlazorWebApp.Workflows.Fragments.MiniMax;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.MiniMax;

/// <summary>
/// MiniMax H3 omni-modal reference-to-video workflow. Conditions the shot on any mix of up to 9
/// reference images, 1 reference video (optionally with its own audio), and 1 standalone
/// reference audio clip - referenced in the prompt via &lt;Picture N&gt;/&lt;Video N&gt;/&lt;Audio N&gt;
/// tags. Pipeline: Load Models -&gt; LoRA (optional) -&gt; SageAttention patch (optional) -&gt; Load
/// references -&gt; MiniMaxH3ReferenceToVideo -&gt; Sampler -&gt; Decode (video+audio) -&gt; Save.
/// </summary>
public class MiniMaxRefToVideoWorkflow : IWorkflowBuilder
{
    private readonly MiniMaxLoadModelsFragment _loadModelsFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly MiniMaxSagePatchFragment _sagePatchFragment = new();
    private readonly MiniMaxLoadImageFragment _loadImageFragment = new();
    private readonly LoadVideoFragment _loadVideoFragment = new();
    private readonly MiniMaxLoadAudioFragment _loadAudioFragment = new();
    private readonly PromptsFragment _promptsFragment = new();
    private readonly MiniMaxRefVideoSettingsFragment _refVideoSettingsFragment = new();
    private readonly MiniMaxReferenceToVideoFragment _referenceToVideoFragment = new();
    private readonly MiniMaxSamplerFragment _samplerFragment = new();
    private readonly MiniMaxDecodeFragment _decodeFragment = new();
    private readonly RTXUpscaleFragment _rtxUpscaleFragment = new();

    /// <summary>Node's real cap for ref_images (probed via /object_info).</summary>
    private const int MaxRefImages = 9;

    public WorkflowMetadata Metadata => new()
    {
        Title = "H3 Ref2Vid",
        Description = "MiniMax's omni-modal generation model: conditions a video (with native stereo audio) on any " +
                      "mix of reference images, a reference video, and a standalone reference audio clip. Reference " +
                      "each input in the prompt using <Picture N>/<Video N>/<Audio N> tags, in upload order. " +
                      "Negative prompts are not used by this model. For best results, use the \"MiniMax H3 Ref2Vid\" LLM " +
                      "template in the Prompt Utils drawer to convert a plain idea plus reference notes into the required format.",
        Base = Data.Enums.ModelBase.MiniMax,
        Mode = ModeType.Ref2Vid,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "UNet",
                Label = "Diffusion Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "minimax_h3_ref2va_pruned_int8_convrot.safetensors",
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
                Id = "ref_image",
                Label = "Reference Image",
                Type = SourceType.Image,
                Required = false,
                AllowMultiple = true
            },
            new WorkflowSource
            {
                Id = "ref_video",
                Label = "Reference Video",
                Type = SourceType.Video,
                Required = false,
                ShowIncludeAudioToggle = true,
                DefaultVideoOptions = new BlazorWebApp.Models.VideoSourceOptions
                {
                    ForceRate = 24,
                    Format = "None"
                }
            },
            new WorkflowSource
            {
                Id = "ref_audio",
                Label = "Reference Audio",
                Type = SourceType.Audio,
                Required = false
            }
        ],
        CompatibleResourceBaseModels = ["MiniMax H3"]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _refVideoSettingsFragment;
        yield return _samplerFragment;
        yield return _sagePatchFragment;
        yield return _rtxUpscaleFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var videoSettings = parameters.GetFragment(_refVideoSettingsFragment.Metadata.Id);
        var videoDefaults = _refVideoSettingsFragment.DefaultValues;
        var resolutionMode = videoSettings?.GetString("resolution_mode", videoDefaults.ResolutionMode) ?? videoDefaults.ResolutionMode;
        var aspectRatio = videoSettings?.GetString("aspect_ratio", videoDefaults.AspectRatio) ?? videoDefaults.AspectRatio;
        var megapixels = videoSettings?.GetDouble("megapixels", videoDefaults.Megapixels) ?? videoDefaults.Megapixels;
        var multipleOf = videoSettings?.GetInt("multiple_of", videoDefaults.MultipleOf) ?? videoDefaults.MultipleOf;
        var manualWidth = videoSettings?.GetInt("width", videoDefaults.Width) ?? videoDefaults.Width;
        var manualHeight = videoSettings?.GetInt("height", videoDefaults.Height) ?? videoDefaults.Height;
        var duration = videoSettings?.GetDouble("duration", videoDefaults.Duration) ?? videoDefaults.Duration;
        var refImageSize = videoSettings?.GetString("ref_image_size", videoDefaults.RefImageSize) ?? videoDefaults.RefImageSize;

        // Collect reference image sources (ref_image, ref_image_1, ref_image_2, ...), capped at the node's real max.
        var refImages = parameters.Sources
            .Where(kvp => kvp.Key.StartsWith("ref_image", StringComparison.Ordinal) && kvp.Value.HasData)
            .OrderBy(kvp => kvp.Key)
            .Take(MaxRefImages)
            .ToList();

        var refVideoSource = parameters.Sources.GetValueOrDefault("ref_video");
        var refAudioSource = parameters.Sources.GetValueOrDefault("ref_audio");
        var hasRefVideo = refVideoSource?.HasData == true;
        var hasRefAudio = refAudioSource?.HasData == true;
        var includeVideoAudio = hasRefVideo && refVideoSource!.VideoOptions?.IncludeAudio == true;

        int width, height;
        if (string.Equals(resolutionMode, "manual", StringComparison.OrdinalIgnoreCase))
        {
            width = manualWidth;
            height = manualHeight;
        }
        else
        {
            var firstImage = refImages.Count > 0 ? refImages[0].Value : null;
            if (firstImage?.Width is > 0 && firstImage.Height is > 0)
            {
                var size = WorkflowSizing.ScaleToTotalPixels(firstImage.Width, firstImage.Height, megapixels, multipleOf);
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

        // 4. Load reference images (raw, unresized - the model node resizes internally)
        var refImageOutputNames = new List<string>();
        for (var i = 0; i < refImages.Count; i++)
        {
            var outputName = $"minimax_ref_image_{i}_image";
            _loadImageFragment.Build(builder, registry, new MiniMaxLoadImageFragment.Parameters
            {
                NodePrefix = $"minimax_ref_image_{i}",
                Title = $"Reference Image {i + 1}",
                ImagePath = refImages[i].Value.Filename ?? refImages[i].Value.FilePath ?? "",
                OutputName = outputName
            });
            refImageOutputNames.Add(outputName);
        }

        // 5. Load optional reference video (frames + its own audio, only wired when the
        //    "Include Audio" toggle on the source panel is on)
        string? refVideoOutputName = null;
        string? refVideoAudioOutputName = null;
        if (hasRefVideo)
        {
            var videoOptions = refVideoSource!.VideoOptions;
            _loadVideoFragment.Build(builder, registry, new LoadVideoFragment.Parameters
            {
                Video = refVideoSource.Filename ?? refVideoSource.FilePath ?? "",
                ForceRate = videoOptions?.ForceRate is > 0 ? videoOptions.ForceRate : 24,
                CustomWidth = videoOptions?.CustomWidth ?? 0,
                CustomHeight = videoOptions?.CustomHeight ?? 0,
                FrameLoadCap = videoOptions?.FrameLoadCap ?? 0,
                SkipFirstFrames = videoOptions?.SkipFirstFrames ?? 0,
                SelectEveryNth = videoOptions?.SelectEveryNth is > 0 ? videoOptions.SelectEveryNth : 1,
                Format = string.IsNullOrWhiteSpace(videoOptions?.Format) ? "None" : videoOptions.Format
            }, scope: "ref_video_");

            refVideoOutputName = "ref_video_video_frames";
            if (includeVideoAudio)
            {
                refVideoAudioOutputName = "ref_video_audio";
            }
        }

        // 6. Load optional standalone reference audio
        string? refAudioOutputName = null;
        if (hasRefAudio)
        {
            refAudioOutputName = "minimax_ref_audio_output";
            _loadAudioFragment.Build(builder, registry, new MiniMaxLoadAudioFragment.Parameters
            {
                NodePrefix = "minimax_ref_audio",
                Title = "Reference Audio",
                AudioPath = refAudioSource!.Filename ?? refAudioSource.FilePath ?? "",
                TrimStart = refAudioSource.AudioOptions?.TrimStart ?? 0,
                TrimDuration = refAudioSource.AudioOptions?.TrimDuration ?? 0,
                OutputName = refAudioOutputName
            });
        }

        // 7. MiniMaxH3ReferenceToVideo (raw prompt string, no CLIPTextEncode)
        _referenceToVideoFragment.Build(builder, registry, new MiniMaxReferenceToVideoFragment.Parameters
        {
            Prompt = prompt,
            Width = width,
            Height = height,
            Length = length,
            RefImageSize = refImageSize,
            RefImageInputNames = refImageOutputNames,
            RefVideoInputName = refVideoOutputName,
            RefVideoAudioInputName = refVideoAudioOutputName,
            RefAudioInputName = refAudioOutputName
        });

        // 8. Sampler (RandomNoise -> KSamplerSelect -> BasicScheduler -> BasicGuider -> SamplerCustomAdvanced)
        _samplerFragment.Build(builder, parameters, registry);

        // 9. Decode (video + audio) -> [RTX Super Resolution] -> Create/Save video
        _decodeFragment.Build(builder, registry, new MiniMaxDecodeFragment.Parameters { SaveVideo = false });
        _rtxUpscaleFragment.Build(builder, parameters, registry);
        MiniMaxDecodeFragment.BuildSave(builder, registry, new MiniMaxDecodeFragment.Parameters());

        return builder.ToComfyWorkflow(registry);
    }

    /// <summary>
    /// Converts a duration in seconds to a valid MiniMaxH3ReferenceToVideo <c>length</c> (frames),
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
