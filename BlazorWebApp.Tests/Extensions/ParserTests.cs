using BlazorWebApp.Data.Entities;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;
using Moq;

namespace BlazorWebApp.Tests.Extensions
{
    /// <summary>
    /// Unit tests for Parser extension methods focusing on:
    /// - ExpandWildcardsAsync
    /// - DetectWildcards
    /// - LoRA extraction
    /// - Style parsing
    /// </summary>
    public class ParserTests
    {
        #region Helper Methods

        private Mock<IWildcardService> CreateMockWildcardService()
        {
            var mock = new Mock<IWildcardService>();
            
            // Default: no wildcards exist
            mock.Setup(x => x.CollectionExists(It.IsAny<string>()))
                .ReturnsAsync(false);
            
            mock.Setup(x => x.GetRandomEntryWeighted(It.IsAny<string>()))
                .ReturnsAsync((string?)null);
            
            mock.Setup(x => x.ParseWildcards(It.IsAny<string>()))
                .ReturnsAsync((string input) => input); // Default: no replacement
            
            return mock;
        }

        private List<PromptStyle> CreateTestStyles()
        {
            return new List<PromptStyle>
            {
                new PromptStyle
                {
                    Name = "Quality",
                    Prompt = "masterpiece, best quality",
                    NegativePrompt = "worst quality, low quality"
                }
            };
        }

        #endregion

        #region DetectWildcards Tests

