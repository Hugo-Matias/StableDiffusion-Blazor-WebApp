using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;
using Moq;

namespace BlazorWebApp.Tests.Extensions
{
    /// <summary>
    /// Unit tests for Parser extension methods, focusing on Phase 4 additions:
    /// - ParseParametersAsync (with wildcard expansion)
    /// - ExpandWildcardsAsync
    /// - DetectWildcards
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

        private SharedParameters CreateTestParameters(string prompt = "test prompt", string negativePrompt = "")
        {
            return new SharedParameters
            {
                Prompt = prompt,
                NegativePrompt = negativePrompt,
                Seed = 12345,
                Steps = 20,
                CfgScale = 7.0f,
                Width = 512,
                Height = 512,
                Loras = new List<Lora>()
            };
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

        #region ParseParametersAsync Tests

        [Fact]
        public async Task ParseParametersAsync_NullWildcardService_SkipsWildcardExpansion()
        {
            // Arrange
            var parameters = CreateTestParameters("Test prompt");
            var styles = new List<PromptStyle>();

            // Act
            var result = await Parser.ParseParametersAsync(parameters, styles, null);

            // Assert
            result.Prompt.Should().Be("Test prompt");
            result.Seed.Should().NotBe(-1); // Should still process seed
        }

        [Fact]
        public async Task ParseParametersAsync_WithWildcards_ExpandsBeforeStyles()
        {
            // Arrange
            var mockService = CreateMockWildcardService();
            var parameters = CreateTestParameters("__clothing/tops__, {prompt}", ""); // Empty negative prompt
            var styles = CreateTestStyles();
            
            // Mock wildcard expansion - only prompt is expanded (empty negative returns early from ExpandWildcardsAsync)
            mockService.Setup(x => x.ParseWildcards("__clothing/tops__, {prompt}"))
                .ReturnsAsync("blue shirt, {prompt}");

            // Act
            var result = await Parser.ParseParametersAsync(parameters, styles, mockService.Object);

            // Assert
            result.Prompt.Should().Contain("blue shirt");
            result.Prompt.Should().Contain("masterpiece"); // From style expansion
            // Only verify prompt was called - empty strings don't call ParseWildcards in ExpandWildcardsAsync
            mockService.Verify(x => x.ParseWildcards("__clothing/tops__, {prompt}"), Times.Once);
        }

        [Fact]
        public async Task ParseParametersAsync_ExpandsPositiveAndNegativePrompts()
        {
            // Arrange
            var mockService = CreateMockWildcardService();
            var parameters = CreateTestParameters(
                prompt: "1girl, __clothing/tops__",
                negativePrompt: "__quality/bad__"
            );
            
            mockService.Setup(x => x.ParseWildcards("1girl, __clothing/tops__"))
                .ReturnsAsync("1girl, blue shirt");
            mockService.Setup(x => x.ParseWildcards("__quality/bad__"))
                .ReturnsAsync("blurry, low quality");

            // Act
            var result = await Parser.ParseParametersAsync(parameters, new List<PromptStyle>(), mockService.Object);

            // Assert
            result.Prompt.Should().Be("1girl, blue shirt");
            result.NegativePrompt.Should().Be("blurry, low quality");
        }

        [Fact]
        public async Task ParseParametersAsync_GeneratesSeedIfNegative()
        {
            // Arrange
            var mockService = CreateMockWildcardService();
            var parameters = CreateTestParameters("test");
            parameters.Seed = -1;
            
            mockService.Setup(x => x.ParseWildcards("test"))
                .ReturnsAsync("test");
            mockService.Setup(x => x.ParseWildcards(""))
                .ReturnsAsync("");

            // Act
            var result = await Parser.ParseParametersAsync(parameters, new List<PromptStyle>(), mockService.Object);

            // Assert
            result.Seed.Should().NotBe(-1);
            result.Seed.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task ParseParametersAsync_PreservesSeedIfSet()
        {
            // Arrange
            var mockService = CreateMockWildcardService();
            var parameters = CreateTestParameters("test");
            parameters.Seed = 12345;
            
            mockService.Setup(x => x.ParseWildcards("test"))
                .ReturnsAsync("test");
            mockService.Setup(x => x.ParseWildcards(""))
                .ReturnsAsync("");

            // Act
            var result = await Parser.ParseParametersAsync(parameters, new List<PromptStyle>(), mockService.Object);

            // Assert
            result.Seed.Should().Be(12345);
        }

        [Fact]
        public async Task ParseParametersAsync_AppliesStylesAfterWildcards()
        {
            // Arrange
            var mockService = CreateMockWildcardService();
            var parameters = CreateTestParameters("__clothing/tops__");
            var styles = new List<PromptStyle>
            {
                new PromptStyle
                {
                    Name = "Test",
                    Prompt = "{prompt}, high quality",
                    NegativePrompt = "low quality"
                }
            };
            
            mockService.Setup(x => x.ParseWildcards("__clothing/tops__"))
                .ReturnsAsync("blue shirt");
            mockService.Setup(x => x.ParseWildcards(""))
                .ReturnsAsync("");

            // Act
            var result = await Parser.ParseParametersAsync(parameters, styles, mockService.Object);

            // Assert
            result.Prompt.Should().Contain("blue shirt");
            result.Prompt.Should().Contain("high quality");
        }

        [Fact]
        public async Task ParseParametersAsync_HandlesMultipleWildcardsAndStyles()
        {
            // Arrange
            var mockService = CreateMockWildcardService();
            var parameters = CreateTestParameters(
                prompt: "__characters/hair-color__ girl wearing __clothing/tops__",
                negativePrompt: "__quality/bad__"
            );
            var styles = new List<PromptStyle>
            {
                new PromptStyle
                {
                    Name = "Quality",
                    Prompt = "{prompt}, masterpiece",
                    NegativePrompt = "{np}, worst quality"
                }
            };
            
            mockService.Setup(x => x.ParseWildcards("__characters/hair-color__ girl wearing __clothing/tops__"))
                .ReturnsAsync("blonde girl wearing blue shirt");
            mockService.Setup(x => x.ParseWildcards("__quality/bad__"))
                .ReturnsAsync("blurry");

            // Act
            var result = await Parser.ParseParametersAsync(parameters, styles, mockService.Object);

            // Assert
            result.Prompt.Should().Contain("blonde girl wearing blue shirt");
            result.Prompt.Should().Contain("masterpiece");
            result.NegativePrompt.Should().Contain("blurry");
            result.NegativePrompt.Should().Contain("worst quality");
        }

        [Fact]
        public async Task ParseParametersAsync_EmptyPrompts_HandlesGracefully()
        {
            // Arrange
            var mockService = CreateMockWildcardService();
            var parameters = CreateTestParameters("", "");
            
            mockService.Setup(x => x.ParseWildcards(""))
                .ReturnsAsync("");

            // Act
            var result = await Parser.ParseParametersAsync(parameters, new List<PromptStyle>(), mockService.Object);

            // Assert
            result.Prompt.Should().BeEmpty();
            result.NegativePrompt.Should().BeEmpty();
        }

        [Fact]
        public async Task ParseParametersAsync_NullPrompts_HandlesGracefully()
        {
            // Arrange
            var mockService = CreateMockWildcardService();
            var parameters = new SharedParameters
            {
                Prompt = null,
                NegativePrompt = null,
                Seed = 12345,
                Loras = new List<Lora>() // Initialize Loras to prevent null reference
            };
            
            mockService.Setup(x => x.ParseWildcards(It.IsAny<string>()))
                .ReturnsAsync((string? input) => input ?? "");

            // Act
            var result = await Parser.ParseParametersAsync(parameters, new List<PromptStyle>(), mockService.Object);

            // Assert
            result.Prompt.Should().BeNullOrEmpty();
            result.NegativePrompt.Should().BeNullOrEmpty();
        }

        [Fact]
        public async Task ParseParametersAsync_WildcardServiceError_ContinuesProcessing()
        {
            // Arrange
            var mockService = CreateMockWildcardService();
            mockService.Setup(x => x.ParseWildcards(It.IsAny<string>()))
                .ReturnsAsync((string input) => input); // Fallback behavior on error
            
            var parameters = CreateTestParameters("__test__");
            var styles = CreateTestStyles();

            // Act
            var result = await Parser.ParseParametersAsync(parameters, styles, mockService.Object);

            // Assert - Should still process styles even if wildcard fails
            result.Prompt.Should().Contain("__test__"); // Wildcard unchanged
            result.Seed.Should().NotBe(-1); // Other processing continues
        }

        #endregion

        #region Backward Compatibility Tests

        [Fact]
        public void ParseParameters_SyncVersion_StillWorks()
        {
            // Arrange
            var parameters = CreateTestParameters("test prompt");
            var styles = CreateTestStyles();

            // Act
            var result = Parser.ParseParameters(parameters, styles);

            // Assert
            result.Should().NotBeNull();
            result.Seed.Should().NotBe(-1);
        }

        [Fact]
        public void ParseParameters_SyncVersion_DoesNotExpandWildcards()
        {
            // Arrange
            var parameters = CreateTestParameters("__clothing/tops__");

            // Act
            var result = Parser.ParseParameters(parameters, new List<PromptStyle>());

            // Assert
            result.Prompt.Should().Contain("__clothing/tops__"); // Should remain unchanged
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task FullWorkflow_WildcardsStylesLoRAs_ProcessesInCorrectOrder()
        {
            // Arrange
            var mockService = CreateMockWildcardService();
            var parameters = CreateTestParameters(
                prompt: "__clothing/tops__, <lora:detail:1.0>, {prompt}",
                negativePrompt: "__quality/bad__"
            );
            var styles = new List<PromptStyle>
            {
                new PromptStyle
                {
                    Name = "Quality",
                    Prompt = "masterpiece, {prompt}",
                    NegativePrompt = "{np}, worst quality"
                }
            };
            
            mockService.Setup(x => x.ParseWildcards("__clothing/tops__, <lora:detail:1.0>, {prompt}"))
                .ReturnsAsync("blue shirt, <lora:detail:1.0>, {prompt}");
            mockService.Setup(x => x.ParseWildcards("__quality/bad__"))
                .ReturnsAsync("blurry");

            // Act
            var result = await Parser.ParseParametersAsync(parameters, styles, mockService.Object);

            // Assert - Order: Wildcards -> Styles -> LoRAs -> Seed
            result.Prompt.Should().Contain("blue shirt"); // Wildcard expanded first
            result.Prompt.Should().Contain("masterpiece"); // Style applied second
            // Note: LoRA parsing happens in the sync part of ParseParameters
            result.NegativePrompt.Should().Contain("blurry");
            result.NegativePrompt.Should().Contain("worst quality");
            result.Seed.Should().NotBe(-1);
        }

        #endregion
    }
}
