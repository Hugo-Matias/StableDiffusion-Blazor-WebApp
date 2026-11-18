using BlazorWebApp.Models;
using BlazorWebApp.Models.CharacterCreator;
using FluentAssertions;
using System.Text.Json;

namespace BlazorWebApp.Tests.Models;

public class CharacterStateTests
{
    [Fact]
    public void AppState_ShouldInitializeCharacterStateWithDefaults()
    {
        var appState = new AppState();

        appState.Character.Should().NotBeNull();
        appState.Character.Engine.Should().Be(CharacterReferenceEngine.Qwen);
        appState.Character.LoaderMode.Should().Be(CharacterLoaderMode.AioCheckpoint);
        appState.Character.GlobalPositivePromptExtension.Should().BeEmpty();
        appState.Character.SelectedCharacterId.Should().BeNull();
        appState.Character.SelectedReferenceSheetId.Should().BeNull();
        appState.Character.SelectedCreatorRegionId.Should().Be("body");
        appState.Character.SelectedPromptProfileId.Should().BeNull();
        appState.Character.SelectedWardrobeId.Should().BeNull();
        appState.Character.PendingSourceImage.Should().BeNull();
        appState.Character.GlobalSeed.Should().Be(-1);
        appState.Character.ShowEngineSettings.Should().BeFalse();
        appState.Character.UseRtxUpscale.Should().BeTrue();
        appState.Character.UseCleanGpu.Should().BeFalse();
        appState.Character.FaceReplacement.Enabled.Should().BeFalse();
        appState.Character.FaceReplacement.UseCloseNeutralReference.Should().BeFalse();
        appState.Character.Assets.Aio.Checkpoint.Should().Be(CharacterReferenceDefaults.AioCheckpoint);
        appState.Character.Assets.Flux.DiffusionModel.Should().Be(CharacterReferenceDefaults.Flux2KleinDiffusionModel);
        appState.Character.Assets.Flux.Clip.Should().Be(CharacterReferenceDefaults.Flux2KleinClip);
        appState.Character.Assets.Flux.Vae.Should().Be(CharacterReferenceDefaults.Flux2KleinVae);
        appState.Character.Slots.Should().HaveCount(15);
    }

    [Fact]
    public void PendingSourceImage_ShouldResolveSourceByImageIdBeforePathOrBytes()
    {
        var pending = new CharacterPendingSourceImage
        {
            ImageId = 17,
            ImagePath = "C:/images/source.png",
            ImageDataUri = "data:image/png;base64,aW1hZ2U=",
            SourceLabel = "Source",
            OriginalFilename = "source.png"
        };

        var source = pending.ToReferenceSourceImage();

        source.ImageId.Should().Be(17);
        source.ImagePath.Should().Be("C:/images/source.png");
        source.SourceFingerprint.Should().Be("image:17");
    }

    [Fact]
    public void CharacterReferenceSheetBody_ShouldApplyPersistedSheetToAppState()
    {
        var sheet = CharacterReferenceSheetBody.Create(CharacterReferenceSourceImage.FromPath("C:/images/source.png", "Source"), "Main Sheet");
        sheet.Engine = CharacterReferenceEngine.Flux2Klein;
        sheet.GlobalPositivePromptExtension = "consistent lighting";
        sheet.Slots[0].LastOutputImageId = 42;
        var state = new AppStateCharacter();

        sheet.ApplyToAppState(state);

        state.SelectedReferenceSheetId.Should().Be(sheet.Id);
        state.Engine.Should().Be(CharacterReferenceEngine.Flux2Klein);
        state.SourceImage.ImagePath.Should().Be("C:/images/source.png");
        state.SourceImage.SourceLabel.Should().Be("Source");
        state.GlobalPositivePromptExtension.Should().Be("consistent lighting");
        state.Slots[0].LastOutputImageId.Should().Be(42);
    }

    [Fact]
    public void CreateDefaultSlots_ShouldUseRenamedExpressionLabels()
    {
        var slots = CharacterReferenceSlotCatalog.CreateDefaultSlots();

        slots.Should().HaveCount(15);
        slots.Select(slot => slot.Label).Should().Contain([
            "Neutral",
            "Happy",
            "Scared",
            "Angry",
            "Sad",
            "Crying",
            "Smug"
        ]);
        slots.Should().NotContain(slot => slot.Label.StartsWith("Close ", StringComparison.OrdinalIgnoreCase));
        var customPresetKeys = new[]
        {
            CharacterReferenceSlotPresetKey.CustomExpression,
            CharacterReferenceSlotPresetKey.CustomPose,
            CharacterReferenceSlotPresetKey.CustomCamera,
            CharacterReferenceSlotPresetKey.CustomBody,
            CharacterReferenceSlotPresetKey.CustomOutfit,
            CharacterReferenceSlotPresetKey.CustomLandscape,
            CharacterReferenceSlotPresetKey.Blank
        };
        slots.Where(slot => slot.PresetKey.HasValue && customPresetKeys.Contains(slot.PresetKey.Value))
            .Should().BeEmpty();
    }

