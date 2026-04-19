using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Ltx;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.Ltx;

/// <summary>
/// LTX 2.3 Img2Vid workflow using two-pass sampling with latent upscaling.
/// Generates video with audio from a source image using a single checkpoint model.
/// Pipeline: Load Models -> LoRA (optional) -> Load Image -> Prompts -> Conditioning
///           -> Pass 1 (half-res: EmptyLatent + I2V + Concat + Sample)
///           -> Upsample -> CropGuides
///           -> Pass 2 (full-res: I2V + Concat + Sample)
///           -> Decode (Video + Audio) -> Save
/// </summary>
public class LtxImg2VidWorkflow : IWorkflowBuilder
{
    private readonly LtxLoadModelFragment _loadModelFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly LtxLoadImageFragment _loadImageFragment = new();
    private readonly PromptsFragment _promptsFragment = new();
    private readonly LtxConditioningFragment _conditioningFragment = new();
    private readonly LtxEmptyLatentFragment _emptyLatentFragment = new();
    private readonly LtxImgToVideoFragment _imgToVideoFragment = new();
    private readonly LtxConcatAVLatentFragment _concatAVFragment = new();
    private readonly LtxSamplingPassFragment _samplingPassFragment = new();
    private readonly LtxUpsampleLatentFragment _upsampleLatentFragment = new();
    private readonly LtxDecodeFragment _decodeFragment = new();
    private readonly LtxVideoSettingsFragment _videoSettingsFragment = new();
    private readonly LtxSamplerFragment _samplerFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Img2Vid",
        Base = Data.Enums.ModelBase.LTX,
        Mode = ModeType.Img2Vid,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "Model",
                Label = "Model",
                Type = AssetType.CheckpointModel,
                DefaultValue = "ltx-2.3-22b-dev-fp8.safetensors",
                Order = 1,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = "TextEncoder",
                Label = "Text Encoder",
                Type = AssetType.Clip,
                DefaultValue = "gemma_3_12B_it_fp4_mixed.safetensors",
                Order = 2,
                ColumnSize = 6
            }
        ],
        Sources =
        [
            new WorkflowSource
            {
                Id = "source_image",
                Label = "Source Image",
                Type = SourceType.Image,
                Required = true
            }
        ]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _loadImageFragment;
        yield return _videoSettingsFragment;
        yield return _samplerFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        // Read video settings
        var videoSettings = parameters.GetFragment(_videoSettingsFragment.Metadata.Id);
        var duration = videoSettings?.GetInt("duration", 5) ?? 5;
        var frameRate = videoSettings?.GetInt("frame_rate", 25) ?? 25;
        var imgCompression = videoSettings?.GetInt("img_compression", 18) ?? 18;
        var i2vStrength = videoSettings?.GetDouble("i2v_strength", 0.7) ?? 0.7;

        // Read sampler settings
        var samplerSettings = parameters.GetFragment(_samplerFragment.Metadata.Id);
        var seed = samplerSettings?.GetLong("seed", -1) ?? -1;
        if (seed < 0) seed = Random.Shared.NextInt64(0, int.MaxValue);
        var cfg = samplerSettings?.GetDouble("cfg", 1.0) ?? 1.0;

        // Read resolution
        var imageFragment = parameters.GetFragment(_loadImageFragment.Metadata.Id);
        var width = imageFragment?.GetInt("width", 1280) ?? 1280;
        var height = imageFragment?.GetInt("height", 720) ?? 720;
        var frameCount = duration * frameRate + 1;
        var halfWidth = width / 2;
        var halfHeight = height / 2;

        // Get source image path
        var source = parameters.Sources?.GetValueOrDefault("source_image");
        var imagePath = source?.FilePath ?? source?.Filename ?? "";

        // 1. Load Models (checkpoint, text encoder, audio VAE, upscale model)
        _loadModelFragment.Build(builder, registry, new LtxLoadModelFragment.Parameters
        {
            CheckpointName = parameters.Assets?.GetValueOrDefault("Model") ?? "ltx-2.3-22b-dev-fp8.safetensors",
            TextEncoderName = parameters.Assets?.GetValueOrDefault("TextEncoder") ?? "gemma_3_12B_it_fp4_mixed.safetensors"
        });

        // 2. LoRAs (dynamic app nodes)
        _loraLoaderFragment.BuildAll(builder, registry, parameters.Loras);

        // 3. Load + Preprocess Image
        _loadImageFragment.Build(builder, registry, new LtxLoadImageFragment.Parameters
        {
            ImagePath = imagePath,
            Width = width,
            Height = height,
            ImgCompression = imgCompression
        });

        // 4. Encode Prompts (reads clip_output, possibly LoRA-modified)
        var promptsData = parameters.GetFragment("prompts");
        _promptsFragment.Build(builder, registry, new PromptsFragment.Parameters
        {
            Positive = promptsData?.GetString("positive", "") ?? "",
            Negative = promptsData?.GetString("negative", "") ?? ""
        });

        // 5. LTX Conditioning (wraps prompts with frame_rate)
        _conditioningFragment.Build(builder, registry, new LtxConditioningFragment.Parameters
        {
            FrameRate = frameRate
        });

        // === PASS 1: Half-resolution generation ===

        // 6. Empty Latent (half-res video + audio)
        _emptyLatentFragment.Build(builder, registry, new LtxEmptyLatentFragment.Parameters
        {
            Width = halfWidth,
            Height = halfHeight,
            Length = frameCount,
            BatchSize = 1,
            FrameRate = frameRate
        });

        // 7. I2V Injection (pass 1, reduced strength)
        _imgToVideoFragment.Build(builder, registry, new LtxImgToVideoFragment.Parameters
        {
            NodeId = "ltx_i2v_pass1",
            Strength = i2vStrength,
            Bypass = false,
            Title = "LTXVImgToVideoInplace (Pass 1)"
        });

        // 8. Concat AV (pass 1)
        _concatAVFragment.Build(builder, registry, new LtxConcatAVLatentFragment.Parameters
        {
            NodeId = "ltx_concat_av_pass1",
            Title = "LTXVConcatAVLatent (Pass 1)"
        });

        // 9. Sampling (pass 1 - generation)
        _samplingPassFragment.Build(builder, registry, new LtxSamplingPassFragment.Parameters
        {
            PassId = "pass1",
            Seed = seed,
            Cfg = cfg,
            SamplerName = "euler_ancestral_cfg_pp",
            Sigmas = "1.0, 0.99375, 0.9875, 0.98125, 0.975, 0.909375, 0.725, 0.421875, 0.0",
            PositiveInputName = "ltx_positive_output",
            NegativeInputName = "ltx_negative_output",
            Title = "SamplerCustomAdvanced (Pass 1)"
        });

        // === BETWEEN PASSES: Upsample ===

        // 10. Separate + Upsample Latent
        _upsampleLatentFragment.Build(builder, registry, new LtxUpsampleLatentFragment.Parameters());

        // 11. Crop Conditioning to match upsampled latent
        _conditioningFragment.BuildCropped(builder, registry, new LtxConditioningFragment.CropParameters());

        // === PASS 2: Full-resolution refinement ===

        // 12. I2V Injection (pass 2, full strength)
        _imgToVideoFragment.Build(builder, registry, new LtxImgToVideoFragment.Parameters
        {
            NodeId = "ltx_i2v_pass2",
            Strength = 1.0,
            Bypass = false,
            Title = "LTXVImgToVideoInplace (Pass 2)"
        });

        // 13. Concat AV (pass 2)
        _concatAVFragment.Build(builder, registry, new LtxConcatAVLatentFragment.Parameters
        {
            NodeId = "ltx_concat_av_pass2",
            Title = "LTXVConcatAVLatent (Pass 2)"
        });

        // 14. Sampling (pass 2 - refinement, fixed seed, different sampler + sigmas)
        _samplingPassFragment.Build(builder, registry, new LtxSamplingPassFragment.Parameters
        {
            PassId = "pass2",
            Seed = 42,
            Cfg = cfg,
            SamplerName = "euler_cfg_pp",
            Sigmas = "0.85, 0.7250, 0.4219, 0.0",
            PositiveInputName = "ltx_cropped_positive_output",
            NegativeInputName = "ltx_cropped_negative_output",
            Title = "SamplerCustomAdvanced (Pass 2)"
        });

        // === DECODE + OUTPUT ===

        // 15. Decode + Save
        _decodeFragment.Build(builder, registry, new LtxDecodeFragment.Parameters
        {
            FrameRate = frameRate
        });

        return builder.ToComfyWorkflow(registry);
    }
}
