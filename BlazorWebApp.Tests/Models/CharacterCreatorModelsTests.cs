using BlazorWebApp.Models;
using BlazorWebApp.Models.CharacterCreator;
using FluentAssertions;
using System.Text;
using System.Text.Json;

namespace BlazorWebApp.Tests.Models;

public class CharacterCreatorModelsTests
{
    [Fact]
    public void CharacterBody_CreateDefault_ShouldInitializeIdentityAndPromptProfiles()
    {
        var body = CharacterBody.CreateDefault("Kira");

        body.SchemaVersion.Should().Be(CharacterBody.CurrentSchemaVersion);
        body.Identity.DisplayName.Should().Be("Kira");
        body.ReferenceSheets.Should().BeEmpty();
        body.PromptProfiles.Should().Contain(profile => profile.Id == "identity_plus_wardrobe" && profile.IsDefault);
    }

    [Fact]
    public void CharacterReferenceSheetBody_Create_ShouldUseSourceAndDefaultSlots()
    {
        var source = CharacterReferenceSourceImage.FromImageId(42, "kira.png", "Kira Source");

        var sheet = CharacterReferenceSheetBody.Create(source);

        sheet.Id.Should().StartWith("sheet-");
        sheet.Label.Should().Be("Kira Source");
        sheet.SourceImage.SourceFingerprint.Should().Be("image:42");
        sheet.Engine.Should().Be(CharacterReferenceEngine.Qwen);
        sheet.Slots.Should().HaveCount(15);
        sheet.UseRtxUpscale.Should().BeTrue();
    }

    [Fact]
    public void CharacterReferenceSheetBody_FromAppState_ShouldMirrorDurableReferenceSettings()
    {
        var state = new AppStateCharacter
        {
            Engine = CharacterReferenceEngine.Flux2Klein,
            LoaderMode = CharacterLoaderMode.SplitStack,
            GlobalPositivePromptExtension = "watercolor lighting",
            GlobalSeed = 123,
            UseCleanGpu = true,
            FaceReplacement = new CharacterFaceReplacementSettings { Enabled = true }
        };
        state.AddSlot(CharacterReferenceSlotPresetKey.CustomPose, "Leap");
        var source = CharacterReferenceSourceImage.FromPath("C:/images/kira.png");

        var sheet = CharacterReferenceSheetBody.FromAppState(state, source);

        sheet.Engine.Should().Be(CharacterReferenceEngine.Flux2Klein);
        sheet.LoaderMode.Should().Be(CharacterLoaderMode.SplitStack);
        sheet.GlobalPositivePromptExtension.Should().Be("watercolor lighting");
        sheet.GlobalSeed.Should().Be(123);
        sheet.UseCleanGpu.Should().BeTrue();
        sheet.FaceReplacement.Enabled.Should().BeTrue();
        sheet.Slots.Should().HaveCount(16);
    }

    [Fact]
    public void CharacterSourceFingerprint_ShouldUseStablePrefixes()
    {
        var image = CharacterSourceFingerprint.FromImageId(10);
        var bytes = CharacterSourceFingerprint.FromBytes(Encoding.UTF8.GetBytes("same-image"));
        var path = CharacterSourceFingerprint.FromPath("C:/Images/Character.png");

        image.Should().Be("image:10");
        bytes.Should().StartWith("sha256:");
        path.Should().StartWith("path:");
        path.Should().Contain("character.png");
    }

    [Fact]
    public void CharacterBody_ShouldRoundTripThroughCharacterJsonOptions()
    {
        var body = CharacterBody.CreateDefault("Kira");
        body.ReferenceSheets.Add(CharacterReferenceSheetBody.Create(CharacterReferenceSourceImage.FromImageId(5)));
        body.Regions["hair"] = new CharacterRegionState
        {
            RegionId = "hair",
            SelectedTraits =
            {
                ["hair.length"] = new CharacterTraitValue { TraitId = "hair.length", Value = "long", Locked = true }
            }
        };

        var json = JsonSerializer.Serialize(body, CharacterCreatorJsonOptions.Compact);
        var restored = JsonSerializer.Deserialize<CharacterBody>(json, CharacterCreatorJsonOptions.Compact);

        restored.Should().NotBeNull();
        restored!.Identity.DisplayName.Should().Be("Kira");
        restored.ReferenceSheets.Should().ContainSingle().Which.SourceImage.SourceFingerprint.Should().Be("image:5");
        restored.Regions["hair"].SelectedTraits["hair.length"].Locked.Should().BeTrue();
    }
}