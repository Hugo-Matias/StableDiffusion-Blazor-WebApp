using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Workflows.Templates.Flux;
using FluentAssertions;
using System.Text.Json;

namespace BlazorWebApp.Tests.Workflows;

public class Flux2KleinCharacterReferenceWorkflowComposerTests
{
    [Fact]
    public void Build_ShouldUseFlux2KleinLoaderDefaults()
    {
        var state = CreateStateWithoutSlots();

        var result = new Flux2KleinCharacterReferenceWorkflowComposer().Build(state);
        using var json = Parse(result);

        ClassType(json, "unet_loader").Should().Be("UNETLoader");
        ClassType(json, "clip_loader").Should().Be("CLIPLoader");
        ClassType(json, "vae_loader").Should().Be("VAELoader");
        Inputs(json, "unet_loader").GetProperty("unet_name").GetString()
            .Should().Be(CharacterReferenceDefaults.Flux2KleinDiffusionModel);
        Inputs(json, "clip_loader").GetProperty("clip_name").GetString()
            .Should().Be(CharacterReferenceDefaults.Flux2KleinClip);
        Inputs(json, "clip_loader").GetProperty("type").GetString().Should().Be("flux2");
        Inputs(json, "vae_loader").GetProperty("vae_name").GetString()
            .Should().Be(CharacterReferenceDefaults.Flux2KleinVae);
        result.Workflow.Registry!.GetRef("model_output").Should().Be(("unet_loader", 0));
        result.Workflow.Registry!.GetRef("clip_output").Should().Be(("clip_loader", 0));
        result.Workflow.Registry!.GetRef("vae_output").Should().Be(("vae_loader", 0));
    }

