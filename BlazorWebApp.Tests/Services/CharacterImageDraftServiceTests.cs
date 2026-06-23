using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Models;
using BlazorWebApp.Models.CharacterCreator;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BlazorWebApp.Tests.Services;

public class CharacterImageDraftServiceTests
{
    [Fact]
    public void TryParseDraft_ShouldNormalizeAgainstCatalog()
    {
        var service = CreateService(new AppState());
        var catalog = CharacterCreatorCatalog.CreateFallback();
        var source = CharacterReferenceSourceImage.FromBytes([1, 2, 3], "source");

        var parsed = service.TryParseDraft(
            """
            {
              "displayName":"Kira",
              "summary":"A silver-haired scout with a calm smile.",
              "species":"human",
              "archetype":"scout",
              "notes":"Keep the quiet confidence.",
              "regions":[
                {"regionId":"hair","notes":"Loose silver hair.","traits":[{"traitId":"hair.length","value":"long","confidence":1.4}]},
                {"regionId":"missing","traits":[{"traitId":"hair.length","value":"short","confidence":0.8}]},
                {"regionId":"expression","traits":[{"traitId":"hair.length","value":"short","confidence":0.8}]}
              ],
              "warnings":["Hair color is inferred."]
            }
            """,
            catalog,
            source,
            "source",
            out var draft);

        parsed.Should().BeTrue();
        draft.DisplayName.Should().Be("Kira");
        draft.Summary.Should().Be("A silver-haired scout with a calm smile.");
        draft.Species.Should().Be("human");
        draft.Archetype.Should().Be("scout");
        draft.Notes.Should().Be("Keep the quiet confidence.");
        draft.SourceImage.Should().BeSameAs(source);
        draft.Warnings.Should().ContainSingle("Hair color is inferred.");
        draft.Regions.Should().ContainSingle();
        draft.Regions[0].RegionId.Should().Be("hair");
        draft.Regions[0].Traits.Should().ContainSingle().Which.Should().BeEquivalentTo(new CharacterImageDraftTrait
        {
            TraitId = "hair.length",
            Value = "long",
            Confidence = 1
        });
    }

    [Fact]
    public void TryParseDraft_WhenMalformed_ShouldReturnFalse()
    {
        var service = CreateService(new AppState());

        var parsed = service.TryParseDraft("not json", CharacterCreatorCatalog.CreateFallback(), null, null, out var draft);

        parsed.Should().BeFalse();
        draft.HasChanges.Should().BeFalse();
    }

    [Fact]
    public void ApplyDraft_ShouldApplyImageTraitsAndSkipManualLockedTraits()
    {
        var service = CreateService(new AppState());
        var body = CharacterBody.CreateDefault("Kira");
        body.Regions["hair"] = new CharacterRegionState
        {
            RegionId = "hair",
            SelectedTraits =
            {
                ["hair.length"] = new CharacterTraitValue
                {
                    TraitId = "hair.length",
                    Value = "short",
                    Source = CharacterTraitSource.Manual,
                    Locked = true
                }
            }
        };

        var source = CharacterReferenceSourceImage.FromBytes([5, 6, 7], "portrait");
        var draft = new CharacterImageDraft
        {
            Summary = "A calm portrait subject.",
            Species = "human",
            Archetype = "scout",
            Notes = "Visible image-derived notes.",
            SourceImage = source,
            SourceLabel = "portrait",
            Regions =
            [
                new CharacterImageDraftRegion
                {
                    RegionId = "hair",
                    Notes = "Hair frames the face.",
                    Traits = [new CharacterImageDraftTrait { TraitId = "hair.length", Value = "long", Confidence = 0.9 }]
                },
                new CharacterImageDraftRegion
                {
                    RegionId = "expression",
                    Traits = [new CharacterImageDraftTrait { TraitId = "expression.default", Value = "soft_smile", Confidence = 0.7 }]
                }
            ]
        };

        var result = service.ApplyDraft(body, draft);

        result.AppliedTraitCount.Should().Be(1);
        result.SkippedTraitCount.Should().Be(1);
        result.AddedReferenceSheet.Should().BeTrue();
        result.SelectedReferenceSheet.Should().BeTrue();
        body.Identity.Summary.Should().Be("A calm portrait subject.");
        body.Identity.Species.Should().Be("human");
        body.Identity.Archetype.Should().Be("scout");
        body.Notes.Should().Be("Visible image-derived notes.");
        body.Regions["hair"].FreeformNotes.Should().Be("Hair frames the face.");
        body.Regions["hair"].SelectedTraits["hair.length"].Value.Should().Be("short");
        body.Regions["expression"].SelectedTraits["expression.default"].Value.Should().Be("soft_smile");
        body.Regions["expression"].SelectedTraits["expression.default"].Source.Should().Be(CharacterTraitSource.Image);
        body.Regions["expression"].ConfidenceByTrait["expression.default"].Should().BeApproximately(0.7, 0.001);
        body.ReferenceSheets.Should().ContainSingle(sheet => sheet.SourceImage.SourceFingerprint == source.SourceFingerprint);
        body.ActiveReferenceSheetId.Should().Be(body.ReferenceSheets[0].Id);
    }

