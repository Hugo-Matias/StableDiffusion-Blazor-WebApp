using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Models;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace BlazorWebApp.Tests.Workflows.Fragments;

/// <summary>
/// Tests for the Img2Img shared fragments (LoadImageScaled, VaeEncode).
/// </summary>
public class Img2ImgFragmentTests
{
    #region LoadImageScaledFragment

    [Fact]
    public void LoadImageScaled_Build_ShouldCreateLoadImageNode()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadImageScaledFragment().Build(builder, registry, new LoadImageScaledFragment.Parameters
        {
            Image = "test_image.png"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("image_loader", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("LoadImage");
        node.GetProperty("inputs").GetProperty("image").GetString().Should().Be("test_image.png");
    }

    [Fact]
    public void LoadImageScaled_Build_ShouldCreateScaleNode()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadImageScaledFragment().Build(builder, registry, new LoadImageScaledFragment.Parameters
        {
            Image = "test.png",
            UpscaleMethod = "nearest-exact",
            Megapixels = 2.0
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("image_scale", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("ImageScaleToTotalPixels");

        var inputs = node.GetProperty("inputs");
        inputs.GetProperty("upscale_method").GetString().Should().Be("nearest-exact");
        inputs.GetProperty("megapixels").GetDouble().Should().Be(2.0);
    }

    [Fact]
    public void LoadImageScaled_Build_ShouldChainImageToScale()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadImageScaledFragment().Build(builder, registry, new LoadImageScaledFragment.Parameters
        {
            Image = "test.png"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var scaleInputs = json.RootElement.GetProperty("image_scale").GetProperty("inputs");
        var imageRef = scaleInputs.GetProperty("image");
        imageRef[0].GetString().Should().Be("image_loader");
        imageRef[1].GetInt32().Should().Be(0);
    }

    [Fact]
    public void LoadImageScaled_Build_ShouldRegisterImageInput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadImageScaledFragment().Build(builder, registry, new LoadImageScaledFragment.Parameters
        {
            Image = "test.png"
        });

        registry.HasOutput("image_input").Should().BeTrue();
        var (nodeId, index) = registry.GetRef("image_input");
        nodeId.Should().Be("image_scale");
        index.Should().Be(0);
    }

    [Fact]
    public void LoadImageScaled_Build_ShouldUseDefaultValues()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadImageScaledFragment().Build(builder, registry, new LoadImageScaledFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("image_scale").GetProperty("inputs");
        inputs.GetProperty("upscale_method").GetString().Should().Be("lanczos");
        inputs.GetProperty("megapixels").GetDouble().Should().Be(1.0);
    }

    [Fact]
    public void LoadImageScaled_Metadata_ShouldBeHiddenInputType()
    {
        var fragment = new LoadImageScaledFragment();
        fragment.Metadata.Id.Should().Be("load_image_scaled");
        fragment.Metadata.Type.Should().Be(FragmentType.Input);
        fragment.Metadata.IsHidden.Should().BeTrue();
    }

    #endregion

    #region VaeEncodeFragment

    [Fact]
    public void VaeEncode_Build_ShouldCreateVaeEncodeNode()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("image_input", "image_scale", 0);
        registry.Register("vae_output", "vae_loader", 0);

        new VaeEncodeFragment().Build(builder, registry, new VaeEncodeFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("vae_encoder", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("VAEEncode");
    }

    [Fact]
    public void VaeEncode_Build_ShouldReferenceImageInput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("image_input", "image_scale", 0);
        registry.Register("vae_output", "vae_loader", 0);

        new VaeEncodeFragment().Build(builder, registry, new VaeEncodeFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("vae_encoder").GetProperty("inputs");
        var pixelsRef = inputs.GetProperty("pixels");
        pixelsRef[0].GetString().Should().Be("image_scale");
        pixelsRef[1].GetInt32().Should().Be(0);
    }

    [Fact]
    public void VaeEncode_Build_ShouldReferenceVaeOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("image_input", "image_scale", 0);
        registry.Register("vae_output", "vae_loader", 0);

        new VaeEncodeFragment().Build(builder, registry, new VaeEncodeFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("vae_encoder").GetProperty("inputs");
        var vaeRef = inputs.GetProperty("vae");
        vaeRef[0].GetString().Should().Be("vae_loader");
        vaeRef[1].GetInt32().Should().Be(0);
    }

    [Fact]
    public void VaeEncode_Build_ShouldRegisterLatentOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("image_input", "image_scale", 0);
        registry.Register("vae_output", "vae_loader", 0);

        new VaeEncodeFragment().Build(builder, registry, new VaeEncodeFragment.Parameters());

        registry.HasOutput("latent_output").Should().BeTrue();
        var (nodeId, index) = registry.GetRef("latent_output");
        nodeId.Should().Be("vae_encoder");
        index.Should().Be(0);
    }

    [Fact]
    public void VaeEncode_Build_WithCustomImageInput_ShouldUseCustomRef()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("my_custom_image", "custom_loader", 0);
        registry.Register("vae_output", "vae_loader", 0);

        new VaeEncodeFragment().Build(builder, registry, new VaeEncodeFragment.Parameters
        {
            ImageInputName = "my_custom_image"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("vae_encoder").GetProperty("inputs");
        var pixelsRef = inputs.GetProperty("pixels");
        pixelsRef[0].GetString().Should().Be("custom_loader");
    }

    [Fact]
    public void VaeEncode_Build_WithScope_ShouldUseScopedVae()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("image_input", "image_scale", 0);
        registry.Register("detailer_vae_output", "detailer_vae_loader", 0);

        new VaeEncodeFragment().Build(builder, registry, new VaeEncodeFragment.Parameters
        {
            Scope = "detailer_"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("vae_encoder").GetProperty("inputs");
        var vaeRef = inputs.GetProperty("vae");
        vaeRef[0].GetString().Should().Be("detailer_vae_loader");
    }

    [Fact]
    public void VaeEncode_Metadata_ShouldBeHiddenLatentType()
    {
        var fragment = new VaeEncodeFragment();
        fragment.Metadata.Id.Should().Be("vae_encode");
        fragment.Metadata.Type.Should().Be(FragmentType.Latent);
        fragment.Metadata.IsHidden.Should().BeTrue();
    }

    #endregion

    #region Integration: LoadImageScaled -> VaeEncode Chain

    [Fact]
    public void LoadImageScaled_ThenVaeEncode_ShouldChainCorrectly()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        // Simulate a VAE loader registration (from LoadDiffusion)
        registry.Register("vae_output", "vae_loader", 0);

        // Load and scale image
        new LoadImageScaledFragment().Build(builder, registry, new LoadImageScaledFragment.Parameters
        {
            Image = "source.png",
            Megapixels = 1.5
        });

        // Encode to latent
        new VaeEncodeFragment().Build(builder, registry, new VaeEncodeFragment.Parameters());

        // Verify chain: image_loader -> image_scale -> vae_encoder
        var json = JsonDocument.Parse(builder.ToJson());

        // image_scale references image_loader
        var scaleInputs = json.RootElement.GetProperty("image_scale").GetProperty("inputs");
        scaleInputs.GetProperty("image")[0].GetString().Should().Be("image_loader");

        // vae_encoder references image_scale (via image_input)
        var encodeInputs = json.RootElement.GetProperty("vae_encoder").GetProperty("inputs");
        encodeInputs.GetProperty("pixels")[0].GetString().Should().Be("image_scale");
        encodeInputs.GetProperty("vae")[0].GetString().Should().Be("vae_loader");

        // Final output is latent_output
        registry.HasOutput("latent_output").Should().BeTrue();
        registry.GetRef("latent_output").nodeId.Should().Be("vae_encoder");
    }

    #endregion
}
