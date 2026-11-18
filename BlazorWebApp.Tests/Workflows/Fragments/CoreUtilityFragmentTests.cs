using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace BlazorWebApp.Tests.Workflows.Fragments;

/// <summary>
/// Tests for the shared utility fragments created in Phase 6 Step 1.
/// </summary>
public class CoreUtilityFragmentTests
{
    #region CleanVramFragment

    [Fact]
    public void CleanVram_Build_ShouldCreateNodeWithCorrectClassType()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("latent_output", "some_sampler", 0);

        new CleanVramFragment().Build(builder, registry, new CleanVramFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("clean_vram", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("easy cleanGpuUsed");
    }

    [Fact]
    public void CleanVram_Build_ShouldRegisterOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("latent_output", "some_sampler", 0);

        new CleanVramFragment().Build(builder, registry, new CleanVramFragment.Parameters());

        registry.HasOutput("cleaned_latent_output").Should().BeTrue();
    }

    [Fact]
    public void CleanVram_Build_WithCustomNames_ShouldUseCustomInputOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("my_input", "node_a", 0);

        new CleanVramFragment().Build(builder, registry, new CleanVramFragment.Parameters
        {
            NodeId = "my_clean",
            InputName = "my_input",
            OutputName = "my_output"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("my_clean", out _).Should().BeTrue();
        registry.HasOutput("my_output").Should().BeTrue();
    }

    #endregion

    #region SaveVideoFragment

    [Fact]
    public void SaveVideo_Build_ShouldCreateVHSVideoCombineNode()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("image_output", "vae_decoder", 0);

        new SaveVideoFragment().Build(builder, registry, new SaveVideoFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("video_save", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("VHS_VideoCombine");
    }

    [Fact]
    public void SaveVideo_Build_ShouldSetFrameRate()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("image_output", "vae_decoder", 0);

        new SaveVideoFragment().Build(builder, registry, new SaveVideoFragment.Parameters { FrameRate = 24 });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("video_save").GetProperty("inputs");
        inputs.GetProperty("frame_rate").GetInt32().Should().Be(24);
    }

    [Fact]
    public void SaveVideo_Build_ShouldReferenceCorrectImageInput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("frames_output", "rife_node", 0);

        new SaveVideoFragment().Build(builder, registry, new SaveVideoFragment.Parameters
        {
            ImageInputName = "frames_output"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("video_save").GetProperty("inputs");
        var imagesRef = inputs.GetProperty("images");
        imagesRef[0].GetString().Should().Be("rife_node");
        imagesRef[1].GetInt32().Should().Be(0);
    }

    #endregion

    #region LoadVideoFragment

    [Fact]
    public void LoadVideo_Build_ShouldCreateVHSLoadVideoNode()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadVideoFragment().Build(builder, registry, new LoadVideoFragment.Parameters
        {
            Video = "test.mp4"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("load_video", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("VHS_LoadVideo");
    }

    [Fact]
    public void LoadVideo_Build_ShouldRegisterThreeOutputs()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadVideoFragment().Build(builder, registry, new LoadVideoFragment.Parameters
        {
            Video = "test.mp4"
        });

        registry.HasOutput("video_frames").Should().BeTrue();
        registry.HasOutput("frame_count").Should().BeTrue();
        registry.HasOutput("audio").Should().BeTrue();
    }

    [Fact]
    public void LoadVideo_Build_ShouldSetVideoParameters()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadVideoFragment().Build(builder, registry, new LoadVideoFragment.Parameters
        {
            Video = "dance.mp4",
            ForceRate = 24,
            CustomWidth = 720,
            CustomHeight = 1280,
            FrameLoadCap = 200
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("load_video").GetProperty("inputs");
        inputs.GetProperty("video").GetString().Should().Be("dance.mp4");
        inputs.GetProperty("force_rate").GetInt32().Should().Be(24);
        inputs.GetProperty("custom_width").GetInt32().Should().Be(720);
        inputs.GetProperty("custom_height").GetInt32().Should().Be(1280);
        inputs.GetProperty("frame_load_cap").GetInt32().Should().Be(200);
    }

    #endregion

    #region GetImageSizeFragment

    [Fact]
    public void GetImageSize_Build_ShouldCreateNode()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("video_frames", "load_video", 0);

        new GetImageSizeFragment().Build(builder, registry, new GetImageSizeFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("get_size", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("GetImageSizeAndCount");
    }

    [Fact]
    public void GetImageSize_Build_ShouldRegisterFourOutputs()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("video_frames", "load_video", 0);

        new GetImageSizeFragment().Build(builder, registry, new GetImageSizeFragment.Parameters());

        registry.HasOutput("image_size_info").Should().BeTrue();
        registry.HasOutput("width").Should().BeTrue();
        registry.HasOutput("height").Should().BeTrue();
        registry.HasOutput("num_frames").Should().BeTrue();
    }

    [Fact]
    public void GetImageSize_Build_ShouldReferenceCorrectImageInput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("my_frames", "some_loader", 0);

        new GetImageSizeFragment().Build(builder, registry, new GetImageSizeFragment.Parameters
        {
            ImageRefName = "my_frames"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("get_size").GetProperty("inputs");
        var imageRef = inputs.GetProperty("image");
        imageRef[0].GetString().Should().Be("some_loader");
    }

    #endregion

    #region LoadImageFragment

    [Fact]
    public void LoadImage_Build_ShouldCreateLoadImageNode()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadImageFragment().Build(builder, registry, new LoadImageFragment.Parameters
        {
            Image = "ref.png"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("load_image", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("LoadImage");
    }

    [Fact]
    public void LoadImage_Build_ShouldRegisterOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadImageFragment().Build(builder, registry, new LoadImageFragment.Parameters
        {
            Image = "ref.png"
        });

        registry.HasOutput("image_input").Should().BeTrue();
    }

    [Fact]
    public void LoadImage_Build_WithCustomOutputName_ShouldRegisterCustomOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadImageFragment().Build(builder, registry, new LoadImageFragment.Parameters
        {
            Image = "ref.png",
            OutputName = "reference_image"
        });

        registry.HasOutput("reference_image").Should().BeTrue();
    }

    #endregion

    #region ResizeImageFragment

    [Fact]
    public void ResizeImage_Build_ShouldCreateImageResizeKJv2Node()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("image_input", "load_image", 0);
        registry.Register("width", "get_size", 1);
        registry.Register("height", "get_size", 2);

        new ResizeImageFragment().Build(builder, registry, new ResizeImageFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("resize_image", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("ImageResizeKJv2");
    }

    [Fact]
    public void ResizeImage_Build_ShouldRegisterOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("image_input", "load_image", 0);
        registry.Register("width", "get_size", 1);
        registry.Register("height", "get_size", 2);

        new ResizeImageFragment().Build(builder, registry, new ResizeImageFragment.Parameters());

        registry.HasOutput("resized_image").Should().BeTrue();
    }

    [Fact]
    public void ResizeImage_Build_ShouldReferenceWidthHeightFromRegistry()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("image_input", "load_image", 0);
        registry.Register("width", "get_size", 1);
        registry.Register("height", "get_size", 2);

        new ResizeImageFragment().Build(builder, registry, new ResizeImageFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("resize_image").GetProperty("inputs");
        inputs.GetProperty("width")[0].GetString().Should().Be("get_size");
        inputs.GetProperty("width")[1].GetInt32().Should().Be(1);
        inputs.GetProperty("height")[0].GetString().Should().Be("get_size");
        inputs.GetProperty("height")[1].GetInt32().Should().Be(2);
    }

    #endregion

    #region LoadClipVisionFragment

    [Fact]
    public void LoadClipVision_Build_ShouldCreateCLIPVisionLoaderNode()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadClipVisionFragment().Build(builder, registry, new LoadClipVisionFragment.Parameters
        {
            ClipVisionName = "clip_vision_h.safetensors"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("clip_vision_loader", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("CLIPVisionLoader");
    }

    [Fact]
    public void LoadClipVision_Build_ShouldRegisterOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadClipVisionFragment().Build(builder, registry, new LoadClipVisionFragment.Parameters());

        registry.HasOutput("clip_vision_output").Should().BeTrue();
    }

    [Fact]
    public void LoadClipVision_Build_WithScope_ShouldPrefixNodeAndOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadClipVisionFragment().Build(builder, registry, new LoadClipVisionFragment.Parameters(), scope: "sd_");

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("sd_clip_vision_loader", out _).Should().BeTrue();
        registry.HasOutput("sd_clip_vision_output").Should().BeTrue();
    }

    #endregion
}
