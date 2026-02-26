using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Templates.Flux;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace BlazorWebApp.Tests.Workflows;

/// <summary>
/// Tests for the FluxTxt2ImgWorkflow C# implementation.
/// Verifies the workflow generates valid ComfyUI JSON.
/// </summary>
public class FluxTxt2ImgWorkflowTests
{
    private readonly FluxTxt2ImgWorkflow _workflow;

    public FluxTxt2ImgWorkflowTests()
    {
        _workflow = new FluxTxt2ImgWorkflow();
    }

    #region Metadata Tests

    [Fact]
    public void Metadata_ShouldHaveCorrectId()
    {
        // Assert
        _workflow.Metadata.Id.Should().Be(Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f23456789012"));
    }

    [Fact]
    public void Metadata_ShouldHaveCorrectTitle()
    {
        // Assert
        _workflow.Metadata.Title.Should().Be("Txt2Img");
    }

    [Fact]
    public void Metadata_ShouldHaveFluxBase()
    {
        // Assert
        _workflow.Metadata.Base.Should().Be(Data.Enums.ModelBase.Flux);
    }

    [Fact]
    public void Metadata_ShouldHaveCorrectMode()
    {
        // Assert
        _workflow.Metadata.Mode.Should().Be(Data.Entities.ModeType.Txt2Img);
    }

    [Fact]
    public void Metadata_ShouldHaveFourAssets()
    {
        // Assert - Flux requires Model, Clip1, Clip2, VAE
        _workflow.Metadata.Assets.Should().HaveCount(4);
    }

    [Fact]
    public void Metadata_Assets_ShouldHaveCorrectParameters()
    {
        // Assert
        var assets = _workflow.Metadata.Assets.ToList();
        
        assets.Should().Contain(a => a.Parameter == "Model");
        assets.Should().Contain(a => a.Parameter == "Clip1");
        assets.Should().Contain(a => a.Parameter == "Clip2");
        assets.Should().Contain(a => a.Parameter == "VAE");
    }

    #endregion

    #region GetFragments Tests

    [Fact]
    public void GetFragments_ShouldReturnExpectedFragments()
    {
        // Act
        var fragments = _workflow.GetFragments().ToList();

        // Assert - Should include UI fragments for prompts, latent, sampler, upscale, detailer
        fragments.Should().HaveCountGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void GetFragments_ShouldIncludePromptsFragment()
    {
        // Act
        var fragments = _workflow.GetFragments().ToList();

        // Assert
        fragments.Should().Contain(f => f.Metadata.Id == "prompts");
    }

    [Fact]
    public void GetFragments_ShouldIncludeSamplerFragment()
    {
        // Act
        var fragments = _workflow.GetFragments().ToList();

        // Assert
        fragments.Should().Contain(f => f.Metadata.Id == "main_sampler");
    }

    [Fact]
    public void GetFragments_ShouldIncludeUpscaleFragment()
    {
        // Act
        var fragments = _workflow.GetFragments().ToList();

        // Assert
        fragments.Should().Contain(f => f.Metadata.Id == "upscale");
    }

    [Fact]
    public void GetFragments_ShouldIncludeDetailerFragment()
    {
        // Act
        var fragments = _workflow.GetFragments().ToList();

        // Assert
        fragments.Should().Contain(f => f.Metadata.Id == "detailer");
    }

    #endregion

    #region Build Tests

    [Fact]
    public void Build_WithMinimalParameters_ShouldGenerateValidWorkflow()
    {
        // Arrange
        var parameters = CreateMinimalParameters();

        // Act
        var workflow = _workflow.Build(parameters);

        // Assert
        workflow.Should().NotBeNull();
        workflow.Json.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Build_ShouldGenerateValidJson()
    {
        // Arrange
        var parameters = CreateMinimalParameters();

        // Act
        var workflow = _workflow.Build(parameters);

        // Assert - Verify it's valid JSON
        var action = () => JsonDocument.Parse(workflow.Json);
        action.Should().NotThrow();
    }

    [Fact]
    public void Build_ShouldContainUnetLoaderNode()
    {
        // Arrange
        var parameters = CreateMinimalParameters();

        // Act
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // Assert
        json.RootElement.TryGetProperty("unet_loader", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldContainDualClipLoaderNode()
    {
        // Arrange
        var parameters = CreateMinimalParameters();

        // Act
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // Assert
        json.RootElement.TryGetProperty("dual_clip_loader", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldContainVaeLoaderNode()
    {
        // Arrange
        var parameters = CreateMinimalParameters();

        // Act
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // Assert
        json.RootElement.TryGetProperty("vae_loader", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldNotContainReFluxPatcherNode()
    {
        // Arrange
        var parameters = CreateMinimalParameters();

        // Act
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // Assert - ReFluxPatcher is disabled in this workflow
        json.RootElement.TryGetProperty("reflux_patcher", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_ShouldContainFluxGuidanceNode()
    {
        // Arrange
        var parameters = CreateMinimalParameters();

        // Act
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // Assert
        json.RootElement.TryGetProperty("flux_guidance", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldContainSamplerNode()
    {
        // Arrange
        var parameters = CreateMinimalParameters();

        // Act
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // Assert
        json.RootElement.TryGetProperty("sampler_main", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldContainVaeDecodeNode()
    {
        // Arrange
        var parameters = CreateMinimalParameters();

        // Act
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // Assert
        json.RootElement.TryGetProperty("vae_decoder", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldContainSaveNode()
    {
        // Arrange
        var parameters = CreateMinimalParameters();

        // Act
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // Assert
        json.RootElement.TryGetProperty("save", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_WithUpscaleActive_ShouldContainUpscaleNodes()
    {
        // Arrange
        var parameters = CreateParametersWithUpscale();

        // Act
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // Assert
        json.RootElement.TryGetProperty("upscale_model_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("upscale_with_model", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("upscale_unsample", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_WithDetailerActive_ShouldContainDetailerNodes()
    {
        // Arrange
        var parameters = CreateParametersWithDetailer();

        // Act
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // Assert
        // Detailer should have its own loader with detailer_ prefix
        json.RootElement.TryGetProperty("detailer_unet_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("detailer", out _).Should().BeTrue();
    }

    [Fact]
    public void Build_UnetLoader_ShouldHaveCorrectClassType()
    {
        // Arrange
        var parameters = CreateMinimalParameters();

        // Act
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // Assert
        json.RootElement.TryGetProperty("unet_loader", out var node).Should().BeTrue();
        node.TryGetProperty("class_type", out var classType).Should().BeTrue();
        classType.GetString().Should().Be("UNETLoader");
    }

    [Fact]
    public void Build_DualClipLoader_ShouldHaveCorrectClassType()
    {
        // Arrange
        var parameters = CreateMinimalParameters();

        // Act
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // Assert
        json.RootElement.TryGetProperty("dual_clip_loader", out var node).Should().BeTrue();
        node.TryGetProperty("class_type", out var classType).Should().BeTrue();
        classType.GetString().Should().Be("DualCLIPLoader");
    }

    [Fact]
    public void Build_WithCustomAssets_ShouldUseProvidedValues()
    {
        // Arrange
        var parameters = CreateMinimalParameters();
        parameters.Assets["Model"] = "custom_flux_model.safetensors";

        // Act
        var workflow = _workflow.Build(parameters);
        var json = JsonDocument.Parse(workflow.Json);

        // Assert
        json.RootElement.TryGetProperty("unet_loader", out var unetNode).Should().BeTrue();
        unetNode.TryGetProperty("inputs", out var inputs).Should().BeTrue();
        inputs.TryGetProperty("unet_name", out var unetName).Should().BeTrue();
        unetName.GetString().Should().Be("custom_flux_model.safetensors");
    }

    #endregion

    #region Helper Methods

    private static GenerationParameters CreateMinimalParameters()
    {
        var parameters = new GenerationParameters();
        
        // Set up prompts fragment
        var promptsFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.Prompts);
        promptsFragment.SetValue(FragmentKeys.Params.Positive, "a beautiful landscape");
        promptsFragment.SetValue(FragmentKeys.Params.Negative, "");
        
        // Set up latent fragment
        var latentFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.Latent);
        latentFragment.SetValue(FragmentKeys.Params.Width, 872);
        latentFragment.SetValue(FragmentKeys.Params.Height, 1248);
        latentFragment.SetValue(FragmentKeys.Params.BatchSize, 1);
        
        // Set up sampler fragment
        var samplerFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler);
        samplerFragment.SetValue(FragmentKeys.Params.SamplerName, "multistep/res_2m");
        samplerFragment.SetValue(FragmentKeys.Params.Scheduler, "beta");
        samplerFragment.SetValue(FragmentKeys.Params.Steps, 20);
        samplerFragment.SetValue(FragmentKeys.Params.Cfg, 1.0);
        samplerFragment.SetValue(FragmentKeys.Params.Denoise, 1.0);
        samplerFragment.SetValue(FragmentKeys.Params.Seed, 42L);
        samplerFragment.SetValue("guidance", 3.5);
        
        // Set up default assets
        parameters.Assets["Model"] = "flux1-krea-dev_fp8_scaled.safetensors";
        parameters.Assets["Clip1"] = "t5xxl_fp8_e4m3fn_scaled.safetensors";
        parameters.Assets["Clip2"] = "ViT-L-14-BEST-smooth-GmP-TE-only-HF-format.safetensors";
        parameters.Assets["VAE"] = "ae.safetensors";
        
        return parameters;
    }

    private static GenerationParameters CreateParametersWithUpscale()
    {
        var parameters = CreateMinimalParameters();
        
        // Set up upscale fragment (active)
        var upscaleFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.Upscale);
        upscaleFragment.IsActive = true;
        upscaleFragment.SetValue("upscale_model", "4x-UltraSharpV2.safetensors");
        upscaleFragment.SetValue("upscale_width", 1744);
        upscaleFragment.SetValue("upscale_height", 2496);
        upscaleFragment.SetValue("upscale_steps", 20);
        upscaleFragment.SetValue("upscale_denoise", 1.0);
        
        return parameters;
    }

    private static GenerationParameters CreateParametersWithDetailer()
    {
        var parameters = CreateMinimalParameters();
        
        // Set up detailer fragment (active)
        var detailerFragment = parameters.GetOrCreateFragment(FragmentKeys.Fragments.Detailer);
        detailerFragment.IsActive = true;
        detailerFragment.SetValue("detailer_detection_model", "bbox/face_yolov8m.pt");
        detailerFragment.SetValue("detailer_sampler", "dpmpp_2m");
        detailerFragment.SetValue("detailer_scheduler", "beta");
        detailerFragment.SetValue("detailer_seed", 42L);
        detailerFragment.SetValue("detailer_steps", 20);
        detailerFragment.SetValue("detailer_cfg", 1.0);
        detailerFragment.SetValue("detailer_denoise", 0.65);
        
        return parameters;
    }

    #endregion
}
