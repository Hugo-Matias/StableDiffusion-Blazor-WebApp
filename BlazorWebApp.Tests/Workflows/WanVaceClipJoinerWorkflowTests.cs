using System.Text.Json;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Fragments.Wan;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Workflows.Templates.Wan;
using FluentAssertions;
using WfAssetType = BlazorWebApp.Workflows.Models.AssetType;
using WfSourceType = BlazorWebApp.Workflows.Models.SourceType;

namespace BlazorWebApp.Tests.Workflows;

public class WanVaceClipJoinerWorkflowTests
{
    private readonly WanVaceClipJoinerWorkflow _workflow = new();

    [Fact]
    public void Metadata_ShouldUseWanVaceClipJoinerDefaults()
    {
        var metadata = _workflow.Metadata;

        metadata.Title.Should().Be("VACE Clip Joiner");
        metadata.Base.Should().Be(BlazorWebApp.Data.Enums.ModelBase.Wan);
        metadata.Mode.Should().Be(ModeType.Vid2Vid);
        metadata.CompatibleResourceBaseModels.Should().Equal("Wan Video 2.2 T2V-A14B");
        metadata.UsesDualModelLoras.Should().BeFalse();

        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "HighModel" &&
            asset.Type == WfAssetType.DiffusionModel &&
            asset.DefaultValue == "Wan2_2-T2V-A14B_HIGH_fp8_e4m3fn_scaled_KJ.safetensors");
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "LowModel" &&
            asset.Type == WfAssetType.DiffusionModel &&
            asset.DefaultValue == "Wan2_2-T2V-A14B-LOW_fp8_e4m3fn_scaled_KJ.safetensors");
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "HighVaceModule" &&
            asset.Type == WfAssetType.DiffusionModel &&
            asset.DefaultValue == "Wan2_2_Fun_VACE_module_A14B_HIGH_fp8_e4m3fn_scaled_KJ.safetensors");
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "LowVaceModule" &&
            asset.Type == WfAssetType.DiffusionModel &&
            asset.DefaultValue == "Wan2_2_Fun_VACE_module_A14B_LOW_fp8_e4m3fn_scaled_KJ.safetensors");
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "Clip" && asset.Type == WfAssetType.Clip);
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "Vae" && asset.Type == WfAssetType.Vae);
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "HighSpeedLora" &&
            asset.Type == WfAssetType.Lora &&
            asset.DefaultValue == "Speed/wan2.2_t2v_lightx2v_4steps_lora_v1.1_high_noise.safetensors" &&
            asset.ColumnSize == 6);
        metadata.Assets.Should().ContainSingle(asset => asset.Parameter == "LowSpeedLora" &&
            asset.Type == WfAssetType.Lora &&
            asset.DefaultValue == "Speed/wan2.2_t2v_lightx2v_4steps_lora_v1.1_low_noise.safetensors" &&
            asset.ColumnSize == 6);

        metadata.Sources.Should().ContainSingle(source => source.Id == "first_video" && source.Type == WfSourceType.Video && source.Required);
        metadata.Sources.Should().ContainSingle(source => source.Id == "second_video" && source.Type == WfSourceType.Video && source.Required);
    }

    [Fact]
    public void GetFragments_ShouldExposeOnlyCoreControls()
    {
        var fragments = _workflow.GetFragments().Select(fragment => fragment.Metadata).ToList();

        fragments.Select(fragment => fragment.Id).Should().Equal(
            "prompts",
            WanVaceClipJoinerSettingsFragment.FragmentId,
            "sampler_advanced");

        var settings = fragments.Single(fragment => fragment.Id == WanVaceClipJoinerSettingsFragment.FragmentId);
        settings.Component.Should().Be("WanVaceClipJoinerSettingsForm");
        settings.Parameters.Should().Contain(parameter => parameter.Name == WanVaceClipJoinerSettingsFragment.ContextFramesParameter && (int)parameter.DefaultValue! == 8);
        settings.Parameters.Should().Contain(parameter => parameter.Name == WanVaceClipJoinerSettingsFragment.ReplaceFramesParameter && (int)parameter.DefaultValue! == 8);
        settings.Parameters.Should().Contain(parameter => parameter.Name == WanVaceClipJoinerSettingsFragment.NewFramesParameter && (int)parameter.DefaultValue! == 0);
        settings.Parameters.Should().Contain(parameter => parameter.Name == WanVaceClipJoinerSettingsFragment.HighSpeedLoraStrengthParameter && (double)parameter.DefaultValue! == 1);
        settings.Parameters.Should().Contain(parameter => parameter.Name == WanVaceClipJoinerSettingsFragment.LowSpeedLoraStrengthParameter && (double)parameter.DefaultValue! == 1);
    }

    [Fact]
    public void Build_DefaultParameters_ShouldProduceValidJson()
    {
        var result = _workflow.Build(CreateParameters());

        var action = () => JsonDocument.Parse(result.Json);
        action.Should().NotThrow();
    }

    [Fact]
    public void Build_ShouldEmitSourceVideoComponentPath()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters()).Json);

        NodeType(json, "load_video_1").Should().Be("LoadVideo");
        Inputs(json, "load_video_1").GetProperty("file").GetString().Should().Be("first.mp4");
        NodeType(json, "video_1_components").Should().Be("GetVideoComponents");
        InputRef(Inputs(json, "video_1_components"), "video").Should().Be(("load_video_1", 0));

        NodeType(json, "load_video_2").Should().Be("LoadVideo");
        Inputs(json, "load_video_2").GetProperty("file").GetString().Should().Be("second.mp4");
        NodeType(json, "video_2_components").Should().Be("GetVideoComponents");
        InputRef(Inputs(json, "video_2_components"), "video").Should().Be(("load_video_2", 0));
    }

    [Fact]
    public void Build_ShouldEmitDoubleKjModelAndFixedLoraBranches()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters()).Json);

        NodeType(json, "high_vace_module_selector").Should().Be("DiffusionModelSelector");
        Inputs(json, "high_vace_module_selector").GetProperty("model_name").GetString()
            .Should().Be("Wan2_2_Fun_VACE_module_A14B_HIGH_fp8_e4m3fn_scaled_KJ.safetensors");
        NodeType(json, "high_model_loader").Should().Be("DiffusionModelLoaderKJ");
        Inputs(json, "high_model_loader").GetProperty("model_name").GetString()
            .Should().Be("Wan2_2-T2V-A14B_HIGH_fp8_e4m3fn_scaled_KJ.safetensors");
        InputRef(Inputs(json, "high_model_loader"), "extra_state_dict").Should().Be(("high_vace_module_selector", 0));
        NodeType(json, "high_fixed_speed_lora").Should().Be("LoraLoaderModelOnly");
        Inputs(json, "high_fixed_speed_lora").GetProperty("lora_name").GetString()
            .Should().Be("Speed/wan2.2_t2v_lightx2v_4steps_lora_v1.1_high_noise.safetensors");

        NodeType(json, "low_vace_module_selector").Should().Be("DiffusionModelSelector");
        Inputs(json, "low_vace_module_selector").GetProperty("model_name").GetString()
            .Should().Be("Wan2_2_Fun_VACE_module_A14B_LOW_fp8_e4m3fn_scaled_KJ.safetensors");
        NodeType(json, "low_model_loader").Should().Be("DiffusionModelLoaderKJ");
        Inputs(json, "low_model_loader").GetProperty("model_name").GetString()
            .Should().Be("Wan2_2-T2V-A14B-LOW_fp8_e4m3fn_scaled_KJ.safetensors");
        InputRef(Inputs(json, "low_model_loader"), "extra_state_dict").Should().Be(("low_vace_module_selector", 0));
        NodeType(json, "low_fixed_speed_lora").Should().Be("LoraLoaderModelOnly");
        Inputs(json, "low_fixed_speed_lora").GetProperty("lora_name").GetString()
            .Should().Be("Speed/wan2.2_t2v_lightx2v_4steps_lora_v1.1_low_noise.safetensors");
    }

    [Fact]
    public void Build_WhenSpeedLoraAssetsAreSelected_ShouldUseSelectedLoras()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(assets: new Dictionary<string, string>
        {
            ["HighSpeedLora"] = "Speed/custom_high.safetensors",
            ["LowSpeedLora"] = "Speed/custom_low.safetensors"
        })).Json);

        Inputs(json, "high_fixed_speed_lora").GetProperty("lora_name").GetString()
            .Should().Be("Speed/custom_high.safetensors");
        Inputs(json, "low_fixed_speed_lora").GetProperty("lora_name").GetString()
            .Should().Be("Speed/custom_low.safetensors");
    }

    [Fact]
    public void Build_ShouldEmitVacePrepAndConditioningPath()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters()).Json);

        NodeType(json, "vace_prep").Should().Be("WanVACEPrep");
        var prepInputs = Inputs(json, "vace_prep");
        InputRef(prepInputs, "video_1").Should().Be(("video_1_components", 0));
        InputRef(prepInputs, "video_2").Should().Be(("video_2_components", 0));
        prepInputs.GetProperty("context_frames").GetInt32().Should().Be(8);
        prepInputs.GetProperty("replace_frames").GetInt32().Should().Be(8);
        prepInputs.GetProperty("new_frames").GetInt32().Should().Be(0);

        NodeType(json, "wan_vace_to_video").Should().Be("WanVaceToVideo");
        var vaceInputs = Inputs(json, "wan_vace_to_video");
        InputRef(vaceInputs, "control_video").Should().Be(("vace_prep", 0));
        InputRef(vaceInputs, "control_masks").Should().Be(("vace_prep", 1));
        InputRef(vaceInputs, "width").Should().Be(("vace_prep", 2));
        InputRef(vaceInputs, "height").Should().Be(("vace_prep", 3));
        InputRef(vaceInputs, "length").Should().Be(("vace_prep", 4));
    }

    [Fact]
    public void Build_ShouldEmitDualSamplerAndOutputJoinPath()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters()).Json);

        var highInputs = Inputs(json, "sampler_high");
        highInputs.GetProperty("add_noise").GetString().Should().Be("enable");
        highInputs.GetProperty("noise_seed").GetInt64().Should().Be(496342861274979);
        highInputs.GetProperty("steps").GetInt32().Should().Be(10);
        highInputs.GetProperty("cfg").GetDouble().Should().Be(1);
        highInputs.GetProperty("sampler_name").GetString().Should().Be("euler");
        highInputs.GetProperty("scheduler").GetString().Should().Be("simple");
        highInputs.GetProperty("start_at_step").GetInt32().Should().Be(0);
        highInputs.GetProperty("end_at_step").GetInt32().Should().Be(5);
        highInputs.GetProperty("return_with_leftover_noise").GetString().Should().Be("enable");
        InputRef(highInputs, "model").Should().Be(("high_fixed_speed_lora", 0));
        InputRef(highInputs, "latent_image").Should().Be(("wan_vace_to_video", 2));

        var lowInputs = Inputs(json, "sampler_low");
        lowInputs.GetProperty("add_noise").GetString().Should().Be("disable");
        lowInputs.GetProperty("noise_seed").GetInt64().Should().Be(0);
        lowInputs.GetProperty("start_at_step").GetInt32().Should().Be(5);
        lowInputs.GetProperty("end_at_step").GetInt32().Should().Be(10000);
        lowInputs.GetProperty("return_with_leftover_noise").GetString().Should().Be("disable");
        InputRef(lowInputs, "model").Should().Be(("low_fixed_speed_lora", 0));
        InputRef(lowInputs, "latent_image").Should().Be(("sampler_high", 0));

        InputRef(Inputs(json, "batch_start_generated"), "image1").Should().Be(("vace_prep", 5));
        InputRef(Inputs(json, "batch_start_generated"), "image2").Should().Be(("vae_decoder", 0));
        InputRef(Inputs(json, "batch_joined_video"), "image1").Should().Be(("batch_start_generated", 0));
        InputRef(Inputs(json, "batch_joined_video"), "image2").Should().Be(("vace_prep", 6));
        InputRef(Inputs(json, "create_video"), "fps").Should().Be(("video_1_components", 2));
        Inputs(json, "save_video").GetProperty("filename_prefix").GetString().Should().Be("VACE joined");
    }

    [Fact]
    public void Build_WhenSpeedLoraStrengthIsZero_ShouldSkipFixedLoras()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(speedLoraStrength: 0)).Json);

        json.RootElement.TryGetProperty("high_fixed_speed_lora", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("low_fixed_speed_lora", out _).Should().BeFalse();
        InputRef(Inputs(json, "sampler_high"), "model").Should().Be(("high_model_loader", 0));
        InputRef(Inputs(json, "sampler_low"), "model").Should().Be(("low_model_loader", 0));
    }

    private static GenerationParameters CreateParameters(double speedLoraStrength = 1, Dictionary<string, string>? assets = null)
    {
        return new GenerationParameters
        {
            Assets = assets ?? new Dictionary<string, string>(),
            Sources = new Dictionary<string, SourceAsset>
            {
                ["first_video"] = new()
                {
                    Type = "video",
                    Filename = "first.mp4"
                },
                ["second_video"] = new()
                {
                    Type = "video",
                    Filename = "second.mp4"
                }
            },
            Fragments = new Dictionary<string, FragmentParameters>
            {
                ["prompts"] = new()
                {
                    IsActive = true,
                    Values = new Dictionary<string, object?>
                    {
                        ["positive"] = "",
                        ["negative"] = ""
                    }
                },
                [WanVaceClipJoinerSettingsFragment.FragmentId] = new()
                {
                    IsActive = true,
                    Values = new Dictionary<string, object?>
                    {
                        [WanVaceClipJoinerSettingsFragment.ContextFramesParameter] = 8,
                        [WanVaceClipJoinerSettingsFragment.ReplaceFramesParameter] = 8,
                        [WanVaceClipJoinerSettingsFragment.NewFramesParameter] = 0,
                        [WanVaceClipJoinerSettingsFragment.HighSpeedLoraStrengthParameter] = speedLoraStrength,
                        [WanVaceClipJoinerSettingsFragment.LowSpeedLoraStrengthParameter] = speedLoraStrength
                    }
                },
                ["sampler_advanced"] = new()
                {
                    IsActive = true,
                    Values = new Dictionary<string, object?>
                    {
                        ["sampler_name"] = "euler",
                        ["scheduler"] = "simple",
                        ["steps"] = 10,
                        ["cfg"] = 1.0,
                        ["seed"] = 496342861274979L,
                        [WanFunInpaintSamplerFragment.LowSeedParameter] = 0L,
                        ["start_at_step"] = 0,
                        [WanFunInpaintSamplerFragment.SplitAtStepParameter] = 5,
                        ["end_at_step"] = 10000,
                        [WanFunInpaintSamplerFragment.LockEndAtStepsParameter] = false
                    }
                }
            }
        };
    }

    private static string NodeType(JsonDocument json, string nodeId)
    {
        return json.RootElement.GetProperty(nodeId).GetProperty("class_type").GetString()!;
    }

    private static JsonElement Inputs(JsonDocument json, string nodeId)
    {
        return json.RootElement.GetProperty(nodeId).GetProperty("inputs");
    }

    private static (string nodeId, int outputIndex) InputRef(JsonElement inputs, string inputName)
    {
        var input = inputs.GetProperty(inputName);
        return (input[0].GetString()!, input[1].GetInt32());
    }
}