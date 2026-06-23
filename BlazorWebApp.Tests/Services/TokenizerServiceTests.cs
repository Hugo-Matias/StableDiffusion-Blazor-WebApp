using BlazorWebApp.Services;
using Xunit;

namespace BlazorWebApp.Tests.Services
{
    public class TokenizerServiceTests
    {
        private readonly ITokenizerService _tokenizerService;

        public TokenizerServiceTests()
        {
            _tokenizerService = new TokenizerService();
        }

        [Fact]
        public async Task CountTokensAsync_EmptyString_ReturnsZero()
        {
            // Arrange
            var text = string.Empty;

            // Act
            var result = await _tokenizerService.CountTokensAsync(text);

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task CountTokensAsync_NullString_ReturnsZero()
        {
            // Arrange
            string? text = null;

            // Act
            var result = await _tokenizerService.CountTokensAsync(text!);

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task CountTokensAsync_WhitespaceString_ReturnsZero()
        {
            // Arrange
            var text = "   \t\n  ";

            // Act
            var result = await _tokenizerService.CountTokensAsync(text);

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task CountTokensAsync_SimpleText_ReturnsPositiveCount()
        {
            // Arrange
            var text = "Hello, world!";

            // Act
            var result = await _tokenizerService.CountTokensAsync(text);

            // Assert
            Assert.True(result > 0);
            // "Hello, world!" typically tokenizes to 4 tokens: ["Hello", ",", " world", "!"]
            Assert.InRange(result, 3, 5);
        }

        [Fact]
        public async Task CountTokensAsync_LongText_ReturnsReasonableCount()
        {
            // Arrange
            var text = "A beautiful sunset over the ocean with vibrant colors painting the sky";

            // Act
            var result = await _tokenizerService.CountTokensAsync(text);

            // Assert
            Assert.True(result > 0);
            // Should be roughly 15-20 tokens
            Assert.InRange(result, 10, 25);
        }

        [Fact]
        public async Task TokenizeTextAsync_EmptyString_ReturnsEmptyList()
        {
            // Arrange
            var text = string.Empty;

            // Act
            var result = await _tokenizerService.TokenizeTextAsync(text);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task TokenizeTextAsync_SimpleText_ReturnsTokens()
        {
            // Arrange
            var text = "Hello world";

            // Act
            var result = await _tokenizerService.TokenizeTextAsync(text);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.True(result.Count > 0);
        }

        [Fact]
        public async Task TokenizeTextAsync_Tokens_ConcatenateToOriginal()
        {
            // Arrange
            var text = "The quick brown fox";

            // Act
            var tokens = await _tokenizerService.TokenizeTextAsync(text);
            var reconstructed = string.Concat(tokens);

            // Assert
            Assert.Equal(text, reconstructed);
        }

        [Fact]
        public async Task CountTokensAsync_MatchesTokenizeCount()
        {
            // Arrange
            var text = "Test prompt for tokenization";

            // Act
            var count = await _tokenizerService.CountTokensAsync(text);
            var tokens = await _tokenizerService.TokenizeTextAsync(text);

            // Assert
            Assert.Equal(count, tokens.Count);
        }

        [Fact]
        public async Task TokenizeTextAsync_SpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            var text = "Hello, @user! How's it going?";

            // Act
            var tokens = await _tokenizerService.TokenizeTextAsync(text);

            // Assert
            Assert.NotNull(tokens);
            Assert.NotEmpty(tokens);
        }

        [Fact]
        public async Task CountTokensAsync_UnicodeCharacters_HandlesCorrectly()
        {
            // Arrange
            var text = "Hello ?? ??";

            // Act
            var result = await _tokenizerService.CountTokensAsync(text);

            // Assert
            Assert.True(result > 0);
        }

        [Fact]
        public async Task CountTokensAsync_MultipleCallsSameText_ReturnsSameResult()
        {
            // Arrange
            var text = "Consistency test";

            // Act
            var result1 = await _tokenizerService.CountTokensAsync(text);
            var result2 = await _tokenizerService.CountTokensAsync(text);
            var result3 = await _tokenizerService.CountTokensAsync(text);

            // Assert
            Assert.Equal(result1, result2);
            Assert.Equal(result2, result3);
        }

        [Fact]
        public async Task TokenizeTextAsync_SubwordTokenization_Works()
        {
            // Arrange
            var text = "tokenizer";

            // Act
            var tokens = await _tokenizerService.TokenizeTextAsync(text);

            // Assert
            Assert.NotNull(tokens);
            Assert.NotEmpty(tokens);
            // "tokenizer" might split into ["token", "izer"] or similar
            var reconstructed = string.Concat(tokens);
            Assert.Equal(text, reconstructed);
        }

        [Theory]
        [InlineData("a", 1, 2)]
        [InlineData("cat", 1, 2)]
        [InlineData("hello world", 2, 4)]
        [InlineData("The quick brown fox jumps", 4, 8)]
        public async Task CountTokensAsync_VariousTexts_ReturnsExpectedRange(string text, int minTokens, int maxTokens)
        {
            // Act
            var result = await _tokenizerService.CountTokensAsync(text);

            // Assert
            Assert.InRange(result, minTokens, maxTokens);
        }

        [Fact]
        public async Task Service_ConcurrentCalls_HandlesCorrectly()
        {
            // Arrange
            var text = "Concurrent test";

            // Act
            var tasks = Enumerable.Range(0, 10)
                .Select(_ => _tokenizerService.CountTokensAsync(text))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.All(results, r => Assert.True(r > 0));
            Assert.All(results, r => Assert.Equal(results[0], r));
        }

        [Fact]
        public async Task TokenizeTextAsync_PreservesWhitespace()
        {
            // Arrange
            var text = "Hello   world"; // Multiple spaces

            // Act
            var tokens = await _tokenizerService.TokenizeTextAsync(text);
            var reconstructed = string.Concat(tokens);

            // Assert
            Assert.Equal(text, reconstructed);
        }
    }
}
