using BlazorWebApp.Services;
using FluentAssertions;

namespace BlazorWebApp.Tests.Services;

public class HuggingFacePathResolverTests
{
    private static readonly string Root = Path.GetFullPath(@"C:\models\checkpoints");

    // -----------------------------------------------------------------------
    // Resolve — keep-folder-structure mode
    // -----------------------------------------------------------------------

    [Fact]
    public void Resolve_KeepFolderStructure_PreservesSubfolder()
    {
        var result = HuggingFacePathResolver.Resolve(Root, "unet/model.safetensors", keepFolderStructure: true);
        result.Should().Be(Path.Combine(Root, "unet", "model.safetensors"));
    }

    [Fact]
    public void Resolve_KeepFolderStructure_NestedPath()
    {
        var result = HuggingFacePathResolver.Resolve(Root, "transformer/blocks/weights.bin", keepFolderStructure: true);
        result.Should().Be(Path.Combine(Root, "transformer", "blocks", "weights.bin"));
    }

    // -----------------------------------------------------------------------
    // Resolve — flat mode
    // -----------------------------------------------------------------------

    [Fact]
    public void Resolve_FlatMode_StripsSubfolder()
    {
        var result = HuggingFacePathResolver.Resolve(Root, "unet/model.safetensors", keepFolderStructure: false);
        result.Should().Be(Path.Combine(Root, "model.safetensors"));
    }

    [Fact]
    public void Resolve_FlatMode_TopLevelFile_Unchanged()
    {
        var result = HuggingFacePathResolver.Resolve(Root, "model.safetensors", keepFolderStructure: false);
        result.Should().Be(Path.Combine(Root, "model.safetensors"));
    }

    // -----------------------------------------------------------------------
    // Traversal rejection
    // -----------------------------------------------------------------------

    [Fact]
    public void Resolve_WithDotDotSegment_ThrowsArgumentException()
    {
        var act = () => HuggingFacePathResolver.Resolve(Root, "../etc/passwd", keepFolderStructure: true);
        act.Should().Throw<ArgumentException>().WithMessage("*traversal*");
    }

    [Fact]
    public void Resolve_WithMiddleDotDotSegment_ThrowsArgumentException()
    {
        var act = () => HuggingFacePathResolver.Resolve(Root, "unet/../../../etc/passwd", keepFolderStructure: true);
        act.Should().Throw<ArgumentException>().WithMessage("*traversal*");
    }

    // -----------------------------------------------------------------------
    // ResolveDbFilename
    // -----------------------------------------------------------------------

    [Fact]
    public void ResolveDbFilename_KeepFolderStructure_ContainsBothParts()
    {
        var name = HuggingFacePathResolver.ResolveDbFilename("unet/model.safetensors", keepFolderStructure: true);
        name.Should().Contain("model.safetensors");
        name.Should().Contain("unet");
    }

    [Fact]
    public void ResolveDbFilename_Flat_ReturnsBareFilename()
    {
        var name = HuggingFacePathResolver.ResolveDbFilename("unet/model.safetensors", keepFolderStructure: false);
        name.Should().Be("model.safetensors");
    }

    // -----------------------------------------------------------------------
    // Flat collision detection
    // -----------------------------------------------------------------------

    [Fact]
    public void DetectFlatCollisions_UniqueFilenames_ReturnsEmpty()
    {
        var paths = new[] { "unet/a.bin", "text_encoder/b.bin" };
        var collisions = HuggingFacePathResolver.DetectFlatCollisions(paths);
        collisions.Should().BeEmpty();
    }

    [Fact]
    public void DetectFlatCollisions_DuplicateName_ReturnsThatName()
    {
        var paths = new[] { "unet/model.bin", "vae/model.bin" };
        var collisions = HuggingFacePathResolver.DetectFlatCollisions(paths);
        collisions.Should().ContainSingle().Which.Should().Be("model.bin");
    }

    [Fact]
    public void DetectFlatCollisions_CaseInsensitive()
    {
        var paths = new[] { "unet/MODEL.BIN", "vae/model.bin" };
        var collisions = HuggingFacePathResolver.DetectFlatCollisions(paths);
        collisions.Should().ContainSingle();
    }

    [Fact]
    public void CheckFlatCollisions_HasCollisions_IsTrue()
    {
        var paths = new[] { "a/model.bin", "b/model.bin" };
        var result = HuggingFacePathResolver.CheckFlatCollisions(paths);
        result.HasCollisions.Should().BeTrue();
    }

    [Fact]
    public void CheckFlatCollisions_CollidingPaths_ContainsBothOriginalPaths()
    {
        var paths = new[] { "a/model.bin", "b/model.bin", "c/other.bin" };
        var result = HuggingFacePathResolver.CheckFlatCollisions(paths);
        result.CollidingPaths.Should().HaveCount(2);
        result.CollidingPaths.Should().NotContain("c/other.bin");
    }

    [Fact]
    public void CheckFlatCollisions_NoCollisions_HasCollisionsFalse()
    {
        var paths = new[] { "a/model.bin", "b/weights.bin" };
        var result = HuggingFacePathResolver.CheckFlatCollisions(paths);
        result.HasCollisions.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // NormalizeRepoPath
    // -----------------------------------------------------------------------

    [Fact]
    public void NormalizeRepoPath_LeadingSlash_IsStripped()
    {
        var norm = HuggingFacePathResolver.NormalizeRepoPath("/unet/model.bin");
        norm.Should().NotStartWith("/");
        norm.Should().NotStartWith("\\");
    }

    [Fact]
    public void NormalizeRepoPath_ForwardSlashes_NormalizedToOsSeparator()
    {
        var norm = HuggingFacePathResolver.NormalizeRepoPath("a/b/c.bin");
        norm.Should().NotContain("/");
    }
}