    [Fact]
    public void CreateDefaultSlots_ShouldPreserveSourceDimensionsAndDependencies()
    {
        var slots = CharacterReferenceSlotCatalog.CreateDefaultSlots();

        slots.Single(slot => slot.PresetKey == CharacterReferenceSlotPresetKey.FrontView).Should().BeEquivalentTo(new
        {
            Id = "front-view",
            Label = "Front view",
            Width = 1088,
            Height = 1920,
            Cfg = 1.6,
            DependencyPolicy = CharacterReferenceDependencyPolicy.SourceImage,
            IsBuiltIn = true
        });
        slots.Single(slot => slot.PresetKey == CharacterReferenceSlotPresetKey.NeutralExpression).Should().BeEquivalentTo(new
        {
            Id = "neutral",
            Label = "Neutral",
            Width = 1088,
            Height = 1088,
            Cfg = 1.6,
            DependencyPolicy = CharacterReferenceDependencyPolicy.SourceImage,
            IsBuiltIn = true
        });
        slots.Single(slot => slot.PresetKey == CharacterReferenceSlotPresetKey.HappyExpression)
            .DependencyPolicy.Should().Be(CharacterReferenceDependencyPolicy.NeutralOutput);
        slots.Single(slot => slot.PresetKey == CharacterReferenceSlotPresetKey.ActionPose)
            .DependencyPolicy.Should().Be(CharacterReferenceDependencyPolicy.FrontViewOutput);
    }

    [Fact]
    public void CreateDefaultSlots_ShouldGroupBodyAnglesBeforeExpressions()
    {
        var slots = CharacterReferenceSlotCatalog.CreateDefaultSlots();

        slots.Select(slot => slot.PresetKey).Take(6).Should().Equal([
            CharacterReferenceSlotPresetKey.FrontView,
            CharacterReferenceSlotPresetKey.LeftProfile,
            CharacterReferenceSlotPresetKey.RightProfile,
            CharacterReferenceSlotPresetKey.BackView,
            CharacterReferenceSlotPresetKey.FrontThreeQuarter,
            CharacterReferenceSlotPresetKey.BackThreeQuarter
        ]);
    }

    [Fact]
    public void GetAddablePresets_ShouldExposeCustomPresetChoicesOnly()
    {
        var presets = CharacterReferenceSlotCatalog.GetAddablePresets();

        presets.Select(preset => preset.PresetKey).Should().BeEquivalentTo([
            CharacterReferenceSlotPresetKey.CustomExpression,
            CharacterReferenceSlotPresetKey.CustomPose,
            CharacterReferenceSlotPresetKey.CustomCamera,
            CharacterReferenceSlotPresetKey.CustomBody,
            CharacterReferenceSlotPresetKey.CustomOutfit,
            CharacterReferenceSlotPresetKey.CustomLandscape,
            CharacterReferenceSlotPresetKey.Blank
        ], options => options.WithStrictOrdering());
    }

    [Fact]
    public void OrderSlotsByKind_ShouldMatchCharacterGridGroupingOrder()
    {
        var slots = new[]
        {
            new CharacterReferenceSlotState { Id = "custom", Kind = CharacterReferenceSlotKind.Custom },
            new CharacterReferenceSlotState { Id = "outfit", Kind = CharacterReferenceSlotKind.Outfit },
            new CharacterReferenceSlotState { Id = "expression", Kind = CharacterReferenceSlotKind.Expression },
            new CharacterReferenceSlotState { Id = "body", Kind = CharacterReferenceSlotKind.Body },
            new CharacterReferenceSlotState { Id = "body-angle", Kind = CharacterReferenceSlotKind.BodyAngle },
            new CharacterReferenceSlotState { Id = "landscape", Kind = CharacterReferenceSlotKind.Landscape },
            new CharacterReferenceSlotState { Id = "pose", Kind = CharacterReferenceSlotKind.Pose }
        };

        CharacterReferenceSlotCatalog.OrderSlotsByKind(slots).Select(slot => slot.Id).Should().Equal([
            "body-angle",
            "expression",
            "pose",
            "body",
            "outfit",
            "landscape",
            "custom"
        ]);
    }

