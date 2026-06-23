using System.Text.Json;
using BlazorWebApp.Models;
using BlazorWebApp.Scheduler;
using BlazorWebApp.Scheduler.Directives;
using BlazorWebApp.Scheduler.Targets;
using FluentAssertions;

namespace BlazorWebApp.Tests.Scheduler;

public class DirectiveSerializationTests
{
    private static readonly JsonSerializerOptions Options = SchedulerJsonOptions.Compact;

    [Fact]
    public void SetValueDirective_RoundTrips_WithPolymorphicTarget()
    {
        var original = new SetValueDirective
        {
            Label = "pin steps",
            Target = new FragmentTarget { FragmentId = "main_sampler", ParamKey = "steps" },
            Value = 30
        };

        var json = JsonSerializer.Serialize<Directive>(original, Options);
        json.Should().Contain("\"$type\":\"set\"");
        json.Should().Contain("\"$type\":\"fragment\"");

        var back = JsonSerializer.Deserialize<Directive>(json, Options);
        var set = back.Should().BeOfType<SetValueDirective>().Subject;
        set.Label.Should().Be("pin steps");
        set.Target.Should().BeOfType<FragmentTarget>()
            .Which.ParamKey.Should().Be("steps");
    }

    [Fact]
    public void AppendPromptDirective_RoundTrips()
    {
        var original = new AppendPromptDirective
        {
            Text = "masterpiece",
            IsPrefix = true,
            IsNegative = false,
            Separator = " | "
        };
        var json = JsonSerializer.Serialize<Directive>(original, Options);
        var back = JsonSerializer.Deserialize<Directive>(json, Options);
        var a = back.Should().BeOfType<AppendPromptDirective>().Subject;
        a.Text.Should().Be("masterpiece");
        a.IsPrefix.Should().BeTrue();
        a.Separator.Should().Be(" | ");
    }

    [Fact]
    public void ReplacePromptDirective_RoundTrips()
    {
        var original = new ReplacePromptDirective
        {
            Search = "cat",
            Replace = "dog",
            IsNegative = true,
            CaseSensitive = true
        };
        var json = JsonSerializer.Serialize<Directive>(original, Options);
        var back = JsonSerializer.Deserialize<Directive>(json, Options);
        var r = back.Should().BeOfType<ReplacePromptDirective>().Subject;
        r.Search.Should().Be("cat");
        r.Replace.Should().Be("dog");
        r.IsNegative.Should().BeTrue();
        r.CaseSensitive.Should().BeTrue();
    }

    [Fact]
    public void AddLoraDirective_RoundTrips()
    {
        var original = new AddLoraDirective
        {
            Lora = new Lora { Name = "styleA", Strength = 0.75f, IsEnabled = true }
        };
        var json = JsonSerializer.Serialize<Directive>(original, Options);
        var back = JsonSerializer.Deserialize<Directive>(json, Options);
        var add = back.Should().BeOfType<AddLoraDirective>().Subject;
        add.Lora.Name.Should().Be("styleA");
        add.Lora.Strength.Should().Be(0.75f);
    }

    [Fact]
    public void RemoveLoraDirective_RoundTrips()
    {
        var json = JsonSerializer.Serialize<Directive>(
            new RemoveLoraDirective { LoraName = "x" }, Options);
        JsonSerializer.Deserialize<Directive>(json, Options)
            .Should().BeOfType<RemoveLoraDirective>()
            .Which.LoraName.Should().Be("x");
    }

    [Fact]
    public void ToggleLoraDirective_RoundTrips()
    {
        var json = JsonSerializer.Serialize<Directive>(
            new ToggleLoraDirective { LoraName = "x", Enable = false }, Options);
        var back = JsonSerializer.Deserialize<Directive>(json, Options);
        back.Should().BeOfType<ToggleLoraDirective>()
            .Which.Enable.Should().BeFalse();
    }

    [Fact]
    public void AddPromptStyleDirective_RoundTrips()
    {
        var json = JsonSerializer.Serialize<Directive>(
            new AddPromptStyleDirective { StyleNames = new() { "anime", "cinematic" } },
            Options);
        var back = JsonSerializer.Deserialize<Directive>(json, Options);
        back.Should().BeOfType<AddPromptStyleDirective>()
            .Which.StyleNames.Should().ContainInOrder("anime", "cinematic");
    }

    [Fact]
    public void SwapAssetDirective_RoundTrips()
    {
        var json = JsonSerializer.Serialize<Directive>(
            new SwapAssetDirective { AssetKey = "Model", AssetValue = "flux.safetensors" },
            Options);
        var back = JsonSerializer.Deserialize<Directive>(json, Options);
        back.Should().BeOfType<SwapAssetDirective>()
            .Which.AssetValue.Should().Be("flux.safetensors");
    }

    [Fact]
    public void SetOutputDirective_RoundTrips()
    {
        var json = JsonSerializer.Serialize<Directive>(
            new SetOutputDirective { ProjectName = "P" },
            Options);
        var back = JsonSerializer.Deserialize<Directive>(json, Options);
        var o = back.Should().BeOfType<SetOutputDirective>().Subject;
        o.ProjectName.Should().Be("P");
    }

    [Fact]
    public void DisabledFlag_RoundTrips()
    {
        var json = JsonSerializer.Serialize<Directive>(
            new SetValueDirective
            {
                Enabled = false,
                Target = new PromptTarget { IsNegative = false },
                Value = "x"
            },
            Options);
        var back = JsonSerializer.Deserialize<Directive>(json, Options);
        back!.Enabled.Should().BeFalse();
    }
}
