using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Fragments.Qwen;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Workflows.Templates.Qwen;
using FluentAssertions;
using System.Text.Json;

namespace BlazorWebApp.Tests.Workflows;

public class QwenCharacterReferenceWorkflowComposerTests
{
    [Fact]
    public void Build_AioMode_ShouldUseCheckpointLoaderOnly()
    {
        var state = CreateStateWithoutSlots();

        var result = new QwenCharacterReferenceWorkflowComposer().Build(state);
        using var json = Parse(result);

        json.RootElement.TryGetProperty("checkpoint_loader", out var checkpoint).Should().BeTrue();
        checkpoint.GetProperty("class_type").GetString().Should().Be("CheckpointLoaderSimple");
        Inputs(json, "checkpoint_loader").GetProperty("ckpt_name").GetString()
            .Should().Be(CharacterReferenceDefaults.AioCheckpoint);
        json.RootElement.TryGetProperty("unet_loader", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("clip_loader", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("vae_loader", out _).Should().BeFalse();
        result.Workflow.Registry!.GetRef("model_output").Should().Be(("checkpoint_loader", 0));
        result.Workflow.Registry!.GetRef("clip_output").Should().Be(("checkpoint_loader", 1));
        result.Workflow.Registry!.GetRef("vae_output").Should().Be(("checkpoint_loader", 2));
    }

    [Fact]
    public void Build_SplitMode_ShouldUseDiffusionClipAndVaeLoadersOnly()
    {
        var state = CreateStateWithoutSlots();
        state.LoaderMode = CharacterLoaderMode.SplitStack;
        state.Assets.Split.DiffusionModel = "qwen_edit_diffusion.safetensors";

        var result = new QwenCharacterReferenceWorkflowComposer().Build(state);
        using var json = Parse(result);

        json.RootElement.TryGetProperty("checkpoint_loader", out _).Should().BeFalse();
        Inputs(json, "unet_loader").GetProperty("unet_name").GetString().Should().Be("qwen_edit_diffusion.safetensors");
        Inputs(json, "unet_loader").GetProperty("weight_dtype").GetString().Should().Be("default");
        Inputs(json, "clip_loader").GetProperty("clip_name").GetString().Should().Be(CharacterReferenceDefaults.SplitClip);
        Inputs(json, "clip_loader").GetProperty("type").GetString().Should().Be("qwen_image");
        Inputs(json, "vae_loader").GetProperty("vae_name").GetString().Should().Be(CharacterReferenceDefaults.SplitVae);
        result.Workflow.Registry!.GetRef("model_output").Should().Be(("unet_loader", 0));
        result.Workflow.Registry!.GetRef("clip_output").Should().Be(("clip_loader", 0));
        result.Workflow.Registry!.GetRef("vae_output").Should().Be(("vae_loader", 0));
    }

    [Fact]
    public void Build_WithEnabledLora_ShouldChainAfterSelectedLoaderMode()
    {
        var state = CreateStateWithoutSlots();
        state.Loras.Add(new Lora
        {
            Name = "Qwen/custom.safetensors",
            Path = "Qwen/custom.safetensors",
            Strength = 0.75f,
            IsEnabled = true
        });

        var result = new QwenCharacterReferenceWorkflowComposer().Build(state);
        using var json = Parse(result);

        var loraInputs = Inputs(json, "lora_loader_0");
        loraInputs.GetProperty("lora_name").GetString().Should().Be("Qwen/custom.safetensors");
        loraInputs.GetProperty("strength_model").GetDouble().Should().Be(0.75);
        loraInputs.GetProperty("model")[0].GetString().Should().Be("checkpoint_loader");
        loraInputs.GetProperty("clip")[0].GetString().Should().Be("checkpoint_loader");
        result.Workflow.Registry!.GetRef("model_output").Should().Be(("lora_loader_0", 0));
        result.Workflow.Registry!.GetRef("clip_output").Should().Be(("lora_loader_0", 1));
    }

    [Fact]
    public void Build_WithSingleSlot_ShouldEmitQwenShotSubgraphAndSaveOutput()
    {
        var state = CreateStateWithSlots(CharacterReferenceSlotPresetKey.FrontView);
        state.SourceImage.ImagePath = "source.png";
        state.Slots[0].PromptExtension = "front view prompt";

        var result = new QwenCharacterReferenceWorkflowComposer().Build(state);
        using var json = Parse(result);
        var prefix = CharacterReferenceWorkflowIds.SlotNodePrefix("front-view");

        result.Outputs.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            SlotId = "front-view",
            Label = "Front view",
            NodeId = $"{prefix}save",
            FilenamePrefix = "Character/front_view"
        });
        Inputs(json, CharacterReferenceWorkflowIds.SourceImageNodeId).GetProperty("image").GetString().Should().Be("source.png");
        ClassType(json, $"{prefix}sage_attention").Should().Be("PathchSageAttentionKJ");
        ClassType(json, $"{prefix}model_sampling").Should().Be("ModelSamplingAuraFlow");
        ClassType(json, $"{prefix}cfg_norm").Should().Be("CFGNorm");
        ClassType(json, $"{prefix}encode_positive").Should().Be(QwenImageEditPlusProEncodeFragment.NodeClassType);
        ClassType(json, $"{prefix}encode_negative").Should().Be(QwenImageEditPlusProEncodeFragment.NodeClassType);
        ClassType(json, $"{prefix}rtx_upscale").Should().Be("RTXVideoSuperResolution");
        ClassType(json, $"{prefix}save").Should().Be("SaveImage");

        var samplerInputs = Inputs(json, $"{prefix}sampler");
        samplerInputs.GetProperty("seed").GetInt64().Should().BeGreaterThanOrEqualTo(0);
        samplerInputs.GetProperty("steps").GetInt32().Should().Be(4);
        samplerInputs.GetProperty("cfg").GetDouble().Should().Be(1.6);
        samplerInputs.GetProperty("sampler_name").GetString().Should().Be("euler");
        samplerInputs.GetProperty("scheduler").GetString().Should().Be("simple");
        samplerInputs.GetProperty("denoise").GetDouble().Should().Be(1);

        var encodeInputs = Inputs(json, $"{prefix}encode_positive");
        encodeInputs.GetProperty("prompt").GetString().Should().EndWith(" front view prompt");
        encodeInputs.GetProperty("target_size").GetInt32().Should().Be(1024);
        encodeInputs.GetProperty("target_vl_size").GetInt32().Should().Be(384);
        encodeInputs.GetProperty("upscale_method").GetString().Should().Be("lanczos");
        encodeInputs.GetProperty("crop_method").GetString().Should().Be("pad");

        var rtxInputs = Inputs(json, $"{prefix}rtx_upscale");
        rtxInputs.GetProperty("resize_type").GetString().Should().Be("scale by multiplier");
        rtxInputs.GetProperty("resize_type.scale").GetInt32().Should().Be(2);
        rtxInputs.GetProperty("quality").GetString().Should().Be("ULTRA");
        Inputs(json, $"{prefix}save").GetProperty("images")[0].GetString().Should().Be($"{prefix}rtx_upscale");
    }