    [Fact]
    public void AddSlot_ShouldAppendCustomSlotWithNormalizedLabel()
    {
        var state = new AppStateCharacter();

        var slot = state.AddSlot(CharacterReferenceSlotPresetKey.CustomExpression, "Close Mischievous");

        state.Slots.Should().HaveCount(16);
        slot.IsBuiltIn.Should().BeFalse();
        slot.Label.Should().Be("Mischievous");
        slot.Kind.Should().Be(CharacterReferenceSlotKind.Expression);
        slot.Width.Should().Be(1088);
        slot.Height.Should().Be(1088);
        slot.DependencyPolicy.Should().Be(CharacterReferenceDependencyPolicy.NeutralOutput);
        slot.Id.Should().StartWith("custom-customexpression-");
    }

    [Fact]
    public void AddSlot_ShouldCreateCameraBodyAnglePreset()
    {
        var state = new AppStateCharacter();

        var slot = state.AddSlot(CharacterReferenceSlotPresetKey.CustomCamera);

        slot.Label.Should().Be("Camera");
        slot.Kind.Should().Be(CharacterReferenceSlotKind.BodyAngle);
        slot.Width.Should().Be(1088);
        slot.Height.Should().Be(1920);
        slot.DependencyPolicy.Should().Be(CharacterReferenceDependencyPolicy.FrontViewOutput);
        slot.PromptTemplate.Should().Be("Same exact person and body proportions, same exact detailed artstyle, same exact hairstyle, and outfit. Same exact facial expression and pose.");
        slot.PromptExtension.Should().BeEmpty();
        slot.Id.Should().StartWith("custom-customcamera-");
    }

    [Fact]
    public void AddSlot_ShouldCreateBodyAndOutfitPresetTypes()
    {
        var state = new AppStateCharacter();

        var body = state.AddSlot(CharacterReferenceSlotPresetKey.CustomBody);
        var outfit = state.AddSlot(CharacterReferenceSlotPresetKey.CustomOutfit);

        body.Label.Should().Be("Body");
        body.Kind.Should().Be(CharacterReferenceSlotKind.Body);
        body.Width.Should().Be(1088);
        body.Height.Should().Be(1920);
        body.PromptTemplate.Should().NotBeNullOrWhiteSpace();
        body.PromptExtension.Should().BeEmpty();

        outfit.Label.Should().Be("Outfit");
        outfit.Kind.Should().Be(CharacterReferenceSlotKind.Outfit);
        outfit.Width.Should().Be(1088);
        outfit.Height.Should().Be(1920);
        outfit.PromptTemplate.Should().NotBeNullOrWhiteSpace();
        outfit.PromptExtension.Should().BeEmpty();
    }

    [Fact]
    public void ComposePrompt_ShouldAppendExtensionAfterHiddenTemplate()
    {
        var state = new AppStateCharacter();
        var slot = state.AddSlot(CharacterReferenceSlotPresetKey.CustomCamera);

        slot.PromptExtension = "low-angle camera, dramatic lighting";

        CharacterReferenceSlotCatalog.ComposePrompt(slot).Should()
            .Be("Same exact person and body proportions, same exact detailed artstyle, same exact hairstyle, and outfit. Same exact facial expression and pose. low-angle camera, dramatic lighting");
    }

    [Fact]
    public void ComposePrompt_ShouldAppendGlobalExtensionAfterSlotExtension()
    {
        var state = new AppStateCharacter
        {
            GlobalPositivePromptExtension = "consistent watercolor lighting"
        };
        var slot = state.AddSlot(CharacterReferenceSlotPresetKey.CustomCamera);
        slot.PromptExtension = "low-angle camera, dramatic lighting";

        CharacterReferenceSlotCatalog.ComposePrompt(slot, state.GlobalPositivePromptExtension).Should()
            .Be("Same exact person and body proportions, same exact detailed artstyle, same exact hairstyle, and outfit. Same exact facial expression and pose. low-angle camera, dramatic lighting consistent watercolor lighting");
    }