    [Fact]
    public async Task CreateDraftAsync_ShouldSendMultimodalJsonRequest()
    {
        var appState = new AppState();
        appState.Generation.LLM.Options = new AppStateOllamaOptions
        {
            Seed = 123,
            Temperature = 0.9f,
            TopP = 0.4f,
            NumPredict = 120
        };
        var ollama = new CapturingOllamaService();
        var service = CreateService(appState, ollama);

        var draft = await service.CreateDraftAsync(new CharacterImageDraftRequest
        {
            ModelName = "vision-model",
            ImageBytes = [9, 8, 7],
            SourceLabel = "portrait",
            Catalog = CharacterCreatorCatalog.CreateFallback()
        });

        draft.Should().NotBeNull();
        draft!.Summary.Should().Be("A silver-haired scout with a calm smile.");
        ollama.CapturedModelName.Should().Be("vision-model");
        ollama.CapturedFormat.Should().Be("json");
        ollama.CapturedThink.Should().BeFalse();
        ollama.CapturedOptions.Should().NotBeNull();
        ollama.CapturedOptions!.Seed.Should().BeNull();
        ollama.CapturedOptions.Temperature.Should().BeApproximately(0.6f, 0.001f);
        ollama.CapturedOptions.TopP.Should().BeApproximately(0.8f, 0.001f);
        ollama.CapturedOptions.NumPredict.Should().Be(900);
        ollama.CapturedMessages.Should().NotBeNull();
        ollama.CapturedMessages![0].Content.Should().Contain("structured character identity drafts");
        ollama.CapturedMessages[1].Images.Should().ContainSingle(Convert.ToBase64String(new byte[] { 9, 8, 7 }));
    }

    private static CharacterImageDraftService CreateService(AppState appState, OllamaService? ollama = null)
    {
        var state = new Mock<IStateService>();
        state.SetupGet(service => service.State).Returns(appState);
        return new CharacterImageDraftService(
            ollama ?? new CapturingOllamaService(),
            state.Object,
            NullLogger<CharacterImageDraftService>.Instance);
    }

    private sealed class CapturingOllamaService : OllamaService
    {
        public string? CapturedModelName { get; private set; }
        public List<OllamaChatMessage>? CapturedMessages { get; private set; }
        public OllamaOptions? CapturedOptions { get; private set; }
        public string? CapturedFormat { get; private set; }
        public bool? CapturedThink { get; private set; }

        public CapturingOllamaService()
            : base(
                NullLogger<OllamaService>.Instance,
                new ConfigurationBuilder().Build(),
                Mock.Of<IProgressService>())
        {
        }

        public override Task<OllamaChatResponse?> SendChatMessage(
            string modelName,
            List<OllamaChatMessage> messages,
            OllamaOptions? options = null,
            string? keepAlive = "15m",
            bool stream = false,
            string? format = null,
            bool? think = null)
        {
            CapturedModelName = modelName;
            CapturedMessages = messages;
            CapturedOptions = options;
            CapturedFormat = format;
            CapturedThink = think;
            return Task.FromResult<OllamaChatResponse?>(new OllamaChatResponse
            {
                Message = new OllamaChatMessage
                {
                    Role = "assistant",
                    Content = "{\"summary\":\"A silver-haired scout with a calm smile.\",\"regions\":[{\"regionId\":\"hair\",\"traits\":[{\"traitId\":\"hair.length\",\"value\":\"long\",\"confidence\":0.8}]}]}"
                }
            });
        }
    }
}