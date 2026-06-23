using BlazorWebApp.Data.Entities;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorWebApp.Tests.Services
{
    /// <summary>
    /// Unit tests for WildcardService focusing on wildcard parsing, 
    /// random selection, weighted selection, and collection management.
    /// </summary>
    public class WildcardServiceTests
    {
        #region Helper Methods

        private Mock<IDatabaseService> CreateMockDatabaseService()
        {
            var mockDb = new Mock<IDatabaseService>();

            // Setup default empty collections response
            mockDb.Setup(x => x.GetAllWildcardCollections())
                .ReturnsAsync(new List<WildcardCollection>());

            return mockDb;
        }

        private WildcardService CreateService(Mock<IDatabaseService>? mockDb = null, Mock<ILogger<WildcardService>>? mockLogger = null)
        {
            mockDb ??= CreateMockDatabaseService();
            mockLogger ??= new Mock<ILogger<WildcardService>>();

            return new WildcardService(mockDb.Object, mockLogger.Object);
        }

        private WildcardCollection CreateTestCollection(string name, params (string value, float weight)[] entries)
        {
            var collection = new WildcardCollection
            {
                Id = 1,
                Name = name,
                Description = $"Test collection: {name}",
                Category = "Test",
                Entries = new List<WildcardEntry>()
            };

            for (int i = 0; i < entries.Length; i++)
            {
                collection.Entries.Add(new WildcardEntry
                {
                    Id = i + 1,
                    CollectionId = 1,
                    Value = entries[i].value,
                    Weight = entries[i].weight,
                    SortOrder = i
                });
            }

            return collection;
        }

        #endregion

        #region Wildcard Detection Tests

        [Fact]
        public void DetectWildcards_EmptyString_ReturnsEmptyList()
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = service.DetectWildcards("");

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void DetectWildcards_NullString_ReturnsEmptyList()
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = service.DetectWildcards(null!);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void DetectWildcards_NoWildcards_ReturnsEmptyList()
        {
            // Arrange
            var service = CreateService();
            var input = "This is a normal text without wildcards";

            // Act
            var result = service.DetectWildcards(input);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void DetectWildcards_SingleWildcard_ReturnsWildcardName()
        {
            // Arrange
            var service = CreateService();
            var input = "A photo of __clothing/tops__";

            // Act
            var result = service.DetectWildcards(input);

            // Assert
            result.Should().ContainSingle()
                .Which.Should().Be("clothing/tops");
        }

        [Fact]
        public void DetectWildcards_MultipleWildcards_ReturnsAllNames()
        {
            // Arrange
            var service = CreateService();
            var input = "A __characters/hair-color__ girl wearing __clothing/tops__ and __clothing/bottoms__";

            // Act
            var result = service.DetectWildcards(input);

            // Assert
            result.Should().HaveCount(3)
                .And.Contain("characters/hair-color")
                .And.Contain("clothing/tops")
                .And.Contain("clothing/bottoms");
        }

        [Fact]
        public void DetectWildcards_DuplicateWildcards_ReturnsDistinctNames()
        {
            // Arrange
            var service = CreateService();
            var input = "__clothing/tops__ and another __clothing/tops__";

            // Act
            var result = service.DetectWildcards(input);

            // Assert
            result.Should().ContainSingle()
                .Which.Should().Be("clothing/tops");
        }

        [Fact]
        public void DetectWildcards_WildcardsWithUnderscores_DetectsCorrectly()
        {
            // Arrange
            var service = CreateService();
            var input = "__my_test_wildcard__ and __another_one__";

            // Act
            var result = service.DetectWildcards(input);

            // Assert
            result.Should().HaveCount(2)
                .And.Contain("my_test_wildcard")
                .And.Contain("another_one");
        }

        [Fact]
        public void DetectWildcards_IncompleteWildcards_DoesNotDetect()
        {
            // Arrange
            var service = CreateService();
            var input = "This has _single_ underscores but no valid wildcards";

            // Act
            var result = service.DetectWildcards(input);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public void DetectWildcards_AdjacentWildcards_DetectsBoth()
        {
            // Arrange
            var service = CreateService();
            var input = "__first__ __second__";

            // Act
            var result = service.DetectWildcards(input);

            // Assert
            result.Should().HaveCount(2)
                .And.Contain("first")
                .And.Contain("second");
        }

        [Fact]
        public void DetectWildcards_WildcardsWithSpecialCharacters_DetectsCorrectly()
        {
            // Arrange
            var service = CreateService();
            var input = "__test-collection__ and __another_collection__ and __test.collection__";

            // Act
            var result = service.DetectWildcards(input);

            // Assert
            result.Should().HaveCount(3)
                .And.Contain("test-collection")
                .And.Contain("another_collection")
                .And.Contain("test.collection");
        }

        #endregion

        #region Random Entry Selection Tests

        [Fact]
        public async Task GetRandomEntry_NonExistentCollection_ReturnsNull()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            mockDb.Setup(x => x.GetWildcardCollectionByName("nonexistent"))
                .ReturnsAsync((WildcardCollection?)null);

            var service = CreateService(mockDb);

            // Act
            var result = await service.GetRandomEntry("nonexistent");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetRandomEntry_EmptyCollection_ReturnsNull()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            var emptyCollection = new WildcardCollection
            {
                Id = 1,
                Name = "empty",
                Entries = new List<WildcardEntry>()
            };
            mockDb.Setup(x => x.GetWildcardCollectionByName("empty"))
                .ReturnsAsync(emptyCollection);

            var service = CreateService(mockDb);

            // Act
            var result = await service.GetRandomEntry("empty");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetRandomEntry_CollectionWithOneEntry_ReturnsThatEntry()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            var collection = CreateTestCollection("test", ("only entry", 1.0f));
            mockDb.Setup(x => x.GetWildcardCollectionByName("test"))
                .ReturnsAsync(collection);

            var service = CreateService(mockDb);

            // Act
            var result = await service.GetRandomEntry("test");

            // Assert
            result.Should().Be("only entry");
        }

        [Fact]
        public async Task GetRandomEntry_CollectionWithMultipleEntries_ReturnsOneEntry()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            var collection = CreateTestCollection("test",
                ("entry1", 1.0f),
                ("entry2", 1.0f),
                ("entry3", 1.0f)
            );
            mockDb.Setup(x => x.GetWildcardCollectionByName("test"))
                .ReturnsAsync(collection);

            var service = CreateService(mockDb);

            // Act
            var result = await service.GetRandomEntry("test");

            // Assert
            result.Should().BeOneOf("entry1", "entry2", "entry3");
        }

        [Fact]
        public async Task GetRandomEntry_UpdatesUsageCount()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            var collection = CreateTestCollection("test", ("entry", 1.0f));
            mockDb.Setup(x => x.GetWildcardCollectionByName("test"))
                .ReturnsAsync(collection);

            var service = CreateService(mockDb);

            // Act
            await service.GetRandomEntry("test");

            // Assert
            mockDb.Verify(x => x.UpdateWildcardCollectionUsage(1), Times.Once);
        }

        #endregion

        #region Weighted Random Selection Tests

        [Fact]
        public async Task GetRandomEntryWeighted_NonExistentCollection_ReturnsNull()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            mockDb.Setup(x => x.GetWildcardCollectionByName("nonexistent"))
                .ReturnsAsync((WildcardCollection?)null);

            var service = CreateService(mockDb);

            // Act
            var result = await service.GetRandomEntryWeighted("nonexistent");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetRandomEntryWeighted_EmptyCollection_ReturnsNull()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            var emptyCollection = new WildcardCollection
            {
                Id = 1,
                Name = "empty",
                Entries = new List<WildcardEntry>()
            };
            mockDb.Setup(x => x.GetWildcardCollectionByName("empty"))
                .ReturnsAsync(emptyCollection);

            var service = CreateService(mockDb);

            // Act
            var result = await service.GetRandomEntryWeighted("empty");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetRandomEntryWeighted_AllZeroWeights_FallsBackToEqualProbability()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            var collection = CreateTestCollection("test",
                ("entry1", 0.0f),
                ("entry2", 0.0f),
                ("entry3", 0.0f)
            );
            mockDb.Setup(x => x.GetWildcardCollectionByName("test"))
                .ReturnsAsync(collection);

            var service = CreateService(mockDb);

            // Act
            var result = await service.GetRandomEntryWeighted("test");

            // Assert - Should still return an entry despite zero weights
            result.Should().BeOneOf("entry1", "entry2", "entry3");
        }

        [Fact]
        public async Task GetRandomEntryWeighted_CollectionWithWeights_ReturnsEntry()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            var collection = CreateTestCollection("test",
                ("rare", 0.1f),
                ("common", 1.0f),
                ("very common", 2.0f)
            );
            mockDb.Setup(x => x.GetWildcardCollectionByName("test"))
                .ReturnsAsync(collection);

            var service = CreateService(mockDb);

            // Act
            var result = await service.GetRandomEntryWeighted("test");

            // Assert
            result.Should().BeOneOf("rare", "common", "very common");
        }

        [Fact]
        public async Task GetRandomEntryWeighted_UpdatesUsageCount()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            var collection = CreateTestCollection("test", ("entry", 1.0f));
            mockDb.Setup(x => x.GetWildcardCollectionByName("test"))
                .ReturnsAsync(collection);

            var service = CreateService(mockDb);

            // Act
            await service.GetRandomEntryWeighted("test");

            // Assert
            mockDb.Verify(x => x.UpdateWildcardCollectionUsage(1), Times.Once);
        }

        #endregion

        #region Get All Entry Values Tests

        [Fact]
        public async Task GetAllEntryValues_NonExistentCollection_ReturnsEmptyList()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            mockDb.Setup(x => x.GetWildcardCollectionByName("nonexistent"))
                .ReturnsAsync((WildcardCollection?)null);

            var service = CreateService(mockDb);

            // Act
            var result = await service.GetAllEntryValues("nonexistent");

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAllEntryValues_EmptyCollection_ReturnsEmptyList()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            var emptyCollection = new WildcardCollection
            {
                Id = 1,
                Name = "empty",
                Entries = new List<WildcardEntry>()
            };
            mockDb.Setup(x => x.GetWildcardCollectionByName("empty"))
                .ReturnsAsync(emptyCollection);

            var service = CreateService(mockDb);

            // Act
            var result = await service.GetAllEntryValues("empty");

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetAllEntryValues_CollectionWithEntries_ReturnsAllValues()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            var collection = CreateTestCollection("test",
                ("entry1", 1.0f),
                ("entry2", 1.0f),
                ("entry3", 1.0f)
            );
            mockDb.Setup(x => x.GetWildcardCollectionByName("test"))
                .ReturnsAsync(collection);

            var service = CreateService(mockDb);

            // Act
            var result = await service.GetAllEntryValues("test");

            // Assert
            result.Should().HaveCount(3)
                .And.Contain("entry1")
                .And.Contain("entry2")
                .And.Contain("entry3");
        }

        [Fact]
        public async Task GetAllEntryValues_ReturnsEntriesInSortOrder()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            var collection = new WildcardCollection
            {
                Id = 1,
                Name = "test",
                Entries = new List<WildcardEntry>
                {
                    new() { Value = "third", SortOrder = 2 },
                    new() { Value = "first", SortOrder = 0 },
                    new() { Value = "second", SortOrder = 1 }
                }
            };
            mockDb.Setup(x => x.GetWildcardCollectionByName("test"))
                .ReturnsAsync(collection);

            var service = CreateService(mockDb);

            // Act
            var result = await service.GetAllEntryValues("test");

            // Assert
            result.Should().HaveCount(3);
            result[0].Should().Be("first");
            result[1].Should().Be("second");
            result[2].Should().Be("third");
        }

        #endregion

        #region Collection Exists Tests

        [Fact]
        public async Task CollectionExists_NonExistentCollection_ReturnsFalse()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            mockDb.Setup(x => x.GetWildcardCollectionByName("nonexistent"))
                .ReturnsAsync((WildcardCollection?)null);

            var service = CreateService(mockDb);

            // Act
            var result = await service.CollectionExists("nonexistent");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task CollectionExists_ExistingCollection_ReturnsTrue()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            var collection = CreateTestCollection("existing", ("entry", 1.0f));
            mockDb.Setup(x => x.GetWildcardCollectionByName("existing"))
                .ReturnsAsync(collection);

            var service = CreateService(mockDb);

            // Act
            var result = await service.CollectionExists("existing");

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task CollectionExists_EmptyCollection_ReturnsTrue()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            var emptyCollection = new WildcardCollection
            {
                Id = 1,
                Name = "empty",
                Entries = new List<WildcardEntry>()
            };
            mockDb.Setup(x => x.GetWildcardCollectionByName("empty"))
                .ReturnsAsync(emptyCollection);

            var service = CreateService(mockDb);

            // Act
            var result = await service.CollectionExists("empty");

            // Assert
            result.Should().BeTrue();
        }

        #endregion

        #region Parse Wildcards Tests

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ParseWildcards_EmptyOrNullInput_ReturnsInput(string? input)
        {
            // Arrange
            var service = CreateService();

            // Act
            var result = await service.ParseWildcards(input!);

            // Assert
            result.Should().Be(input);
        }

        [Fact]
        public async Task ParseWildcards_NoWildcards_ReturnsOriginalText()
        {
            // Arrange
            var service = CreateService();
            var input = "This is normal text without wildcards";

            // Act
            var result = await service.ParseWildcards(input);

            // Assert
            result.Should().Be(input);
        }

        [Fact]
        public async Task ParseWildcards_SingleWildcard_ReplacesWithValue()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            var collection = CreateTestCollection("test", ("replaced value", 1.0f));
            mockDb.Setup(x => x.GetWildcardCollectionByName("test"))
                .ReturnsAsync(collection);

            var service = CreateService(mockDb);
            var input = "This is __test__ text";

            // Act
            var result = await service.ParseWildcards(input);

            // Assert
            result.Should().Be("This is replaced value text");
        }

        [Fact]
        public async Task ParseWildcards_MultipleWildcards_ReplacesAll()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();

            var tops = CreateTestCollection("clothing/tops", ("shirt", 1.0f));
            var bottoms = CreateTestCollection("clothing/bottoms", ("jeans", 1.0f));

            mockDb.Setup(x => x.GetWildcardCollectionByName("clothing/tops"))
                .ReturnsAsync(tops);
            mockDb.Setup(x => x.GetWildcardCollectionByName("clothing/bottoms"))
                .ReturnsAsync(bottoms);

            var service = CreateService(mockDb);
            var input = "wearing __clothing/tops__ and __clothing/bottoms__";

            // Act
            var result = await service.ParseWildcards(input);

            // Assert
            result.Should().Be("wearing shirt and jeans");
        }

        [Fact]
        public async Task ParseWildcards_NonExistentWildcard_LeavesUnchanged()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            mockDb.Setup(x => x.GetWildcardCollectionByName("nonexistent"))
                .ReturnsAsync((WildcardCollection?)null);

            var service = CreateService(mockDb);
            var input = "This has __nonexistent__ wildcard";

            // Act
            var result = await service.ParseWildcards(input);

            // Assert
            result.Should().Be("This has __nonexistent__ wildcard");
        }

        [Fact]
        public async Task ParseWildcards_MixedExistingAndNonExisting_ReplacesOnlyExisting()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();

            var existing = CreateTestCollection("existing", ("value", 1.0f));
            mockDb.Setup(x => x.GetWildcardCollectionByName("existing"))
                .ReturnsAsync(existing);
            mockDb.Setup(x => x.GetWildcardCollectionByName("nonexistent"))
                .ReturnsAsync((WildcardCollection?)null);

            var service = CreateService(mockDb);
            var input = "__existing__ and __nonexistent__";

            // Act
            var result = await service.ParseWildcards(input);

            // Assert
            result.Should().Be("value and __nonexistent__");
        }

        #endregion

        #region Seed Collections Tests

        [Fact]
        public async Task SeedCollections_NoExistingCollections_CreatesDefaultCollections()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            mockDb.Setup(x => x.GetAllWildcardCollections())
                .ReturnsAsync(new List<WildcardCollection>());

            var createdCollections = new List<WildcardCollection>();
            mockDb.Setup(x => x.CreateWildcardCollection(It.IsAny<WildcardCollection>()))
                .Callback<WildcardCollection>(c => createdCollections.Add(c))
                .ReturnsAsync((WildcardCollection c) => c);

            var service = CreateService(mockDb);

            // Act
            await service.SeedCollections();

            // Assert
            createdCollections.Should().NotBeEmpty();
            createdCollections.Should().Contain(c => c.Name == "clothing/tops");
            createdCollections.Should().Contain(c => c.Name == "clothing/bottoms");
            createdCollections.Should().Contain(c => c.Category == "Clothing");
            createdCollections.Should().Contain(c => c.Category == "Locations");
            createdCollections.Should().Contain(c => c.Category == "Styles");
        }

        [Fact]
        public async Task SeedCollections_ExistingCollections_SkipsSeed()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            var existingCollection = CreateTestCollection("existing", ("entry", 1.0f));
            mockDb.Setup(x => x.GetAllWildcardCollections())
                .ReturnsAsync(new List<WildcardCollection> { existingCollection });

            var service = CreateService(mockDb);

            // Act
            await service.SeedCollections();

            // Assert
            mockDb.Verify(x => x.CreateWildcardCollection(It.IsAny<WildcardCollection>()), Times.Never);
        }

        [Fact]
        public async Task SeedCollections_CreatesCollectionsWithEntries()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            mockDb.Setup(x => x.GetAllWildcardCollections())
                .ReturnsAsync(new List<WildcardCollection>());

            var createdCollections = new List<WildcardCollection>();
            mockDb.Setup(x => x.CreateWildcardCollection(It.IsAny<WildcardCollection>()))
                .Callback<WildcardCollection>(c => createdCollections.Add(c))
                .ReturnsAsync((WildcardCollection c) => c);

            var service = CreateService(mockDb);

            // Act
            await service.SeedCollections();

            // Assert
            createdCollections.Should().AllSatisfy(c => c.Entries.Should().NotBeEmpty());
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task GetRandomEntry_DatabaseError_ReturnsNull()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            mockDb.Setup(x => x.GetWildcardCollectionByName(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Database error"));

            var service = CreateService(mockDb);

            // Act
            var result = await service.GetRandomEntry("test");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task ParseWildcards_DatabaseError_ReturnsOriginalInput()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            mockDb.Setup(x => x.GetWildcardCollectionByName(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Database error"));

            var service = CreateService(mockDb);
            var input = "Test __wildcard__ text";

            // Act
            var result = await service.ParseWildcards(input);

            // Assert
            result.Should().Be(input);
        }

        [Fact]
        public async Task CollectionExists_DatabaseError_ReturnsFalse()
        {
            // Arrange
            var mockDb = CreateMockDatabaseService();
            mockDb.Setup(x => x.GetWildcardCollectionByName(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Database error"));

            var service = CreateService(mockDb);

            // Act
            var result = await service.CollectionExists("test");

            // Assert
            result.Should().BeFalse();
        }

        #endregion
    }
}
