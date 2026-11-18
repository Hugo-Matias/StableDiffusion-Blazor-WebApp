using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Workflows.Templates.Qwen;
using FluentAssertions;
using System.Text.Json;
using Xunit;
using WorkflowAssetType = BlazorWebApp.Workflows.Models.AssetType;

namespace BlazorWebApp.Tests.Workflows;

public class QwenPoseTransferWorkflowTests
{
    private readonly QwenPoseTransferWorkflow _workflow = new();

    [Fact]
    public void Metadata_ShouldExposeQwenImg2ImgWorkflow()
    {
        _workflow.Metadata.Title.Should().Be("Pose Transfer");
        _workflow.Metadata.Base.Should().Be(Data.Enums.ModelBase.Qwen);
        _workflow.Metadata.Mode.Should().Be(ModeType.Img2Img);
        _workflow.Metadata.Description.Should().NotBeNullOrWhiteSpace();
        _workflow.Metadata.CompatibleResourceBaseModels.Should().Contain(["Qwen", "Qwen 2"]);
    }

    [Fact]
    public void Metadata_ShouldExposeModelPoseAndLoraAssets()
    {
        var assets = _workflow.Metadata.Assets.ToList();

        assets.Should().Contain(a => a.Parameter == "Model" && a.Type == WorkflowAssetType.DiffusionModel && a.DefaultValue == "qwen_image_edit_2511_fp8mixed.safetensors");
        assets.Should().Contain(a => a.Parameter == "Clip" && a.Type == WorkflowAssetType.Clip && a.DefaultValue == "qwen_2.5_vl_7b_fp8_scaled.safetensors");
        assets.Should().Contain(a => a.Parameter == "Vae" && a.Type == WorkflowAssetType.Vae && a.DefaultValue == "qwen_image_vae.safetensors");
        assets.Should().Contain(a => a.Parameter == "PoseCheckpoint" && a.Type == WorkflowAssetType.CheckpointModel && a.DefaultValue == "SDPose/sdpose_wholebody_fp16.safetensors");
        assets.Should().Contain(a => a.Parameter == "LightningLora" && a.Type == WorkflowAssetType.Lora && a.DefaultValue == "Qwen/Qwen-Image-Edit-2511-Lightning-4steps-V1.0-bf16.safetensors");
        assets.Should().Contain(a => a.Parameter == "ConsistencyLora" && a.Type == WorkflowAssetType.Lora && a.DefaultValue == "Util/qe2511_consis_alpha_patched.safetensors");
    }

    [Fact]
    public void Metadata_ShouldExposeTwoImageSources()
    {
        var sources = _workflow.Metadata.Sources.ToList();

        sources.Should().Contain(s => s.Id == "target_image" && s.Type == SourceType.Image && s.Required);
        sources.Should().Contain(s => s.Id == "pose_reference_image" && s.Type == SourceType.Image && s.Required);
    }

    [Fact]
    public void GetFragments_ShouldExposePromptSettingsAndSampler()
    {
        var fragments = _workflow.GetFragments().ToList();

        fragments.Should().Contain(f => f.Metadata.Id == "prompts");
        fragments.Should().Contain(f => f.Metadata.Id == "pose_transfer_settings" && f.Metadata.Component == "QwenPoseTransferSettingsForm");
        fragments.Should().Contain(f => f.Metadata.Id == "main_sampler" && f.Metadata.Component == "SamplerForm");
    }

    [Fact]
    public void Build_ShouldWirePoseExtractionConditioningAndSampling()
    {
        using var json = BuildJson();

        json.RootElement.GetProperty("pose_checkpoint_loader").GetProperty("inputs").GetProperty("ckpt_name").GetString()
            .Should().Be("SDPose/sdpose_wholebody_fp16.safetensors");
        json.RootElement.GetProperty("pose_keypoint_extractor").GetProperty("class_type").GetString().Should().Be("SDPoseKeypointExtractor");
        json.RootElement.GetProperty("pose_keypoint_draw").GetProperty("class_type").GetString().Should().Be("SDPoseDrawKeypoints");
        json.RootElement.GetProperty("qwen_pose_encode").GetProperty("class_type").GetString().Should().Be("TextEncodeQwenImageEditPlus");
        json.RootElement.GetProperty("qwen_pose_positive_method").GetProperty("class_type").GetString().Should().Be("FluxKontextMultiReferenceLatentMethod");
        json.RootElement.GetProperty("qwen_pose_negative_zero").GetProperty("class_type").GetString().Should().Be("ConditioningZeroOut");
        json.RootElement.GetProperty("qwen_pose_repeat_latent").GetProperty("class_type").GetString().Should().Be("RepeatLatentBatch");

        var samplerInputs = json.RootElement.GetProperty("sampler_main").GetProperty("inputs");
        samplerInputs.GetProperty("positive")[0].GetString().Should().Be("qwen_pose_positive_method");
        samplerInputs.GetProperty("negative")[0].GetString().Should().Be("qwen_pose_negative_method");
        samplerInputs.GetProperty("latent_image")[0].GetString().Should().Be("qwen_pose_repeat_latent");
    }

