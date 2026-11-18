using BlazorWebApp.Models;
using FluentAssertions;
using Xunit;

namespace BlazorWebApp.Tests.Models;

public class SourceAssetTests
{
    [Fact]
    public void Clone_ShouldPreserveMaskFields()
    {
        var source = new SourceAsset
        {
            Data = "image.png",
            MaskData = "data:image/png;base64,mask",
            MaskFilename = "mask.png",
            MaskFilePath = "C:/temp/mask.png"
        };

        var clone = source.Clone();

        clone.MaskData.Should().Be(source.MaskData);
        clone.MaskFilename.Should().Be(source.MaskFilename);
        clone.MaskFilePath.Should().Be(source.MaskFilePath);
        clone.HasMask.Should().BeTrue();
    }

    [Fact]
    public void Clear_ShouldRemoveMaskFields()
    {
        var source = new SourceAsset
        {
            Data = "image.png",
            MaskData = "data:image/png;base64,mask",
            MaskFilename = "mask.png",
            MaskFilePath = "C:/temp/mask.png"
        };

        source.Clear();

        source.MaskData.Should().BeNull();
        source.MaskFilename.Should().BeNull();
        source.MaskFilePath.Should().BeNull();
        source.HasMask.Should().BeFalse();
    }
}