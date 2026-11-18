using BlazorWebApp.Models;
using BlazorWebApp.Scheduler.Directives;
using BlazorWebApp.Scheduler.Engine;
using BlazorWebApp.Scheduler.Models;
using BlazorWebApp.Scheduler.Targets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlazorWebApp.Tests.Scheduler;

public class DirectiveExecutorTests
{
    private readonly DirectiveExecutor _sut;

    public DirectiveExecutorTests()
    {
        var applier = new ParameterApplier(NullLogger<ParameterApplier>.Instance);
        _sut = new DirectiveExecutor(applier, NullLogger<DirectiveExecutor>.Instance);
    }

    [Fact]
    public void Disabled_Directive_IsNoOp()
    {
        var p = new GenerationParameters();
        var o = new JobOutputConfig();
        _sut.Apply(p, o, new SwapAssetDirective { Enabled = false, AssetKey = "Model", AssetValue = "x" });
        p.Assets.Should().BeEmpty();
    }

    [Fact]
    public void SetValueDirective_DelegatesToApplier()
    {
        var p = new GenerationParameters();
        var o = new JobOutputConfig();
        var d = new SetValueDirective
        {
            Target = new FragmentTarget { FragmentId = "main_sampler", ParamKey = "steps" },
            Value = 20L
        };
        _sut.Apply(p, o, d);
        p.GetFragment("main_sampler")!.Values["steps"].Should().Be(20L);
    }

    [Fact]
    public void AppendPromptDirective_Appends_WithSeparator()
    {
        var p = new GenerationParameters();
        p.GetOrCreateFragment("prompts").Values["positive"] = "cat";
        _sut.Apply(p, new JobOutputConfig(), new AppendPromptDirective { Text = "cute", Separator = ", " });
        p.GetFragment("prompts")!.Values["positive"].Should().Be("cat, cute");
    }

    [Fact]
    public void AppendPromptDirective_Prepends_WhenIsPrefix()
    {
        var p = new GenerationParameters();
        p.GetOrCreateFragment("prompts").Values["positive"] = "cat";
        _sut.Apply(p, new JobOutputConfig(), new AppendPromptDirective { Text = "cute", IsPrefix = true, Separator = " " });
        p.GetFragment("prompts")!.Values["positive"].Should().Be("cute cat");
    }

    [Fact]
    public void AppendPromptDirective_EmptyCurrent_NoSeparator()
    {
        var p = new GenerationParameters();
        _sut.Apply(p, new JobOutputConfig(), new AppendPromptDirective { Text = "cat", Separator = ", " });
        p.GetFragment("prompts")!.Values["positive"].Should().Be("cat");
    }

    [Fact]
    public void ReplacePromptDirective_CaseInsensitive_Default()
    {
        var p = new GenerationParameters();
        p.GetOrCreateFragment("prompts").Values["positive"] = "A Cat";
        _sut.Apply(p, new JobOutputConfig(), new ReplacePromptDirective { Search = "cat", Replace = "dog" });
        p.GetFragment("prompts")!.Values["positive"].Should().Be("A dog");
    }

    [Fact]
    public void AddLoraDirective_AddsCloneToList()
    {
        var p = new GenerationParameters();
        var lora = new Lora { Name = "L1", Strength = 0.5f, IsEnabled = true };
        _sut.Apply(p, new JobOutputConfig(), new AddLoraDirective { Lora = lora });

        p.Loras.Should().HaveCount(1);
        p.Loras[0].Should().NotBeSameAs(lora);
        p.Loras[0].Name.Should().Be("L1");
    }

    [Fact]
    public void RemoveLoraDirective_RemovesByName_CaseInsensitive()
    {
        var p = new GenerationParameters();
        p.Loras.Add(new Lora { Name = "Alpha" });
        p.Loras.Add(new Lora { Name = "Beta" });

        _sut.Apply(p, new JobOutputConfig(), new RemoveLoraDirective { LoraName = "ALPHA" });

        p.Loras.Should().ContainSingle(l => l.Name == "Beta");
    }

    [Fact]
    public void ToggleLoraDirective_TogglesExistingLora()
    {
        var p = new GenerationParameters();
        p.Loras.Add(new Lora { Name = "L1", IsEnabled = true });
        _sut.Apply(p, new JobOutputConfig(), new ToggleLoraDirective { LoraName = "L1", Enable = false });
        p.Loras[0].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void SwapAssetDirective_OverwritesAsset()
    {
        var p = new GenerationParameters();
        p.Assets["Model"] = "old";
        _sut.Apply(p, new JobOutputConfig(), new SwapAssetDirective { AssetKey = "Model", AssetValue = "new" });
        p.Assets["Model"].Should().Be("new");
    }

    [Fact]
    public void SetOutputDirective_UpdatesOutputConfig()
    {
        var p = new GenerationParameters();
        var o = new JobOutputConfig { ProjectName = "Orig", FolderName = "OrigFolder" };
        _sut.Apply(p, o, new SetOutputDirective { ProjectName = "Override" });
        o.ProjectName.Should().Be("Override");
        o.FolderName.Should().Be("OrigFolder"); // untouched: directive only targets the project
    }

    [Fact]
    public void AddPromptStyleDirective_AppendsStyleTokens()
    {
        var p = new GenerationParameters();
        p.GetOrCreateFragment("prompts").Values["positive"] = "cat";
        _sut.Apply(p, new JobOutputConfig(), new AddPromptStyleDirective { StyleNames = new() { "anime", "detailed" } });
        p.GetFragment("prompts")!.Values["positive"].Should().Be("cat, anime, detailed");
    }
}
