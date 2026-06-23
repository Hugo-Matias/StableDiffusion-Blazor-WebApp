using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BlazorWebApp.Tests.Services;

public class CharacterPromptSuggestionServiceTests
{
    [Fact]
    public async Task SuggestAsync_ShouldRequestNovelSuggestionWithExploratoryOptions()
    {
        var appState = new AppState();
        appState.Generation.LLM.Model = "test-model";
        appState.Generation.LLM.Options = new AppStateOllamaOptions
        {
            Seed = 123,
            Temperature = 0.4f,
            TopK = 20,
            TopP = 0.7f,
            MinP = 0.1f,
            NumCtx = 2048,
            NumPredict = 500
        };

        var state = new Mock<IStateService>();
        state.SetupGet(service => service.State).Returns(appState);

        var ollama = new CapturingOllamaService();
        var service = new CharacterPromptSuggestionService(
            ollama,
            state.Object,
            NullLogger<CharacterPromptSuggestionService>.Instance);

        var slot = CharacterReferenceSlotCatalog.CreateDefaultSlots()
            .Single(slot => slot.PresetKey == CharacterReferenceSlotPresetKey.ModelPose);
        slot.Label = "Soft Model Pose";
        slot.PromptExtension = "gentle model stance, relaxed smile";

        var suggestion = await service.SuggestAsync(slot);

        suggestion.Should().BeEquivalentTo(new CharacterPromptSuggestion("Offset Gesture", "one hand lifted near collar, shifted hip balance"));
        ollama.CapturedMessages.Should().NotBeNull();
        ollama.CapturedMessages![0].Content.Should().Contain("Prioritize novelty over polish");
        ollama.CapturedMessages[0].Content.Should().Contain("generate a different idea instead of rephrasing it");
        ollama.CapturedMessages[1].Content.Should().Contain("Creative direction to explore:");
        ollama.CapturedMessages[1].Content.Should().Contain("Treat the current label and current prompt extension as an avoid list");
        ollama.CapturedMessages[1].Content.Should().Contain("gentle model stance, relaxed smile");

        ollama.CapturedOptions.Should().NotBeNull();
        ollama.CapturedOptions!.Seed.Should().BeNull();
        ollama.CapturedOptions.Temperature.Should().BeApproximately(1.25f, 0.001f);
        ollama.CapturedOptions.TopK.Should().Be(80);
        ollama.CapturedOptions.TopP.Should().BeApproximately(0.95f, 0.001f);
        ollama.CapturedOptions.MinP.Should().BeApproximately(0.03f, 0.001f);
        ollama.CapturedOptions.NumCtx.Should().Be(2048);
        ollama.CapturedOptions.NumPredict.Should().Be(500);
    }

    private sealed class CapturingOllamaService : OllamaService
    {
        public List<OllamaChatMessage>? CapturedMessages { get; private set; }
        public OllamaOptions? CapturedOptions { get; private set; }

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

            return Task.FromResult<OllamaChatResponse?>(new OllamaChatResponse
            {
                Message = new OllamaChatMessage
                {
                    Role = "assistant",
                    Content = "{\"label\":\"Offset Gesture\",\"promptExtension\":\"one hand lifted near collar, shifted hip balance\"}"
                }
            });
        }
    }
}
