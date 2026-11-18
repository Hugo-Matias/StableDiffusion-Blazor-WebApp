using BlazorWebApp.Models;
using BlazorWebApp.Models;
using FluentAssertions;
using Xunit;

namespace BlazorWebApp.Tests.Models;

public class LoraTests
{
    [Fact]
    public void Clone_ShouldCopyAllProperties()
    {
        var original = new Lora
        {
            Name = "test",
            Path = "test.safetensors",
            Strength = 0.7f,
            HighPath = "high.safetensors",
            LowPath = "low.safetensors",
            IsEnabled = true,
            IsNegative = true
        };

        var clone = new Lora(original);

        clone.Name.Should().Be("test");
        clone.Path.Should().Be("test.safetensors");
        clone.Strength.Should().Be(0.7f);
        clone.HighPath.Should().Be("high.safetensors");
        clone.LowPath.Should().Be("low.safetensors");
        clone.IsEnabled.Should().BeTrue();
        clone.IsNegative.Should().BeTrue();
    }

    [Fact]
    public void IsDualModel_WithHighPathOnly_ShouldBeTrue()
    {
        var lora = new Lora { HighPath = "high.safetensors" };
        lora.IsDualModel.Should().BeTrue();
        lora.HasHighPath.Should().BeTrue();
        lora.HasLowPath.Should().BeFalse();
    }

    [Fact]
    public void IsDualModel_WithLowPathOnly_ShouldBeTrue()
    {
        var lora = new Lora { LowPath = "low.safetensors" };
        lora.IsDualModel.Should().BeTrue();
        lora.HasHighPath.Should().BeFalse();
        lora.HasLowPath.Should().BeTrue();
    }

    [Fact]
    public void IsDualModel_WithBothPaths_ShouldBeTrue()
    {
        var lora = new Lora { HighPath = "high.safetensors", LowPath = "low.safetensors" };
        lora.IsDualModel.Should().BeTrue();
    }

    [Fact]
    public void IsDualModel_WithNoPaths_ShouldBeFalse()
    {
        var lora = new Lora();
        lora.IsDualModel.Should().BeFalse();
    }
}
