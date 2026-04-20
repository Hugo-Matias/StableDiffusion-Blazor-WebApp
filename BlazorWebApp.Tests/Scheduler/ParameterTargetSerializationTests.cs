using System.Text.Json;
using BlazorWebApp.Scheduler;
using BlazorWebApp.Scheduler.Targets;
using FluentAssertions;

namespace BlazorWebApp.Tests.Scheduler;

public class ParameterTargetSerializationTests
{
    private static readonly JsonSerializerOptions Options = SchedulerJsonOptions.Compact;

    [Fact]
    public void FragmentTarget_RoundTrips()
    {
        var original = new FragmentTarget { FragmentId = "main_sampler", ParamKey = "steps" };
        var json = JsonSerializer.Serialize<ParameterTarget>(original, Options);
        json.Should().Contain("\"$type\":\"fragment\"");

        var back = JsonSerializer.Deserialize<ParameterTarget>(json, Options);
        back.Should().BeOfType<FragmentTarget>();
        var ft = (FragmentTarget)back!;
        ft.FragmentId.Should().Be("main_sampler");
        ft.ParamKey.Should().Be("steps");
    }

    [Fact]
    public void LoraTarget_RoundTrips()
    {
        var original = new LoraTarget { LoraName = "styleA" };
        var json = JsonSerializer.Serialize<ParameterTarget>(original, Options);
        json.Should().Contain("\"$type\":\"lora\"");

        var back = JsonSerializer.Deserialize<ParameterTarget>(json, Options);
        back.Should().BeOfType<LoraTarget>().Which.LoraName.Should().Be("styleA");
    }

    [Fact]
    public void AssetTarget_RoundTrips()
    {
        var original = new AssetTarget { AssetKey = "Model" };
        var json = JsonSerializer.Serialize<ParameterTarget>(original, Options);
        var back = JsonSerializer.Deserialize<ParameterTarget>(json, Options);
        back.Should().BeOfType<AssetTarget>().Which.AssetKey.Should().Be("Model");
    }

    [Fact]
    public void PromptTarget_RoundTrips()
    {
        var original = new PromptTarget { IsNegative = true };
        var json = JsonSerializer.Serialize<ParameterTarget>(original, Options);
        var back = JsonSerializer.Deserialize<ParameterTarget>(json, Options);
        back.Should().BeOfType<PromptTarget>().Which.IsNegative.Should().BeTrue();
    }

    [Fact]
    public void OutputTarget_RoundTrips()
    {
        var original = new OutputTarget { Field = OutputField.Folder };
        var json = JsonSerializer.Serialize<ParameterTarget>(original, Options);
        var back = JsonSerializer.Deserialize<ParameterTarget>(json, Options);
        back.Should().BeOfType<OutputTarget>().Which.Field.Should().Be(OutputField.Folder);
    }

    [Fact]
    public void DisplayName_IsNotSerialized()
    {
        var original = new FragmentTarget { FragmentId = "a", ParamKey = "b" };
        var json = JsonSerializer.Serialize<ParameterTarget>(original, Options);
        json.Should().NotContain("displayName", because: "DisplayName is exposed as a method, not a property");
        original.GetDisplayName().Should().Be("a.b");
    }
}
