using BlazorWebApp.Models;
using FluentAssertions;
using Xunit;

namespace BlazorWebApp.Tests.Models;

public class ResolutionSelectorMathTests
{
    [Fact]
    public void Calculate_Square_OneMegapixel_ShouldReturn1024x1024()
    {
        var (width, height) = ResolutionSelectorMath.Calculate("1:1 (Square)", 1.0, 8);

        width.Should().Be(1024);
        height.Should().Be(1024);
    }

    [Fact]
    public void Calculate_Widescreen_ShouldPreserveApproximateAspectRatio()
    {
        var (width, height) = ResolutionSelectorMath.Calculate("16:9 (Widescreen)", 1.0, 8);

        width.Should().BeGreaterThan(height);
        ((double)width / height).Should().BeApproximately(16.0 / 9.0, 0.05);
    }

    [Fact]
    public void Calculate_PortraitRatio_ShouldReturnTallerThanWide()
    {
        var (width, height) = ResolutionSelectorMath.Calculate("9:16 (Portrait Widescreen)", 1.0, 8);

        height.Should().BeGreaterThan(width);
    }

    [Fact]
    public void Calculate_RotatedAspectRatio_ShouldSwapBothDimensions()
    {
        var portrait = ResolutionSelectorMath.Calculate("9:16 (Portrait Widescreen)", 1.0, 8);
        var landscape = ResolutionSelectorMath.Calculate("16:9 (Widescreen)", 1.0, 8);

        landscape.Width.Should().Be(portrait.Height);
        landscape.Height.Should().Be(portrait.Width);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(64)]
    public void Calculate_ShouldRoundResultToRequestedMultiple(int multiple)
    {
        var (width, height) = ResolutionSelectorMath.Calculate("3:2 (Photo)", 2.0, multiple);

        (width % multiple).Should().Be(0);
        (height % multiple).Should().Be(0);
    }

    [Fact]
    public void Calculate_UnknownAspectRatio_ShouldFallBackToSquare()
    {
        var (width, height) = ResolutionSelectorMath.Calculate("bogus", 1.0, 8);

        width.Should().Be(height);
    }

    [Fact]
    public void Calculate_HigherMegapixels_ShouldProduceLargerDimensions()
    {
        var (smallWidth, smallHeight) = ResolutionSelectorMath.Calculate("1:1 (Square)", 1.0, 8);
        var (largeWidth, largeHeight) = ResolutionSelectorMath.Calculate("1:1 (Square)", 4.0, 8);

        largeWidth.Should().BeGreaterThan(smallWidth);
        largeHeight.Should().BeGreaterThan(smallHeight);
    }
}