    [Fact]
    public void Build_WithDependentSlot_ShouldUsePrerequisiteOutputWhenAvailable()
    {
        var state = CreateStateWithSlots(
            CharacterReferenceSlotPresetKey.FrontView,
            CharacterReferenceSlotPresetKey.LeftProfile);

        var result = new QwenCharacterReferenceWorkflowComposer().Build(state);
        using var json = Parse(result);
        var frontPrefix = CharacterReferenceWorkflowIds.SlotNodePrefix("front-view");
        var leftPrefix = CharacterReferenceWorkflowIds.SlotNodePrefix("left-profile");

        result.Outputs.Should().HaveCount(2);
        Inputs(json, $"{leftPrefix}image_scale").GetProperty("image")[0].GetString()
            .Should().Be($"{frontPrefix}rtx_upscale");
    }

    [Fact]
    public void Build_WithCleanGpuEnabled_ShouldSaveFromCleanGpuNode()
    {
        var state = CreateStateWithSlots(CharacterReferenceSlotPresetKey.FrontView);
        state.UseCleanGpu = true;

        var result = new QwenCharacterReferenceWorkflowComposer().Build(state);
        using var json = Parse(result);
        var prefix = CharacterReferenceWorkflowIds.SlotNodePrefix("front-view");

        ClassType(json, $"{prefix}clean_gpu").Should().Be("easy cleanGpuUsed");
        Inputs(json, $"{prefix}clean_gpu").GetProperty("anything")[0].GetString().Should().Be($"{prefix}rtx_upscale");
        Inputs(json, $"{prefix}save").GetProperty("images")[0].GetString().Should().Be($"{prefix}clean_gpu");
    }

    [Fact]
    public void Build_WithSamplerOverrides_ShouldApplyOverridesToSlots()
    {
        var state = CreateStateWithSlots(CharacterReferenceSlotPresetKey.FrontView);
        state.SamplerOverrides.Enabled = true;
        state.SamplerOverrides.Seed = 123;
        state.SamplerOverrides.Steps = 6;
        state.SamplerOverrides.Cfg = 2.2;
        state.SamplerOverrides.SamplerName = "euler";
        state.SamplerOverrides.Scheduler = "simple";
        state.SamplerOverrides.Denoise = 0.9;

        var result = new QwenCharacterReferenceWorkflowComposer().Build(state);
        using var json = Parse(result);
        var samplerInputs = Inputs(json, $"{CharacterReferenceWorkflowIds.SlotNodePrefix("front-view")}sampler");

        samplerInputs.GetProperty("seed").GetInt64().Should().Be(123);
        samplerInputs.GetProperty("steps").GetInt32().Should().Be(6);
        samplerInputs.GetProperty("cfg").GetDouble().Should().Be(2.2);
        samplerInputs.GetProperty("denoise").GetDouble().Should().Be(0.9);
    }

    private static AppStateCharacter CreateStateWithoutSlots()
    {
        var state = new AppStateCharacter();
        state.Slots.Clear();
        return state;
    }

    private static AppStateCharacter CreateStateWithSlots(params CharacterReferenceSlotPresetKey[] presetKeys)
    {
        var defaultSlots = CharacterReferenceSlotCatalog.CreateDefaultSlots();
        var state = CreateStateWithoutSlots();
        foreach (var presetKey in presetKeys)
        {
            state.Slots.Add(defaultSlots.Single(slot => slot.PresetKey == presetKey));
        }

        return state;
    }

    private static JsonDocument Parse(CharacterReferenceWorkflowBuildResult result)
    {
        return JsonDocument.Parse(result.Workflow.Json);
    }

    private static JsonElement Inputs(JsonDocument json, string nodeId)
    {
        return json.RootElement.GetProperty(nodeId).GetProperty("inputs");
    }

    private static string? ClassType(JsonDocument json, string nodeId)
    {
        return json.RootElement.GetProperty(nodeId).GetProperty("class_type").GetString();
    }
}