        [Fact]
        public void DetectWildcards_NullInput_ReturnsEmptyList()
        {
            // Act
            var result = Parser.DetectWildcards(null);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void DetectWildcards_EmptyString_ReturnsEmptyList()
        {
            // Act
            var result = Parser.DetectWildcards("");

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void DetectWildcards_NoWildcards_ReturnsEmptyList()
        {
            // Arrange
            var input = "This is a normal prompt without wildcards";

            // Act
            var result = Parser.DetectWildcards(input);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void DetectWildcards_SingleWildcard_ReturnsWildcardName()
        {
            // Arrange
            var input = "A girl wearing __clothing/tops__";

            // Act
            var result = Parser.DetectWildcards(input);

            // Assert
            result.Should().ContainSingle()
                .Which.Should().Be("clothing/tops");
        }

        [Fact]
        public void DetectWildcards_MultipleWildcards_ReturnsAllNames()
        {
            // Arrange
            var input = "A __characters/hair-color__ girl wearing __clothing/tops__ and __clothing/bottoms__ in __locations/indoor__";

            // Act
            var result = Parser.DetectWildcards(input);

            // Assert
            result.Should().HaveCount(4)
                .And.Contain("characters/hair-color")
                .And.Contain("clothing/tops")
                .And.Contain("clothing/bottoms")
                .And.Contain("locations/indoor");
        }

        [Fact]
        public void DetectWildcards_DuplicateWildcards_ReturnsDistinctNames()
        {
            // Arrange
            var input = "__clothing/tops__ and another __clothing/tops__";

            // Act
            var result = Parser.DetectWildcards(input);

            // Assert
            result.Should().ContainSingle()
                .Which.Should().Be("clothing/tops");
        }

        [Fact]
        public void DetectWildcards_WildcardWithoutCategory_DetectsCorrectly()
        {
            // Arrange
            var input = "Test __simple-wildcard__ text";

            // Act
            var result = Parser.DetectWildcards(input);

            // Assert
            result.Should().ContainSingle()
                .Which.Should().Be("simple-wildcard");
        }

        [Fact]
        public void DetectWildcards_AdjacentWildcards_DetectsBoth()
        {
            // Arrange
            var input = "__first__ __second__";

            // Act
            var result = Parser.DetectWildcards(input);

            // Assert
            result.Should().HaveCount(2)
                .And.Contain("first")
                .And.Contain("second");
        }

        [Fact]
        public void DetectWildcards_WildcardsWithSpecialCharacters_DetectsCorrectly()
        {
            // Arrange
            var input = "__test-collection__ and __another_collection__ and __test.collection__";

            // Act
            var result = Parser.DetectWildcards(input);

            // Assert
            result.Should().HaveCount(3)
                .And.Contain("test-collection")
                .And.Contain("another_collection")
                .And.Contain("test.collection");
        }

        [Fact]
        public void DetectWildcards_IncompleteWildcards_DoesNotDetect()
        {
            // Arrange
            var input = "This has _single_ underscores and incomplete __wildcard but no valid ones";

            // Act
            var result = Parser.DetectWildcards(input);

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region ExpandWildcardsAsync Tests

        [Fact]
        public async Task ExpandWildcardsAsync_NullInput_ReturnsEmptyString()
        {
            // Arrange
            var mockService = CreateMockWildcardService();

            // Act
            var result = await Parser.ExpandWildcardsAsync(null, mockService.Object);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task ExpandWildcardsAsync_EmptyString_ReturnsEmptyString()
        {
            // Arrange
            var mockService = CreateMockWildcardService();

            // Act
            var result = await Parser.ExpandWildcardsAsync("", mockService.Object);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task ExpandWildcardsAsync_NoWildcards_ReturnsOriginalText()
        {
            // Arrange
            var mockService = CreateMockWildcardService();
            var input = "Normal prompt without wildcards";
            mockService.Setup(x => x.ParseWildcards(input))
                .ReturnsAsync(input);

            // Act
            var result = await Parser.ExpandWildcardsAsync(input, mockService.Object);

            // Assert
            result.Should().Be(input);
        }

        [Fact]
        public async Task ExpandWildcardsAsync_SingleWildcard_ExpandsCorrectly()
        {
            // Arrange
            var mockService = CreateMockWildcardService();
            var input = "A girl wearing __clothing/tops__";
            var expected = "A girl wearing blue t-shirt";
            
            mockService.Setup(x => x.ParseWildcards(input))
                .ReturnsAsync(expected);

            // Act
            var result = await Parser.ExpandWildcardsAsync(input, mockService.Object);

            // Assert
            result.Should().Be(expected);
            mockService.Verify(x => x.ParseWildcards(input), Times.Once);
        }

        [Fact]
        public async Task ExpandWildcardsAsync_MultipleWildcards_ExpandsAll()
        {
            // Arrange
            var mockService = CreateMockWildcardService();
            var input = "A __characters/hair-color__ girl wearing __clothing/tops__";
            var expected = "A blonde girl wearing blue t-shirt";
            
            mockService.Setup(x => x.ParseWildcards(input))
                .ReturnsAsync(expected);

            // Act
            var result = await Parser.ExpandWildcardsAsync(input, mockService.Object);

            // Assert
            result.Should().Be(expected);
            mockService.Verify(x => x.ParseWildcards(input), Times.Once);
        }

        [Fact]
        public async Task ExpandWildcardsAsync_NonExistentWildcard_LeavesUnchanged()
        {
            // Arrange
            var mockService = CreateMockWildcardService();
            var input = "Test __nonexistent__ wildcard";
            
            mockService.Setup(x => x.ParseWildcards(input))
                .ReturnsAsync(input); // Service leaves it unchanged

            // Act
            var result = await Parser.ExpandWildcardsAsync(input, mockService.Object);

            // Assert
            result.Should().Be(input);
        }

        #endregion

        #region ExtractLorasFromPrompt Tests

        [Fact]
        public void ExtractLorasFromPrompt_NullInput_ReturnsEmptyList()
        {
            // Act
            var result = Parser.ExtractLorasFromPrompt(null, out var cleanedPrompt, false);

            // Assert
            result.Should().BeEmpty();
            cleanedPrompt.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ExtractLorasFromPrompt_EmptyInput_ReturnsEmptyList()
        {
            // Act
            var result = Parser.ExtractLorasFromPrompt("", out var cleanedPrompt, false);

            // Assert
            result.Should().BeEmpty();
            cleanedPrompt.Should().BeEmpty();
        }

        [Fact]
        public void ExtractLorasFromPrompt_NoLoras_ReturnsEmptyList()
        {
            // Arrange
            var input = "A beautiful girl with long hair";

            // Act
            var result = Parser.ExtractLorasFromPrompt(input, out var cleanedPrompt, false);

            // Assert
            result.Should().BeEmpty();
            cleanedPrompt.Should().Be(input);
        }

        [Fact]
        public void ExtractLorasFromPrompt_SingleLora_ExtractsCorrectly()
        {
            // Arrange
            var input = "A girl <lora:detail:0.8>";

            // Act
            var result = Parser.ExtractLorasFromPrompt(input, out var cleanedPrompt, false);

            // Assert
            result.Should().ContainSingle();
            result[0].Name.Should().Be("detail");
            result[0].Strength.Should().BeApproximately(0.8f, 0.01f);
            result[0].IsEnabled.Should().BeTrue();
            result[0].IsNegative.Should().BeFalse();
            cleanedPrompt.Should().Be("A girl");
        }

        [Fact]
        public void ExtractLorasFromPrompt_MultipleLoras_ExtractsAll()
        {
            // Arrange
            var input = "A girl <lora:detail:0.8> wearing <lora:clothing:1.2>";

            // Act
            var result = Parser.ExtractLorasFromPrompt(input, out var cleanedPrompt, false);

            // Assert
            result.Should().HaveCount(2);
            result[0].Name.Should().Be("detail");
            result[1].Name.Should().Be("clothing");
            cleanedPrompt.Should().Be("A girl wearing");
        }

        [Fact]
        public void ExtractLorasFromPrompt_NegativePrompt_SetsIsNegative()
        {
            // Arrange
            var input = "bad quality <lora:fix:0.5>";

            // Act
            var result = Parser.ExtractLorasFromPrompt(input, out var cleanedPrompt, isNegative: true);

            // Assert
            result.Should().ContainSingle();
            result[0].IsNegative.Should().BeTrue();
        }

        #endregion

        #region ParseStyles Tests

        [Fact]
        public void ParseStyles_NullStyles_ReturnsOriginalPrompt()
        {
            // Arrange
            var prompt = "test prompt";

            // Act
            var result = prompt.ParseStyles(null, false);

            // Assert
            result.Should().Be(prompt);
        }

        [Fact]
        public void ParseStyles_EmptyStyles_ReturnsOriginalPrompt()
        {
            // Arrange
            var prompt = "test prompt";

            // Act
            var result = prompt.ParseStyles(new List<PromptStyle>(), false);

            // Assert
            result.Should().Be(prompt);
        }

        [Fact]
        public void ParseStyles_WithPromptPlaceholder_ReplacesCorrectly()
        {
            // Arrange
            var prompt = "test prompt";
            var styles = new List<PromptStyle>
            {
                new PromptStyle { Prompt = "masterpiece, {prompt}, high quality" }
            };

            // Act
            var result = prompt.ParseStyles(styles, false);

            // Assert
            result.Should().Be("masterpiece, test prompt, high quality");
        }

        [Fact]
        public void ParseStyles_WithoutPlaceholder_AppendsStyle()
        {
            // Arrange
            var prompt = "test prompt";
            var styles = new List<PromptStyle>
            {
                new PromptStyle { Prompt = ", masterpiece" }
            };

            // Act
            var result = prompt.ParseStyles(styles, false);

            // Assert
            result.Should().Be("test prompt, masterpiece");
        }

        #endregion

        #region ParseLoras Tests

        [Fact]
        public void ParseLoras_EmptyList_ReturnsEmptyStrings()
        {
            // Arrange
            var loras = new List<Lora>();

            // Act
            var (prompt, negative) = loras.ParseLoras();

            // Assert
            prompt.Should().BeEmpty();
            negative.Should().BeEmpty();
        }

        [Fact]
        public void ParseLoras_EnabledLora_ReturnsLoraString()
        {
            // Arrange
            var loras = new List<Lora>
            {
                new Lora { Name = "test", Strength = 0.8f, IsEnabled = true, IsNegative = false }
            };

            // Act
            var (prompt, negative) = loras.ParseLoras();

            // Assert
            prompt.Should().Contain("<lora:test:0.80>");
            negative.Should().BeEmpty();
        }

        [Fact]
        public void ParseLoras_DisabledLora_IsIgnored()
        {
            // Arrange
            var loras = new List<Lora>
            {
                new Lora { Name = "test", Strength = 0.8f, IsEnabled = false, IsNegative = false }
            };

            // Act
            var (prompt, negative) = loras.ParseLoras();

            // Assert
            prompt.Should().BeEmpty();
            negative.Should().BeEmpty();
        }

        [Fact]
        public void ParseLoras_NegativeLora_GoesToNegativePrompt()
        {
            // Arrange
            var loras = new List<Lora>
            {
                new Lora { Name = "test", Strength = 0.5f, IsEnabled = true, IsNegative = true }
            };

            // Act
            var (prompt, negative) = loras.ParseLoras();

            // Assert
            prompt.Should().BeEmpty();
            negative.Should().Contain("<lora:test:0.50>");
        }

        #endregion
    }
}
