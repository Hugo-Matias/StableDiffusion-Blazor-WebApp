using System.Text.Json;
using BlazorWebApp.Scheduler;
using BlazorWebApp.Scheduler.Targets;
using BlazorWebApp.Scheduler.Variations;
using FluentAssertions;

namespace BlazorWebApp.Tests.Scheduler;

public class VariationSerializationTests
{
    private static readonly JsonSerializerOptions Options = SchedulerJsonOptions.Compact;

    private static ParameterTarget FragmentTarget() =>
        new FragmentTarget { FragmentId = "main_sampler", ParamKey = "cfg" };

    [Fact]
    public void ListVariation_RoundTrips()
    {
        var original = new ListVariation
        {
            Label = "cfg",
            Target = FragmentTarget(),
            Values = new List<object?> { 1.0, 2.0, 3.0 }
        };
        var json = JsonSerializer.Serialize<Variation>(original, Options);
        json.Should().Contain("\"$type\":\"list\"");
        var back = JsonSerializer.Deserialize<Variation>(json, Options);
        back.Should().BeOfType<ListVariation>()
            .Which.Values.Should().HaveCount(3);
    }

    [Fact]
    public void RangeVariation_RoundTrips()
    {
        var original = new RangeVariation
        {
            Target = FragmentTarget(),
            Start = 1,
            End = 5,
            Step = 0.5,
            IsInteger = false
        };
        var json = JsonSerializer.Serialize<Variation>(original, Options);
        var back = JsonSerializer.Deserialize<Variation>(json, Options);
        var r = back.Should().BeOfType<RangeVariation>().Subject;
        r.Start.Should().Be(1);
        r.End.Should().Be(5);
        r.Step.Should().Be(0.5);
    }

    [Fact]
    public void RandomVariation_RoundTrips()
    {
        var original = new RandomVariation
        {
            Target = FragmentTarget(),
            Min = 0,
            Max = 10,
            Count = 4,
            Seed = 42,
            IsInteger = true
        };
        var json = JsonSerializer.Serialize<Variation>(original, Options);
        var back = JsonSerializer.Deserialize<Variation>(json, Options);
        var r = back.Should().BeOfType<RandomVariation>().Subject;
        r.Seed.Should().Be(42);
        r.IsInteger.Should().BeTrue();
    }

    [Fact]
    public void WildcardVariation_RoundTrips()
    {
        var original = new WildcardVariation
        {
            Target = new PromptTarget(),
            CollectionName = "artists",
            Count = 5,
            AllowRepeats = true,
            Weighted = true
        };
        var json = JsonSerializer.Serialize<Variation>(original, Options);
        var back = JsonSerializer.Deserialize<Variation>(json, Options);
        var w = back.Should().BeOfType<WildcardVariation>().Subject;
        w.CollectionName.Should().Be("artists");
        w.Count.Should().Be(5);
        w.AllowRepeats.Should().BeTrue();
        w.Weighted.Should().BeTrue();
    }

    [Fact]
    public void LlmVariation_RoundTrips()
    {
        var original = new LlmVariation
        {
            Target = new PromptTarget(),
            ModelName = "llama3",
            BasePrompt = "a cat",
            Count = 3,
            IsNegative = false,
            SystemPromptTemplateId = 42
        };
        var json = JsonSerializer.Serialize<Variation>(original, Options);
        var back = JsonSerializer.Deserialize<Variation>(json, Options);
        var l = back.Should().BeOfType<LlmVariation>().Subject;
        l.ModelName.Should().Be("llama3");
        l.BasePrompt.Should().Be("a cat");
        l.Count.Should().Be(3);
        l.SystemPromptTemplateId.Should().Be(42);
    }

    [Fact]
    public void SearchReplaceVariation_RoundTrips()
    {
        var original = new SearchReplaceVariation
        {
            Target = new PromptTarget(),
            Search = "cat",
            Replacements = new() { "dog", "fox", "owl" }
        };
        var json = JsonSerializer.Serialize<Variation>(original, Options);
        var back = JsonSerializer.Deserialize<Variation>(json, Options);
        var s = back.Should().BeOfType<SearchReplaceVariation>().Subject;
        s.Replacements.Should().ContainInOrder("dog", "fox", "owl");
    }

    [Fact]
    public void ToggleVariation_RoundTrips()
    {
        var original = new ToggleVariation
        {
            Target = new LoraTarget { LoraName = "styleA" },
            OnValue = true,
            OffValue = false
        };
        var json = JsonSerializer.Serialize<Variation>(original, Options);
        var back = JsonSerializer.Deserialize<Variation>(json, Options);
        back.Should().BeOfType<ToggleVariation>();
    }
}
