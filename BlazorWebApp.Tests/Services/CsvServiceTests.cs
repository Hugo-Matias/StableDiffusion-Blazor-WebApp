using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace BlazorWebApp.Tests.Services;

public class CsvServiceTests
{
    [Fact]
    public async Task SearchTags_WithMaxResults_ReturnsOnlyBestRankedMatches()
    {
        using var csvFixture = CsvFixture.Create(
            "alpha_tag,0,5,\"\"",
            "beta_tag,0,100,\"\"",
            "gamma_tag,0,10,\"\"",
            "tag_alpha,0,1,\"\"");
        var cache = new TestCacheService();
        var service = CreateService(csvFixture.InputsPath, cache);

        var results = (await service.SearchTags("tag", enableFuzzy: false, maxResults: 2)).ToList();

        results.Select(tag => tag.Name).Should().Equal("tag alpha", "beta tag");
        cache.SnapshotCalls.Should().Be(1);
        cache.PerTagUsageCalls.Should().Be(0);
    }

    [Fact]
    public async Task SearchTags_UsesAliasFuzzyMatching()
    {
        using var csvFixture = CsvFixture.Create(
            "blue_sky,0,5,\"azure_ceiling\"",
            "red_sky,0,10,\"crimson_ceiling\"");
        var cache = new TestCacheService();
        var service = CreateService(csvFixture.InputsPath, cache);

        var results = (await service.SearchTags("azure", enableFuzzy: true, maxResults: 8)).ToList();

        results.Should().ContainSingle();
        results[0].Name.Should().Be("blue sky");
        results[0].Aliases.Should().Be("azure ceiling");
        results[0].FuzzyScore.Should().Be(450);
    }

    [Fact]
    public async Task SearchTags_LocalUsageSnapshotParticipatesInRanking()
    {
        using var csvFixture = CsvFixture.Create(
            "popular_tag,0,1000,\"\"",
            "local_tag,0,1,\"\"");
        var cache = new TestCacheService
        {
            LocalUsage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["local_tag"] = 3
            }
        };
        var service = CreateService(csvFixture.InputsPath, cache);

        var results = (await service.SearchTags("tag", enableFuzzy: false, maxResults: 2)).ToList();

        results.Select(tag => tag.Name).Should().Equal("local tag", "popular tag");
        results[0].LocalUses.Should().Be(3);
    }

    private static CsvService CreateService(string inputsPath, ICacheService cacheService)
    {
        var backend = new Mock<IBackendService>();
        backend.SetupGet(service => service.IsBackendAvailable).Returns(true);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ComfyUI:InputsPath"] = inputsPath
            })
            .Build();

        return new CsvService(backend.Object, configuration, cacheService);
    }

    private sealed class CsvFixture : IDisposable
    {
        public string InputsPath { get; }

        private CsvFixture(string inputsPath)
        {
            InputsPath = inputsPath;
        }

        public static CsvFixture Create(params string[] rows)
        {
            var inputsPath = Path.Combine(Path.GetTempPath(), "csv-service-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(inputsPath);
            File.WriteAllLines(Path.Combine(inputsPath, "danbooru.csv"), rows);
            return new CsvFixture(inputsPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(InputsPath))
                Directory.Delete(InputsPath, recursive: true);
        }
    }

    private sealed class TestCacheService : ICacheService
    {
        public Dictionary<string, int> LocalUsage { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RecentTags { get; init; } = new();
        public int SnapshotCalls { get; private set; }
        public int PerTagUsageCalls { get; private set; }

        public Task<int> GetLocalTagUsageCount(string tagName)
        {
            PerTagUsageCalls++;
            return Task.FromResult(LocalUsage.TryGetValue(tagName, out var count) ? count : 0);
        }

        public Task<IReadOnlyDictionary<string, int>> GetLocalTagUsageSnapshot()
        {
            SnapshotCalls++;
            return Task.FromResult((IReadOnlyDictionary<string, int>)LocalUsage);
        }

        public Task<List<(string Tag, int Count)>> GetTopLocalTags(int count = 100) =>
            Task.FromResult(LocalUsage.Select(pair => (pair.Key, pair.Value)).Take(count).ToList());

        public List<string> GetRecentTags(int count = 10) => RecentTags.Take(count).ToList();

        public void IncrementTagUsage(string tagName)
        {
        }

        public Task RefreshTagCache() => Task.CompletedTask;

        public Task LoadDictionaries() => Task.CompletedTask;

        public Task ReloadDictionaries() => Task.CompletedTask;

        public List<DictionaryWord> SearchDictionaries(string query, int maxResults = 10, params DictionaryTheme[] excludeThemes) => new();

        public List<DictionaryWord> SearchDictionaryThemes(string query, DictionaryTheme[] themes, int maxResults = 10) => new();

        public List<string> GetDictionaryWords(DictionaryTheme theme) => new();

        public bool AreDictionariesLoaded() => true;

        public Dictionary<string, int> GetDictionaryStats() => new();

        public int CalculateFuzzyScore(string search, string target)
        {
            if (string.IsNullOrEmpty(search) || string.IsNullOrEmpty(target))
                return 0;

            if (target == search) return 1000;
            if (target.StartsWith(search, StringComparison.Ordinal)) return 500;
            if (target.Contains($" {search}", StringComparison.Ordinal)) return 300;
            if (target.Contains(search, StringComparison.Ordinal)) return 250;

            return 0;
        }
    }
}
