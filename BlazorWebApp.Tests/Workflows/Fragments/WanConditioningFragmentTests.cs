using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Wan;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace BlazorWebApp.Tests.Workflows.Fragments;

/// <summary>
/// Tests for Img2Vid conditioning fragments (Phase 6 Step 3).
/// </summary>
public class WanConditioningFragmentTests
{
    #region WanLoadImageFragment

    [Fact]
    public void WanLoadImage_Build_ShouldCreateTwoNodes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new WanLoadImageFragment().Build(builder, registry, new WanLoadImageFragment.Parameters
        {
            ImagePath = "test_image.png",
            Width = 768,
            Height = 768
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("load_image", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("image_resize", out _).Should().BeTrue();
    }

    [Fact]
    public void WanLoadImage_Build_ShouldSetCorrectClassTypes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new WanLoadImageFragment().Build(builder, registry, new WanLoadImageFragment.Parameters
        {
            ImagePath = "test.png"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("load_image").GetProperty("class_type").GetString().Should().Be("LoadImage");
        json.RootElement.GetProperty("image_resize").GetProperty("class_type").GetString().Should().Be("ImageResizeKJv2");
    }

    [Fact]
    public void WanLoadImage_Build_ShouldChainResizeToLoadImage()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new WanLoadImageFragment().Build(builder, registry, new WanLoadImageFragment.Parameters
        {
            ImagePath = "test.png"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var resizeInputs = json.RootElement.GetProperty("image_resize").GetProperty("inputs");
        resizeInputs.GetProperty("image")[0].GetString().Should().Be("load_image");
        resizeInputs.GetProperty("image")[1].GetInt32().Should().Be(0);
    }

    [Fact]
    public void WanLoadImage_Build_ShouldRegisterFourOutputs()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new WanLoadImageFragment().Build(builder, registry, new WanLoadImageFragment.Parameters
        {
            ImagePath = "test.png"
        });

        registry.HasOutput("original_image_output").Should().BeTrue();
        registry.HasOutput("image_output").Should().BeTrue();
        registry.HasOutput("image_width_output").Should().BeTrue();
        registry.HasOutput("image_height_output").Should().BeTrue();

        // original_image_output -> load_image, index 0
        var (origNode, origIdx) = registry.GetRef("original_image_output");
        origNode.Should().Be("load_image");
        origIdx.Should().Be(0);

        // image_output -> image_resize, index 0
        var (imgNode, imgIdx) = registry.GetRef("image_output");
        imgNode.Should().Be("image_resize");
        imgIdx.Should().Be(0);

        // width -> image_resize, index 1
        var (widthNode, widthIdx) = registry.GetRef("image_width_output");
        widthNode.Should().Be("image_resize");
        widthIdx.Should().Be(1);

        // height -> image_resize, index 2
        var (heightNode, heightIdx) = registry.GetRef("image_height_output");
        heightNode.Should().Be("image_resize");
        heightIdx.Should().Be(2);
    }

    [Fact]
    public void WanLoadImage_Build_ShouldSetResizeParameters()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new WanLoadImageFragment().Build(builder, registry, new WanLoadImageFragment.Parameters
        {
            ImagePath = "test.png",
            Width = 1024,
            Height = 576
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("image_resize").GetProperty("inputs");
        inputs.GetProperty("width").GetInt32().Should().Be(1024);
        inputs.GetProperty("height").GetInt32().Should().Be(576);
        inputs.GetProperty("upscale_method").GetString().Should().Be("lanczos");
        inputs.GetProperty("keep_proportion").GetString().Should().Be("resize");
        inputs.GetProperty("divisible_by").GetInt32().Should().Be(16);
    }

    [Fact]
    public void WanLoadImage_Metadata_ShouldHaveUIParameters()
    {
        var fragment = new WanLoadImageFragment();
        fragment.Metadata.Component.Should().Be("LatentForm");
        fragment.Metadata.Order.Should().Be(30);
        fragment.Metadata.Parameters.Should().HaveCount(3);
        fragment.Metadata.IsHidden.Should().BeFalse();
    }

    #endregion

    #region ClipVisionEncodeFragment

    [Fact]
    public void ClipVisionEncode_Build_ShouldCreateTwoNodes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("original_image_output", "load_image", 0);

        new ClipVisionEncodeFragment().Build(builder, registry, new ClipVisionEncodeFragment.Parameters
        {
            ClipVisionName = "clip_vision_h.safetensors"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("clip_vision_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("clip_vision_encode", out _).Should().BeTrue();
    }

    [Fact]
    public void ClipVisionEncode_Build_ShouldChainCorrectly()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("original_image_output", "load_image", 0);

        new ClipVisionEncodeFragment().Build(builder, registry, new ClipVisionEncodeFragment.Parameters
        {
            ClipVisionName = "clip_vision_h.safetensors"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var encodeInputs = json.RootElement.GetProperty("clip_vision_encode").GetProperty("inputs");

        // clip_vision -> clip_vision_loader
        encodeInputs.GetProperty("clip_vision")[0].GetString().Should().Be("clip_vision_loader");
        encodeInputs.GetProperty("clip_vision")[1].GetInt32().Should().Be(0);

        // image -> original_image_output (NOT image_output)
        encodeInputs.GetProperty("image")[0].GetString().Should().Be("load_image");
        encodeInputs.GetProperty("image")[1].GetInt32().Should().Be(0);
    }

    [Fact]
    public void ClipVisionEncode_Build_ShouldRegisterOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("original_image_output", "load_image", 0);

        new ClipVisionEncodeFragment().Build(builder, registry, new ClipVisionEncodeFragment.Parameters
        {
            ClipVisionName = "clip_vision_h.safetensors"
        });

        registry.HasOutput("clip_vision_output").Should().BeTrue();
        var (nodeId, index) = registry.GetRef("clip_vision_output");
        nodeId.Should().Be("clip_vision_encode");
        index.Should().Be(0);
    }

    [Fact]
    public void ClipVisionEncode_Build_ShouldSetCropNone()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("original_image_output", "load_image", 0);

        new ClipVisionEncodeFragment().Build(builder, registry, new ClipVisionEncodeFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("clip_vision_encode").GetProperty("inputs")
            .GetProperty("crop").GetString().Should().Be("none");
    }

    [Fact]
    public void ClipVisionEncode_Build_ShouldBeHidden()
    {
        var fragment = new ClipVisionEncodeFragment();
        fragment.Metadata.IsHidden.Should().BeTrue();
    }

    #endregion

    #region WanPromptsFragment

    [Fact]
    public void WanPrompts_Build_ShouldCreateTwoNodes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("clip_output", "clip_loader", 0);

        new WanPromptsFragment().Build(builder, registry, new WanPromptsFragment.Parameters
        {
            Positive = "a beautiful scene",
            Negative = "ugly, blurry"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("positive_encode", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("negative_encode", out _).Should().BeTrue();
    }

    [Fact]
    public void WanPrompts_Build_ShouldReferenceClipOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("clip_output", "clip_loader", 0);

        new WanPromptsFragment().Build(builder, registry, new WanPromptsFragment.Parameters
        {
            Positive = "test",
            Negative = "test"
        });

        var json = JsonDocument.Parse(builder.ToJson());

        var positiveInputs = json.RootElement.GetProperty("positive_encode").GetProperty("inputs");
        positiveInputs.GetProperty("clip")[0].GetString().Should().Be("clip_loader");

        var negativeInputs = json.RootElement.GetProperty("negative_encode").GetProperty("inputs");
        negativeInputs.GetProperty("clip")[0].GetString().Should().Be("clip_loader");
    }

    [Fact]
    public void WanPrompts_Build_ShouldSetPromptText()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("clip_output", "clip_loader", 0);

        new WanPromptsFragment().Build(builder, registry, new WanPromptsFragment.Parameters
        {
            Positive = "a beautiful sunset",
            Negative = "ugly, blurry"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("positive_encode").GetProperty("inputs")
            .GetProperty("text").GetString().Should().Be("a beautiful sunset");
        json.RootElement.GetProperty("negative_encode").GetProperty("inputs")
            .GetProperty("text").GetString().Should().Be("ugly, blurry");
    }

    [Fact]
    public void WanPrompts_Build_ShouldRegisterBothOutputs()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("clip_output", "clip_loader", 0);

        new WanPromptsFragment().Build(builder, registry, new WanPromptsFragment.Parameters
        {
            Positive = "test",
            Negative = "test"
        });

        registry.HasOutput("positive_output").Should().BeTrue();
        registry.HasOutput("negative_output").Should().BeTrue();

        var (posNode, _) = registry.GetRef("positive_output");
        posNode.Should().Be("positive_encode");
        var (negNode, _) = registry.GetRef("negative_output");
        negNode.Should().Be("negative_encode");
    }

    [Fact]
    public void WanPrompts_Build_ShouldSetCorrectClassType()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("clip_output", "clip_loader", 0);

        new WanPromptsFragment().Build(builder, registry, new WanPromptsFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("positive_encode").GetProperty("class_type").GetString().Should().Be("CLIPTextEncode");
        json.RootElement.GetProperty("negative_encode").GetProperty("class_type").GetString().Should().Be("CLIPTextEncode");
    }

    #endregion

    #region PainterI2VFragment

    [Fact]
    public void PainterI2V_Build_ShouldCreateSingleNode()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = SetupPainterI2VRegistry();

        new PainterI2VFragment().Build(builder, registry, new PainterI2VFragment.Parameters
        {
            Length = 81,
            MotionAmplitude = 1.1
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("painter_i2v", out _).Should().BeTrue();
        builder.NodeCount.Should().Be(1);
    }

    [Fact]
    public void PainterI2V_Build_ShouldSetClassType()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = SetupPainterI2VRegistry();

        new PainterI2VFragment().Build(builder, registry, new PainterI2VFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("painter_i2v").GetProperty("class_type").GetString().Should().Be("PainterI2V");
    }

    [Fact]
    public void PainterI2V_Build_ShouldReferenceAllInputs()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = SetupPainterI2VRegistry();

        new PainterI2VFragment().Build(builder, registry, new PainterI2VFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("painter_i2v").GetProperty("inputs");

        // width -> image_resize, index 1
        inputs.GetProperty("width")[0].GetString().Should().Be("image_resize");
        inputs.GetProperty("width")[1].GetInt32().Should().Be(1);

        // height -> image_resize, index 2
        inputs.GetProperty("height")[0].GetString().Should().Be("image_resize");
        inputs.GetProperty("height")[1].GetInt32().Should().Be(2);

        // positive -> positive_encode, index 0
        inputs.GetProperty("positive")[0].GetString().Should().Be("positive_encode");

        // negative -> negative_encode, index 0
        inputs.GetProperty("negative")[0].GetString().Should().Be("negative_encode");

        // vae -> vae_loader, index 0
        inputs.GetProperty("vae")[0].GetString().Should().Be("vae_loader");

        // clip_vision_output -> clip_vision_encode, index 0
        inputs.GetProperty("clip_vision_output")[0].GetString().Should().Be("clip_vision_encode");

        // start_image -> image_resize, index 0
        inputs.GetProperty("start_image")[0].GetString().Should().Be("image_resize");
    }

    [Fact]
    public void PainterI2V_Build_ShouldSetVideoParameters()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = SetupPainterI2VRegistry();

        new PainterI2VFragment().Build(builder, registry, new PainterI2VFragment.Parameters
        {
            Length = 129,
            BatchSize = 2,
            MotionAmplitude = 1.5
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("painter_i2v").GetProperty("inputs");
        inputs.GetProperty("length").GetInt32().Should().Be(129);
        inputs.GetProperty("batch_size").GetInt32().Should().Be(2);
        inputs.GetProperty("motion_amplitude").GetDouble().Should().BeApproximately(1.5, 0.01);
    }

    [Fact]
    public void PainterI2V_Build_ShouldRegisterThreeOutputs()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = SetupPainterI2VRegistry();

        new PainterI2VFragment().Build(builder, registry, new PainterI2VFragment.Parameters());

        registry.HasOutput("painter_positive_output").Should().BeTrue();
        registry.HasOutput("painter_negative_output").Should().BeTrue();
        registry.HasOutput("painter_latent_output").Should().BeTrue();

        var (posNode, posIdx) = registry.GetRef("painter_positive_output");
        posNode.Should().Be("painter_i2v");
        posIdx.Should().Be(0);

        var (negNode, negIdx) = registry.GetRef("painter_negative_output");
        negNode.Should().Be("painter_i2v");
        negIdx.Should().Be(1);

        var (latNode, latIdx) = registry.GetRef("painter_latent_output");
        latNode.Should().Be("painter_i2v");
        latIdx.Should().Be(2);
    }

    [Fact]
    public void PainterI2V_Metadata_ShouldHaveUIConfiguration()
    {
        var fragment = new PainterI2VFragment();
        fragment.Metadata.Component.Should().Be("PainterI2VForm");
        fragment.Metadata.Icon.Should().Be("fa-solid fa-video");
        fragment.Metadata.Order.Should().Be(40);
        fragment.Metadata.Collapsible.Should().BeFalse();
        fragment.Metadata.IsHidden.Should().BeFalse();
        fragment.Metadata.Parameters.Should().HaveCount(3);
    }

    #endregion

    #region Integration: Full Conditioning Chain

    [Fact]
    public void FullConditioningChain_ShouldProduceValidJsonAndRegistry()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        // Simulate loader outputs
        registry.Register("clip_output", "clip_loader", 0);
        registry.Register("vae_output", "vae_loader", 0);

        // Step 1: Load image
        new WanLoadImageFragment().Build(builder, registry, new WanLoadImageFragment.Parameters
        {
            ImagePath = "source.png",
            Width = 768,
            Height = 768
        });

        // Step 2: CLIP Vision encode (uses original_image_output)
        new ClipVisionEncodeFragment().Build(builder, registry, new ClipVisionEncodeFragment.Parameters
        {
            ClipVisionName = "clip_vision_h.safetensors"
        });

        // Step 3: Prompts (uses clip_output)
        new WanPromptsFragment().Build(builder, registry, new WanPromptsFragment.Parameters
        {
            Positive = "a person walking",
            Negative = "ugly"
        });

        // Step 4: PainterI2V (uses all above)
        new PainterI2VFragment().Build(builder, registry, new PainterI2VFragment.Parameters
        {
            Length = 81,
            MotionAmplitude = 1.1
        });

        // Verify all expected outputs exist
        registry.HasOutput("original_image_output").Should().BeTrue();
        registry.HasOutput("image_output").Should().BeTrue();
        registry.HasOutput("image_width_output").Should().BeTrue();
        registry.HasOutput("image_height_output").Should().BeTrue();
        registry.HasOutput("clip_vision_output").Should().BeTrue();
        registry.HasOutput("positive_output").Should().BeTrue();
        registry.HasOutput("negative_output").Should().BeTrue();
        registry.HasOutput("painter_positive_output").Should().BeTrue();
        registry.HasOutput("painter_negative_output").Should().BeTrue();
        registry.HasOutput("painter_latent_output").Should().BeTrue();

        // 2 load image + 2 clip vision + 2 prompts + 1 painter = 7 nodes
        builder.NodeCount.Should().Be(7);

        // Verify JSON is valid
        var action = () => JsonDocument.Parse(builder.ToJson());
        action.Should().NotThrow();
    }

    #endregion

    #region Helpers

    private static NodeRegistry SetupPainterI2VRegistry()
    {
        var registry = new NodeRegistry();
        registry.Register("image_width_output", "image_resize", 1);
        registry.Register("image_height_output", "image_resize", 2);
        registry.Register("positive_output", "positive_encode", 0);
        registry.Register("negative_output", "negative_encode", 0);
        registry.Register("vae_output", "vae_loader", 0);
        registry.Register("clip_vision_output", "clip_vision_encode", 0);
        registry.Register("image_output", "image_resize", 0);
        return registry;
    }

    #endregion
}
