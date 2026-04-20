using BlazorWebApp.Scheduler.Engine;
using BlazorWebApp.Scheduler.Targets;
using BlazorWebApp.Scheduler.Variations;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BlazorWebApp.Tests.Scheduler;

public class VariationMaterializerTests
{
    private readonly Mock<IWildcardService> _wildcards = new();
    private readonly OllamaService _ollama;
    private readonly VariationMaterializer _sut;

    public VariationMaterializerTests()
    {
        // OllamaService is a concrete class with dependencies we don't need here; these tests
        // that touch the LLM path use a dedicated harness below.
        _ollama = null!;
        _sut = new VariationMaterializer(_wildcards.Object, _ollama!, NullLogger<VariationMaterializer>.Instance);
    }

    private static ParameterTarget TargetOf() => new FragmentTarget { FragmentId = "f", ParamKey = "p" };

    [Fact]
    public async Task List_PreservesOrder()
    {
        var v = new ListVariation { Target = TargetOf(), Values = new() { 1, "two", 3.5 } };
        var result = await _sut.MaterializeAsync(v);
        result.Should().Equal(new object?[] { 1, "two", 3.5 });
    }

    [Fact]
    public async Task Range_InclusiveStepped_Double()
    {
        var v = new RangeVariation { Target = TargetOf(), Start = 1, End = 3, Step = 0.5 };
        var result = await _sut.MaterializeAsync(v);
        result.Should().Equal(new object?[] { 1.0, 1.5, 2.0, 2.5, 3.0 });
    }

    [Fact]
    public async Task Range_Integer_Coerced()
    {
        var v = new RangeVariation { Target = TargetOf(), Start = 1, End = 4, Step = 1, IsInteger = true };
        var result = await _sut.MaterializeAsync(v);
        result.Should().AllBeOfType<long>();
        result.Should().Equal(new object?[] { 1L, 2L, 3L, 4L });
    }

    [Fact]
    public async Task Range_InvalidStep_Throws()
    {
        var v = new RangeVariation { Target = TargetOf(), Start = 0, End = 1, Step = 0 };
        var act = async () => await _sut.MaterializeAsync(v);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Toggle_Yields_OnThenOff()
    {
        var v = new ToggleVariation { Target = TargetOf(), OnValue = true, OffValue = false };
        var result = await _sut.MaterializeAsync(v);
        result.Should().Equal(new object?[] { true, false });
    }

    [Fact]
    public async Task SearchReplace_Yields_ReplacementsAsValues()
    {
        var v = new SearchReplaceVariation
        {
            Target = new PromptTarget(),
            Search = "x",
            Replacements = new() { "a", "b", "c" }
        };
        var result = await _sut.MaterializeAsync(v);
        result.Should().Equal(new object?[] { "a", "b", "c" });
    }

    [Fact]
    public async Task Random_Seeded_IsDeterministic()
    {
        var v1 = new RandomVariation { Target = TargetOf(), Min = 0, Max = 100, Count = 5, Seed = 42 };
        var v2 = new RandomVariation { Target = TargetOf(), Min = 0, Max = 100, Count = 5, Seed = 42 };

        var r1 = await _sut.MaterializeAsync(v1);
        var r2 = await _sut.MaterializeAsync(v2);

        r1.Should().Equal(r2);
        r1.Should().HaveCount(5);
    }

    [Fact]
    public async Task Random_Integer_Coerced()
    {
        var v = new RandomVariation { Target = TargetOf(), Min = 0, Max = 10, Count = 3, Seed = 1, IsInteger = true };
        var result = await _sut.MaterializeAsync(v);
        result.Should().AllBeOfType<long>();
    }

    [Fact]
    public async Task Random_InvalidRange_Throws()
    {
        var v = new RandomVariation { Target = TargetOf(), Min = 5, Max = 5, Count = 1 };
        var act = async () => await _sut.MaterializeAsync(v);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Wildcard_NoRepeatsNoCount_UsesAllEntries()
    {
        _wildcards.Setup(w => w.GetAllEntryValues("colors"))
            .ReturnsAsync(new List<string> { "red", "green", "blue" });

        var v = new WildcardVariation { Target = new PromptTarget(), CollectionName = "colors" };
        var result = await _sut.MaterializeAsync(v);
        result.Should().Equal(new object?[] { "red", "green", "blue" });
    }

    [Fact]
    public async Task Wildcard_Weighted_CallsWeightedApi()
    {
        _wildcards.Setup(w => w.GetRandomEntryWeighted("artists")).ReturnsAsync("x");
        var v = new WildcardVariation
        {
            Target = new PromptTarget(),
            CollectionName = "artists",
            Count = 2,
            AllowRepeats = true,
            Weighted = true
        };

        var result = await _sut.MaterializeAsync(v);

        result.Should().HaveCount(2);
        _wildcards.Verify(w => w.GetRandomEntryWeighted("artists"), Times.Exactly(2));
        _wildcards.Verify(w => w.GetRandomEntry(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Wildcard_NoAllowRepeats_SkipsDuplicates()
    {
        // Returns duplicate "a" first, then "b"
        var sequence = new Queue<string>(new[] { "a", "a", "b" });
        _wildcards.Setup(w => w.GetRandomEntry("c"))
            .ReturnsAsync(() => sequence.Count > 0 ? sequence.Dequeue() : null);

        var v = new WildcardVariation
        {
            Target = new PromptTarget(),
            CollectionName = "c",
            Count = 2,
            AllowRepeats = false
        };

        var result = await _sut.MaterializeAsync(v);
        result.Should().Equal(new object?[] { "a", "b" });
    }

    [Fact]
    public async Task Wildcard_NullCollection_ReturnsEmpty()
    {
        _wildcards.Setup(w => w.GetRandomEntry(It.IsAny<string>())).ReturnsAsync((string?)null);
        var v = new WildcardVariation
        {
            Target = new PromptTarget(),
            CollectionName = "missing",
            Count = 3,
            AllowRepeats = true
        };
        var result = await _sut.MaterializeAsync(v);
        result.Should().BeEmpty();
    }
}
