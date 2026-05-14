using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Models;
using BlazorWebApp.Models.CharacterCreator;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BlazorWebApp.Tests.Services;

public class CharacterLlmAuthoringServiceTests
{
    [Fact]
    public void TryParseSuggestion_ShouldReturnStructuredSuggestion()
    {
        var service = CreateService(new AppState());

        var parsed = service.TryParseSuggestion(
            """
            {"label":"Polished Scout","summary":"A nimble scout with calm confidence.","regionId":"hair","regionNotes":"Long wind-tossed silver hair.","traitUpdates":[{"regionId":"eyes","traitId":"eyes.color","value":"emerald eyes","locked":false}],"warnings":["Review eye color."]}
            """,
            CharacterLlmAuthoringOperation.ExpandSummary,
            out var suggestion);

        parsed.Should().BeTrue();
        suggestion.Label.Should().Be("Polished Scout");
        suggestion.Summary.Should().Be("A nimble scout with calm confidence.");
        suggestion.RegionId.Should().Be("hair");
        suggestion.RegionNotes.Should().Be("Long wind-tossed silver hair.");
        suggestion.TraitUpdates.Should().ContainSingle().Which.Should().BeEquivalentTo(new CharacterTraitAssignment
        {
            RegionId = "eyes",
            TraitId = "eyes.color",
            Value = "emerald eyes",
            Locked = false
        });
        suggestion.Warnings.Should().ContainSingle("Review eye color.");
    }

    [Fact]
    public void TryParseSuggestion_WhenMalformed_ShouldReturnFalse()
    {
        var service = CreateService(new AppState());

        var parsed = service.TryParseSuggestion("not json", CharacterLlmAuthoringOperation.FillMissing, out var suggestion);

        parsed.Should().BeFalse();
        suggestion.HasChanges.Should().BeFalse();
    }

    [Fact]
    public void ApplySuggestion_ShouldSkipExistingManualOrLockedTraits()
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
        var suggestion = new CharacterLlmAuthoringSuggestion
        {
            Summary = "A sharper character summary.",
            RegionId = "hair",
            RegionNotes = "Hair is carefully shaped around the face.",
            TraitUpdates =
            [
                new CharacterTraitAssignment { RegionId = "hair", TraitId = "hair.length", Value = "long" },
                new CharacterTraitAssignment { RegionId = "eyes", TraitId = "eyes.color", Value = "emerald eyes" }
            ]
        };

        var result = service.ApplySuggestion(body, suggestion);

        result.AppliedTraitCount.Should().Be(1);
        result.SkippedTraitCount.Should().Be(1);
        body.Identity.Summary.Should().Be("A sharper character summary.");
        body.Regions["hair"].FreeformNotes.Should().Be("Hair is carefully shaped around the face.");
        body.Regions["hair"].SelectedTraits["hair.length"].Value.Should().Be("short");
        body.Regions["eyes"].SelectedTraits["eyes.color"].Value.Should().Be("emerald eyes");
        body.Regions["eyes"].SelectedTraits["eyes.color"].Source.Should().Be(CharacterTraitSource.Llm);
    }

    [Fact]
    public async Task ExpandSummaryAsync_ShouldUseJsonModeAndSharedLlmOptions()
    {
        var appState = new AppState();
        appState.Generation.LLM.Model = "test-model";
        appState.Generation.LLM.Options = new AppStateOllamaOptions
        {
            Seed = 42,
            Temperature = 0.2f,
            TopP = 0.4f,
            NumPredict = 200
        };
        var ollama = new CapturingOllamaService();
        var service = CreateService(appState, ollama);

        var suggestion = await service.ExpandSummaryAsync(CharacterBody.CreateDefault("Kira"), CharacterCreatorCatalog.CreateFallback(), null);

        suggestion.Should().NotBeNull();
        suggestion!.Summary.Should().Be("A nimble scout with calm confidence.");
        ollama.CapturedFormat.Should().Be("json");
        ollama.CapturedThink.Should().BeFalse();
        ollama.CapturedOptions.Should().NotBeNull();
        ollama.CapturedOptions!.Seed.Should().BeNull();
        ollama.CapturedOptions.Temperature.Should().BeApproximately(0.7f, 0.001f);
        ollama.CapturedOptions.TopP.Should().BeApproximately(0.9f, 0.001f);
        ollama.CapturedOptions.NumPredict.Should().Be(700);
        ollama.CapturedMessages.Should().NotBeNull();
        ollama.CapturedMessages![0].Content.Should().Contain("structured Character Creator editor");
        ollama.CapturedMessages[1].Content.Should().Contain("Polish or expand");
    }

    private static CharacterLlmAuthoringService CreateService(AppState appState, OllamaService? ollama = null)
    {
        var state = new Mock<IStateService>();
        state.SetupGet(service => service.State).Returns(appState);
        return new CharacterLlmAuthoringService(
            ollama ?? new CapturingOllamaService(),
            state.Object,
            NullLogger<CharacterLlmAuthoringService>.Instance);
    }

    private sealed class CapturingOllamaService : OllamaService
    {
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
            CapturedMessages = messages;
            CapturedOptions = options;
            CapturedFormat = format;
            CapturedThink = think;
            return Task.FromResult<OllamaChatResponse?>(new OllamaChatResponse
            {
                Message = new OllamaChatMessage
                {
                    Role = "assistant",
                    Content = "{\"label\":\"Polished Scout\",\"summary\":\"A nimble scout with calm confidence.\"}"
                }
            });
        }
    }
}