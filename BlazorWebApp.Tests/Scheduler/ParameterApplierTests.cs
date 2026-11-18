using BlazorWebApp.Models;
using BlazorWebApp.Scheduler.Engine;
using BlazorWebApp.Scheduler.Models;
using BlazorWebApp.Scheduler.Targets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlazorWebApp.Tests.Scheduler;

public class ParameterApplierTests
{
    private readonly ParameterApplier _sut = new(NullLogger<ParameterApplier>.Instance);

    [Fact]
    public void FragmentTarget_WritesValueIntoFragment()
    {
        var p = new GenerationParameters();
        var output = new JobOutputConfig();
        var target = new FragmentTarget { FragmentId = "main_sampler", ParamKey = "steps" };

        _sut.Apply(p, output, target, 42L);

        p.GetFragment("main_sampler")!.Values["steps"].Should().Be(42L);
    }

    [Fact]
    public void PromptTarget_Positive_WritesToPromptsFragment()
    {
        var p = new GenerationParameters();
        var output = new JobOutputConfig();

        _sut.Apply(p, output, new PromptTarget { IsNegative = false }, "cat");

        p.GetFragment("prompts")!.Values["positive"].Should().Be("cat");
    }

    [Fact]
    public void PromptTarget_Negative_WritesToNegative()
    {
        var p = new GenerationParameters();
        var output = new JobOutputConfig();

        _sut.Apply(p, output, new PromptTarget { IsNegative = true }, "blurry");

        p.GetFragment("prompts")!.Values["negative"].Should().Be("blurry");
    }

    [Fact]
    public void LoraTarget_WritesStrengthOnMatchingLora()
    {
        var p = new GenerationParameters();
        p.Loras.Add(new Lora { Name = "MyLora", Strength = 0.5f, IsEnabled = true });
        var output = new JobOutputConfig();

        _sut.Apply(p, output, new LoraTarget { LoraName = "MyLora" }, 0.75);

        p.Loras[0].Strength.Should().BeApproximately(0.75f, 0.0001f);
    }

    [Fact]
    public void LoraTarget_UnknownName_IsNoOp()
    {
        var p = new GenerationParameters();
        p.Loras.Add(new Lora { Name = "Existing", Strength = 0.5f });
        var output = new JobOutputConfig();

        _sut.Apply(p, output, new LoraTarget { LoraName = "Missing" }, 1.0);

        p.Loras[0].Strength.Should().Be(0.5f);
    }

    [Fact]
    public void AssetTarget_WritesToAssets()
    {
        var p = new GenerationParameters();
        var output = new JobOutputConfig();

        _sut.Apply(p, output, new AssetTarget { AssetKey = "Model" }, "flux.safetensors");

        p.Assets["Model"].Should().Be("flux.safetensors");
    }

    [Fact]
    public void OutputTarget_Project_WritesToOutput()
    {
        var p = new GenerationParameters();
        var output = new JobOutputConfig();

        _sut.Apply(p, output, new OutputTarget { Field = OutputField.Project }, "ProjA");

        output.ProjectName.Should().Be("ProjA");
    }

    [Fact]
    public void OutputTarget_Folder_WritesToOutput()
    {
        var p = new GenerationParameters();
        var output = new JobOutputConfig { ProjectName = "Keep" };

        _sut.Apply(p, output, new OutputTarget { Field = OutputField.Folder }, "F1");

        output.FolderName.Should().Be("F1");
        output.ProjectName.Should().Be("Keep");
    }
}