    [Fact]
    public void Build_ShouldUseConfiguredFixedLoraStrengths()
    {
        using var json = BuildJson();

        var lightning = json.RootElement.GetProperty("qwen_lightning_lora_loader").GetProperty("inputs");
        lightning.GetProperty("lora_name").GetString().Should().Be("Qwen/Qwen-Image-Edit-2511-Lightning-4steps-V1.0-bf16.safetensors");
        lightning.GetProperty("strength_model").GetDouble().Should().Be(0.9);

        var consistency = json.RootElement.GetProperty("qwen_consistency_lora_loader").GetProperty("inputs");
        consistency.GetProperty("lora_name").GetString().Should().Be("Util/qe2511_consis_alpha_patched.safetensors");
        consistency.GetProperty("strength_model").GetDouble().Should().Be(0.55);
    }

    [Fact]
    public void Build_WithUserLora_ShouldApplyStandardLoraLoaderBeforeSampling()
    {
        var parameters = CreateParameters();
        parameters.Loras.Add(new Lora
        {
            Name = "Qwen/custom.safetensors",
            Path = "Qwen/custom.safetensors",
            Strength = 0.75f,
            IsEnabled = true
        });

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("lora_loader_0").GetProperty("class_type").GetString().Should().Be("LoraLoader");
        json.RootElement.GetProperty("sampler_main").GetProperty("inputs").GetProperty("model")[0].GetString().Should().Be("lora_loader_0");
    }

    [Fact]
    public void Build_ShouldUseStandardImageSavePrefix()
    {
        using var json = BuildJson();

        json.RootElement.GetProperty("save").GetProperty("inputs").GetProperty("filename_prefix").GetString()
            .Should().Be(SaveFragment.StandardFilenamePrefix);
    }

    private JsonDocument BuildJson()
    {
        var workflow = _workflow.Build(CreateParameters());
        return JsonDocument.Parse(workflow.Json);
    }

    private static GenerationParameters CreateParameters()
    {
        var parameters = new GenerationParameters();
        parameters.Assets["Model"] = "qwen_image_edit_2511_fp8mixed.safetensors";
        parameters.Assets["Clip"] = "qwen_2.5_vl_7b_fp8_scaled.safetensors";
        parameters.Assets["Vae"] = "qwen_image_vae.safetensors";
        parameters.Assets["PoseCheckpoint"] = "SDPose/sdpose_wholebody_fp16.safetensors";
        parameters.Assets["LightningLora"] = "Qwen/Qwen-Image-Edit-2511-Lightning-4steps-V1.0-bf16.safetensors";
        parameters.Assets["ConsistencyLora"] = "Util/qe2511_consis_alpha_patched.safetensors";

        parameters.Sources["target_image"] = new SourceAsset { Filename = "target.png" };
        parameters.Sources["pose_reference_image"] = new SourceAsset { Filename = "pose.png" };

        var prompts = parameters.GetOrCreateFragment("prompts");
        prompts.SetValue("positive", "match the pose from image2");
        prompts.SetValue("negative", "");

        var settings = parameters.GetOrCreateFragment("pose_transfer_settings");
        settings.SetValue("scale_length", 1280);
        settings.SetValue("pose_batch_size", 16);
        settings.SetValue("draw_body", true);
        settings.SetValue("draw_hands", true);
        settings.SetValue("draw_face", false);
        settings.SetValue("draw_feet", false);
        settings.SetValue("stick_width", 4);
        settings.SetValue("face_point_size", 2);
        settings.SetValue("score_threshold", 0.3);
        settings.SetValue("lightning_lora_strength", 0.9);
        settings.SetValue("consistency_lora_strength", 0.55);

        var sampler = parameters.GetOrCreateFragment("main_sampler");
        sampler.SetValue("sampler_name", "euler");
        sampler.SetValue("scheduler", "simple");
        sampler.SetValue("steps", 8);
        sampler.SetValue("cfg", 1.0);
        sampler.SetValue("denoise", 1.0);
        sampler.SetValue("seed", 12345L);

        return parameters;
    }
}