    [Fact]
    public void Build_WithSingleSlot_ShouldEmitFluxI2IReferenceSubgraphAndSaveOutput()
    {
        var state = CreateStateWithSlots(CharacterReferenceSlotPresetKey.FrontView);
        state.GlobalPositivePromptExtension = "global positive extension";
        state.Slots[0].PromptExtension = "front view prompt";

        var result = new Flux2KleinCharacterReferenceWorkflowComposer().Build(state);
        using var json = Parse(result);
        var prefix = CharacterReferenceWorkflowIds.SlotNodePrefix("front-view");

        result.Outputs.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            SlotId = "front-view",
            Label = "Front view",
            NodeId = $"{prefix}save",
            FilenamePrefix = "Character/front_view"
        });
        ClassType(json, $"{prefix}positive_encode").Should().Be("CLIPTextEncode");
        ClassType(json, $"{prefix}negative_encode").Should().Be("CLIPTextEncode");
        ClassType(json, $"{prefix}reference_scale").Should().Be("ImageScaleToTotalPixels");
        ClassType(json, $"{prefix}reference_vae_encode").Should().Be("VAEEncode");
        ClassType(json, $"{prefix}reference_positive").Should().Be("ReferenceLatent");
        ClassType(json, $"{prefix}reference_negative").Should().Be("ReferenceLatent");
        ClassType(json, $"{prefix}empty_latent").Should().Be("EmptyFlux2LatentImage");
        ClassType(json, $"{prefix}flux2_scheduler").Should().Be("Flux2Scheduler");
        ClassType(json, $"{prefix}sampler").Should().Be("SamplerCustomAdvanced");
        ClassType(json, $"{prefix}vae_decode").Should().Be("VAEDecode");
        ClassType(json, $"{prefix}save").Should().Be("SaveImage");

        Inputs(json, $"{prefix}positive_encode").GetProperty("text").GetString()
            .Should().EndWith(" front view prompt global positive extension");
        Inputs(json, $"{prefix}reference_scale").GetProperty("image")[0].GetString()
            .Should().Be(CharacterReferenceWorkflowIds.SourceImageNodeId);
        Inputs(json, $"{prefix}reference_scale").GetProperty("upscale_method").GetString().Should().Be("lanczos");
        Inputs(json, $"{prefix}reference_scale").GetProperty("megapixels").GetDouble().Should().Be(1.0);
        Inputs(json, $"{prefix}noise").GetProperty("noise_seed").GetInt64().Should().BeGreaterThanOrEqualTo(0);
        Inputs(json, $"{prefix}empty_latent").GetProperty("width")[0].GetString().Should().Be($"{prefix}reference_size");
        Inputs(json, $"{prefix}flux2_scheduler").GetProperty("width")[0].GetString().Should().Be($"{prefix}reference_size");
        Inputs(json, $"{prefix}save").GetProperty("images")[0].GetString().Should().Be($"{prefix}vae_decode");
    }

    [Fact]
    public void Build_WithDependentSlot_ShouldUsePrerequisiteOutputWhenAvailable()
    {
        var state = CreateStateWithSlots(
            CharacterReferenceSlotPresetKey.FrontView,
            CharacterReferenceSlotPresetKey.LeftProfile);

        var result = new Flux2KleinCharacterReferenceWorkflowComposer().Build(state);
        using var json = Parse(result);
        var frontPrefix = CharacterReferenceWorkflowIds.SlotNodePrefix("front-view");
        var leftPrefix = CharacterReferenceWorkflowIds.SlotNodePrefix("left-profile");

        result.Outputs.Should().HaveCount(2);
        Inputs(json, $"{leftPrefix}reference_scale").GetProperty("image")[0].GetString()
            .Should().Be($"{frontPrefix}vae_decode");
    }

    [Fact]
    public void Build_WithReusableDependencyImage_ShouldLoadItAndUseItAsPrerequisiteOutput()
    {
        var state = CreateStateWithSlots(CharacterReferenceSlotPresetKey.LeftProfile);
        state.ReusableDependencyImagePaths[CharacterReferenceWorkflowIds.FrontViewSlotId] = "uploaded-front.png";

        var result = new Flux2KleinCharacterReferenceWorkflowComposer().Build(state);
        using var json = Parse(result);
        var dependencyNodeId = CharacterReferenceWorkflowIds.DependencyImageNodeId(CharacterReferenceWorkflowIds.FrontViewSlotId);
        var leftPrefix = CharacterReferenceWorkflowIds.SlotNodePrefix("left-profile");

        result.Outputs.Should().ContainSingle().Which.SlotId.Should().Be("left-profile");
        ClassType(json, dependencyNodeId).Should().Be("LoadImage");
        Inputs(json, dependencyNodeId).GetProperty("image").GetString().Should().Be("uploaded-front.png");
        Inputs(json, $"{leftPrefix}reference_scale").GetProperty("image")[0].GetString()
            .Should().Be(dependencyNodeId);
        json.RootElement.TryGetProperty($"{CharacterReferenceWorkflowIds.SlotNodePrefix("front-view")}save", out _)
            .Should().BeFalse();
    }

    [Fact]
    public void Build_WithSamplerOverrides_ShouldApplyOverridesToFluxSamplerNodes()
    {
        var state = CreateStateWithSlots(CharacterReferenceSlotPresetKey.FrontView);
        state.GlobalSeed = 123;
        state.SamplerOverrides.Enabled = true;
        state.SamplerOverrides.Steps = 8;
        state.SamplerOverrides.Cfg = 2.4;
        state.SamplerOverrides.SamplerName = "er_sde";

        var result = new Flux2KleinCharacterReferenceWorkflowComposer().Build(state);
        using var json = Parse(result);
        var prefix = CharacterReferenceWorkflowIds.SlotNodePrefix("front-view");

        Inputs(json, $"{prefix}noise").GetProperty("noise_seed").GetInt64().Should().Be(123);
        Inputs(json, $"{prefix}flux2_scheduler").GetProperty("steps").GetInt32().Should().Be(8);
        Inputs(json, $"{prefix}cfg_guider").GetProperty("cfg").GetDouble().Should().Be(2.4);
        Inputs(json, $"{prefix}sampler_select").GetProperty("sampler_name").GetString().Should().Be("er_sde");
    }

    [Fact]
    public void Build_WithGlobalSeedOnly_ShouldApplyNoiseSeedWithoutSamplerOverrides()
    {
        var state = CreateStateWithSlots(CharacterReferenceSlotPresetKey.FrontView);
        state.GlobalSeed = 456;

        var result = new Flux2KleinCharacterReferenceWorkflowComposer().Build(state);
        using var json = Parse(result);
        var prefix = CharacterReferenceWorkflowIds.SlotNodePrefix("front-view");

        Inputs(json, $"{prefix}noise").GetProperty("noise_seed").GetInt64().Should().Be(456);
        Inputs(json, $"{prefix}flux2_scheduler").GetProperty("steps").GetInt32().Should().Be(4);
        Inputs(json, $"{prefix}cfg_guider").GetProperty("cfg").GetDouble().Should().Be(1.6);
        Inputs(json, $"{prefix}sampler_select").GetProperty("sampler_name").GetString().Should().Be("euler");
    }

    private static AppStateCharacter CreateStateWithoutSlots()
    {
        var state = new AppStateCharacter
        {
            Engine = CharacterReferenceEngine.Flux2Klein
        };
        state.Slots.Clear();
        state.SourceImage.ImagePath = "source.png";
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