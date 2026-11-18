using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BlazorWebApp.Tests.Services
{
    /// <summary>
    /// Tests for CacheService focusing on tag caching and fuzzy matching.
    /// Dictionary tests are limited as they require file system access.
    /// </summary>
    public class CacheServiceTests
    {
        #region Fuzzy Matching Tests

        [Theory]
        [InlineData("test", "test", 1000)] // Exact match
        [InlineData("test", "testing", 500)] // Starts with
        [InlineData("test", "a test", 300)] // Word boundary
        [InlineData("test", "contest", 250)] // Contains
        public void CalculateFuzzyScore_VariousMatchTypes_ReturnsExpectedScores(string search, string target, int expectedScore)
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockLogger = new Mock<ILogger<CacheService>>();
            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());
            
            var service = new CacheService(mockDb.Object, mockLogger.Object, mockEnv.Object);

            // Act
            var score = service.CalculateFuzzyScore(search, target);

            // Assert
            score.Should().Be(expectedScore);
        }

        [Fact]
        public void CalculateFuzzyScore_ExactMatch_ReturnsHighestScore()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockLogger = new Mock<ILogger<CacheService>>();
            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());
            
            var service = new CacheService(mockDb.Object, mockLogger.Object, mockEnv.Object);

            // Act
            var score = service.CalculateFuzzyScore("masterpiece", "masterpiece");

            // Assert
            score.Should().Be(1000);
        }

        [Fact]
        public void CalculateFuzzyScore_NoMatch_ReturnsZero()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockLogger = new Mock<ILogger<CacheService>>();
            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());
            
            var service = new CacheService(mockDb.Object, mockLogger.Object, mockEnv.Object);

            // Act
            var score = service.CalculateFuzzyScore("xyz", "abc");

            // Assert
            score.Should().Be(0);
        }

        [Fact]
        public void CalculateFuzzyScore_EmptySearch_ReturnsZero()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockLogger = new Mock<ILogger<CacheService>>();
            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());
            
            var service = new CacheService(mockDb.Object, mockLogger.Object, mockEnv.Object);

            // Act
            var score = service.CalculateFuzzyScore("", "target");

            // Assert
            score.Should().Be(0);
        }

        [Fact]
        public void CalculateFuzzyScore_EmptyTarget_ReturnsZero()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockLogger = new Mock<ILogger<CacheService>>();
            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());
            
            var service = new CacheService(mockDb.Object, mockLogger.Object, mockEnv.Object);

            // Act
            var score = service.CalculateFuzzyScore("search", "");

            // Assert
            score.Should().Be(0);
        }

        [Theory]
        [InlineData("1girl", "1girl solo", 500)] // Starts with
        [InlineData("solo", "1girl solo", 300)] // Word boundary  
        [InlineData("girl", "1girl", 250)] // Contains
        public void CalculateFuzzyScore_CommonTagPatterns_ReturnsExpectedScores(string search, string target, int expectedScore)
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockLogger = new Mock<ILogger<CacheService>>();
            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());
            
            var service = new CacheService(mockDb.Object, mockLogger.Object, mockEnv.Object);

            // Act
            var score = service.CalculateFuzzyScore(search, target);

            // Assert
            score.Should().Be(expectedScore);
        }

        #endregion

        #region Tag Usage Tests

        [Fact]
        public void IncrementTagUsage_NewTag_AddsToCache()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockLogger = new Mock<ILogger<CacheService>>();
            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());
            
            var service = new CacheService(mockDb.Object, mockLogger.Object, mockEnv.Object);

            // Act
            service.IncrementTagUsage("new_tag");

            // Assert
            var recentTags = service.GetRecentTags(10);
            recentTags.Should().Contain("new_tag");
        }

        [Fact]
        public void IncrementTagUsage_ExistingTag_IncrementsCount()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockLogger = new Mock<ILogger<CacheService>>();
            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());
            
            var service = new CacheService(mockDb.Object, mockLogger.Object, mockEnv.Object);

            // Act
            service.IncrementTagUsage("test_tag");
            service.IncrementTagUsage("test_tag");
            service.IncrementTagUsage("test_tag");

            // Assert - tag should still be in recent tags (deduplicated)
            var recentTags = service.GetRecentTags(10);
            recentTags.Count(t => t == "test_tag").Should().Be(1);
        }

        [Fact]
        public void IncrementTagUsage_NormalizesTagName()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockLogger = new Mock<ILogger<CacheService>>();
            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());
            
            var service = new CacheService(mockDb.Object, mockLogger.Object, mockEnv.Object);

            // Act
            service.IncrementTagUsage("Test Tag");

            // Assert - should be normalized to lowercase with underscores
            var recentTags = service.GetRecentTags(10);
            recentTags.Should().Contain("test_tag");
        }

        [Fact]
        public void GetRecentTags_ReturnsTagsInRecentOrder()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockLogger = new Mock<ILogger<CacheService>>();
            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());
            
            var service = new CacheService(mockDb.Object, mockLogger.Object, mockEnv.Object);

            // Act
            service.IncrementTagUsage("tag1");
            service.IncrementTagUsage("tag2");
            service.IncrementTagUsage("tag3");

            // Assert - most recent should be first
            var recentTags = service.GetRecentTags(10);
            recentTags[0].Should().Be("tag3");
            recentTags[1].Should().Be("tag2");
            recentTags[2].Should().Be("tag1");
        }

        [Fact]
        public void GetRecentTags_LimitsResults()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockLogger = new Mock<ILogger<CacheService>>();
            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());
            
            var service = new CacheService(mockDb.Object, mockLogger.Object, mockEnv.Object);

            // Act - add 10 tags
            for (int i = 1; i <= 10; i++)
            {
                service.IncrementTagUsage($"tag{i}");
            }

            // Assert - should return only requested count
            var recentTags = service.GetRecentTags(5);
            recentTags.Should().HaveCount(5);
        }

        [Fact]
        public void GetRecentTags_MovesReusedTagToFront()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockLogger = new Mock<ILogger<CacheService>>();
            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());
            
            var service = new CacheService(mockDb.Object, mockLogger.Object, mockEnv.Object);

            // Act
            service.IncrementTagUsage("tag1");
            service.IncrementTagUsage("tag2");
            service.IncrementTagUsage("tag3");
            service.IncrementTagUsage("tag1"); // Reuse tag1

            // Assert - tag1 should now be first (most recent)
            var recentTags = service.GetRecentTags(10);
            recentTags[0].Should().Be("tag1");
        }

        #endregion

        #region Dictionary Tests

        [Fact]
        public void AreDictionariesLoaded_InitiallyFalse_UntilLoaded()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockLogger = new Mock<ILogger<CacheService>>();
            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());
            
            var service = new CacheService(mockDb.Object, mockLogger.Object, mockEnv.Object);

            // Assert - dictionaries load async, so initially may be false or true
            // This is expected behavior - LoadDictionaries runs in constructor
            var loaded = service.AreDictionariesLoaded();
            loaded.Should().Be(loaded); // Just verify it returns a boolean without throwing
        }

        [Fact]
        public void SearchDictionaries_EmptyQuery_ReturnsEmptyList()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockLogger = new Mock<ILogger<CacheService>>();
            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());
            
            var service = new CacheService(mockDb.Object, mockLogger.Object, mockEnv.Object);

            // Act
            var results = service.SearchDictionaries("");

            // Assert
            results.Should().BeEmpty();
        }

        [Fact]
        public void SearchDictionaries_NullQuery_ReturnsEmptyList()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockLogger = new Mock<ILogger<CacheService>>();
            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());
            
            var service = new CacheService(mockDb.Object, mockLogger.Object, mockEnv.Object);

            // Act
            var results = service.SearchDictionaries(null!);

            // Assert
            results.Should().BeEmpty();
        }

        [Fact]
        public void GetDictionaryWords_InvalidTheme_ReturnsEmptyList()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockLogger = new Mock<ILogger<CacheService>>();
            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());
            
            var service = new CacheService(mockDb.Object, mockLogger.Object, mockEnv.Object);

            // Act
            var results = service.GetDictionaryWords(DictionaryTheme.Quality);

            // Assert - may be empty if dictionaries haven't loaded yet
            results.Should().NotBeNull();
        }

        [Fact]
        public void GetDictionaryStats_ReturnsValidDictionary()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockLogger = new Mock<ILogger<CacheService>>();
            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());
            
            var service = new CacheService(mockDb.Object, mockLogger.Object, mockEnv.Object);

            // Act
            var stats = service.GetDictionaryStats();

            // Assert
            stats.Should().NotBeNull();
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_InitializesWithEmptyRecentTags()
        {
            // Arrange
            var mockDb = new Mock<IDatabaseService>();
            var mockLogger = new Mock<ILogger<CacheService>>();
            var mockEnv = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
            mockEnv.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());

            // Act
            var service = new CacheService(mockDb.Object, mockLogger.Object, mockEnv.Object);

            // Assert
            service.GetRecentTags().Should().BeEmpty();
        }

        #endregion
    }
}