    [Fact]
    public void ActiveAssets_ShouldExposeOnlySelectedLoaderModeAssets()
    {
        var state = new AppStateCharacter();

        state.LoaderMode = CharacterLoaderMode.AioCheckpoint;
        state.ActiveAssets.Keys.Should().BeEquivalentTo([CharacterAssetKeys.Checkpoint]);
        state.ActiveAssets[CharacterAssetKeys.Checkpoint].Should().Be(CharacterReferenceDefaults.AioCheckpoint);

        state.LoaderMode = CharacterLoaderMode.SplitStack;
        state.ActiveAssets.Keys.Should().BeEquivalentTo([
            CharacterAssetKeys.DiffusionModel,
            CharacterAssetKeys.Clip,
            CharacterAssetKeys.Vae
        ]);
        state.ActiveAssets.Keys.Should().NotContain(CharacterAssetKeys.Checkpoint);
        state.ActiveAssets[CharacterAssetKeys.Clip].Should().Be(CharacterReferenceDefaults.SplitClip);
        state.ActiveAssets[CharacterAssetKeys.Vae].Should().Be(CharacterReferenceDefaults.SplitVae);

        state.Engine = CharacterReferenceEngine.Flux2Klein;
        state.ActiveAssets.Keys.Should().BeEquivalentTo([
            CharacterAssetKeys.DiffusionModel,
            CharacterAssetKeys.Clip,
            CharacterAssetKeys.Vae
        ]);
        state.ActiveAssets[CharacterAssetKeys.DiffusionModel].Should().Be(CharacterReferenceDefaults.Flux2KleinDiffusionModel);
        state.ActiveAssets[CharacterAssetKeys.Clip].Should().Be(CharacterReferenceDefaults.Flux2KleinClip);
        state.ActiveAssets[CharacterAssetKeys.Vae].Should().Be(CharacterReferenceDefaults.Flux2KleinVae);
    }

    [Fact]
    public void AppStateCharacter_ShouldRoundTripThroughJson()
    {
        var state = new AppStateCharacter
        {
            SelectedCharacterId = 5,
            SelectedReferenceSheetId = "sheet-1",
            SelectedCreatorRegionId = "hair",
            SelectedPromptProfileId = "rich_portrait",
            SelectedWardrobeId = "travel",
            PendingSourceImage = new CharacterPendingSourceImage
            {
                ImagePath = "C:/images/pending.png",
                SourceLabel = "Pending"
            },
            Engine = CharacterReferenceEngine.Flux2Klein,
            LoaderMode = CharacterLoaderMode.SplitStack,
            GlobalPositivePromptExtension = "consistent watercolor lighting",
            GlobalSeed = 12345,
            UseCleanGpu = true,
            FaceReplacement = new CharacterFaceReplacementSettings
            {
                Enabled = true,
                UseCloseNeutralReference = true
            }
        };
        state.AddSlot(CharacterReferenceSlotPresetKey.CustomPose, "Close Leap");

        var json = JsonSerializer.Serialize(state);
        var restored = JsonSerializer.Deserialize<AppStateCharacter>(json);

        restored.Should().NotBeNull();
        restored!.SelectedCharacterId.Should().Be(5);
        restored.SelectedReferenceSheetId.Should().Be("sheet-1");
        restored.SelectedCreatorRegionId.Should().Be("hair");
        restored.SelectedPromptProfileId.Should().Be("rich_portrait");
        restored.SelectedWardrobeId.Should().Be("travel");
        restored.PendingSourceImage.Should().NotBeNull();
        restored.PendingSourceImage!.ImagePath.Should().Be("C:/images/pending.png");
        restored.Engine.Should().Be(CharacterReferenceEngine.Flux2Klein);
        restored.LoaderMode.Should().Be(CharacterLoaderMode.SplitStack);
        restored.GlobalPositivePromptExtension.Should().Be("consistent watercolor lighting");
        restored.GlobalSeed.Should().Be(12345);
        restored.UseRtxUpscale.Should().BeTrue();
        restored.UseCleanGpu.Should().BeTrue();
        restored.FaceReplacement.Enabled.Should().BeTrue();
        restored.FaceReplacement.UseCloseNeutralReference.Should().BeTrue();
        restored.Assets.Aio.Checkpoint.Should().Be(CharacterReferenceDefaults.AioCheckpoint);
        restored.Slots.Should().HaveCount(16);
        restored.Slots.Last().Should().BeEquivalentTo(new
        {
            Label = "Leap",
            IsBuiltIn = false,
            Kind = CharacterReferenceSlotKind.Pose,
            Width = 1088,
            Height = 1920,
            DependencyPolicy = CharacterReferenceDependencyPolicy.FrontViewOutput
        });
    }
}
