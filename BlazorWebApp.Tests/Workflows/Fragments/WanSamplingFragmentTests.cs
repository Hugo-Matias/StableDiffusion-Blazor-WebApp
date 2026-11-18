using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Wan;
using BlazorWebApp.Workflows.Models;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace BlazorWebApp.Tests.Workflows.Fragments;

/// <summary>
/// Tests for Img2Vid sampling and output fragments (Phase 6 Step 4).
/// </summary>
public class WanSamplingFragmentTests
{
    #region SamplerAdvancedFragment

    [Fact]
    public void SamplerAdvanced_Build_ShouldCreateSingleNode()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = SetupSamplerRegistry();

        new SamplerAdvancedFragment().Build(builder, registry, new SamplerAdvancedFragment.Parameters
        {
            SamplerId = "sampler_high",
            ModelInputName = "high_sampled_model_output"
        });

        builder.NodeCount.Should().Be(1);
        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("sampler_high", out _).Should().BeTrue();
    }

    [Fact]
    public void SamplerAdvanced_Build_ShouldSetClassType()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = SetupSamplerRegistry();

        new SamplerAdvancedFragment().Build(builder, registry, new SamplerAdvancedFragment.Parameters
        {
            SamplerId = "sampler_high",
            ModelInputName = "high_sampled_model_output"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("sampler_high").GetProperty("class_type").GetString()
            .Should().Be("KSamplerAdvanced");
    }

    [Fact]
    public void SamplerAdvanced_Build_ShouldSetAllInputs()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = SetupSamplerRegistry();

        new SamplerAdvancedFragment().Build(builder, registry, new SamplerAdvancedFragment.Parameters
        {
            SamplerId = "sampler_high",
            Seed = 12345,
            Steps = 8,
            Cfg = 1.0,
            SamplerName = "euler",
            Scheduler = "simple",
            AddNoise = "enable",
            ReturnWithLeftoverNoise = "enable",
            StartAtStep = 0,
            EndAtStep = 4,
            ModelInputName = "high_sampled_model_output"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("sampler_high").GetProperty("inputs");

        inputs.GetProperty("add_noise").GetString().Should().Be("enable");
        inputs.GetProperty("noise_seed").GetInt64().Should().Be(12345);
        inputs.GetProperty("steps").GetInt32().Should().Be(8);
        inputs.GetProperty("cfg").GetDouble().Should().BeApproximately(1.0, 0.01);
        inputs.GetProperty("sampler_name").GetString().Should().Be("euler");
        inputs.GetProperty("scheduler").GetString().Should().Be("simple");
        inputs.GetProperty("start_at_step").GetInt32().Should().Be(0);
        inputs.GetProperty("end_at_step").GetInt32().Should().Be(4);
        inputs.GetProperty("return_with_leftover_noise").GetString().Should().Be("enable");
    }

    [Fact]
    public void SamplerAdvanced_Build_ShouldReferenceRegistryInputs()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = SetupSamplerRegistry();

        new SamplerAdvancedFragment().Build(builder, registry, new SamplerAdvancedFragment.Parameters
        {
            SamplerId = "sampler_high",
            ModelInputName = "high_sampled_model_output",
            PositiveInputName = "painter_positive_output",
            NegativeInputName = "painter_negative_output",
            LatentInputName = "painter_latent_output"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("sampler_high").GetProperty("inputs");

        inputs.GetProperty("model")[0].GetString().Should().Be("high_model_sampling");
        inputs.GetProperty("positive")[0].GetString().Should().Be("painter_i2v");
        inputs.GetProperty("positive")[1].GetInt32().Should().Be(0);
        inputs.GetProperty("negative")[0].GetString().Should().Be("painter_i2v");
        inputs.GetProperty("negative")[1].GetInt32().Should().Be(1);
        inputs.GetProperty("latent_image")[0].GetString().Should().Be("painter_i2v");
        inputs.GetProperty("latent_image")[1].GetInt32().Should().Be(2);
    }

    [Fact]
    public void SamplerAdvanced_Build_ShouldRegisterLatentOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = SetupSamplerRegistry();

        new SamplerAdvancedFragment().Build(builder, registry, new SamplerAdvancedFragment.Parameters
        {
            SamplerId = "sampler_high",
            ModelInputName = "high_sampled_model_output",
            LatentOutputName = "high_latent_output"
        });

        registry.HasOutput("high_latent_output").Should().BeTrue();
        var (nodeId, index) = registry.GetRef("high_latent_output");
        nodeId.Should().Be("sampler_high");
        index.Should().Be(0);
    }

    [Fact]
    public void SamplerAdvanced_Build_DualSamplerChain_ShouldChainLatents()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = SetupSamplerRegistry();

        // High sampler: steps 0 -> 4
        new SamplerAdvancedFragment().Build(builder, registry, new SamplerAdvancedFragment.Parameters
        {
            SamplerId = "sampler_high",
            Seed = 42,
            Steps = 8,
            StartAtStep = 0,
            EndAtStep = 4,
            AddNoise = "enable",
            ReturnWithLeftoverNoise = "enable",
            ModelInputName = "high_sampled_model_output",
            LatentOutputName = "high_latent_output"
        });

        // Low sampler: steps 4 -> 10000, uses high latent
        new SamplerAdvancedFragment().Build(builder, registry, new SamplerAdvancedFragment.Parameters
        {
            SamplerId = "sampler_low",
            Seed = 0,
            Steps = 8,
            StartAtStep = 4,
            EndAtStep = 10000,
            AddNoise = "disable",
            ReturnWithLeftoverNoise = "disable",
            ModelInputName = "low_sampled_model_output",
            LatentInputName = "high_latent_output",
            LatentOutputName = "latent_output"
        });

        builder.NodeCount.Should().Be(2);

        var json = JsonDocument.Parse(builder.ToJson());

        // Low sampler should reference high sampler's latent output
        var lowInputs = json.RootElement.GetProperty("sampler_low").GetProperty("inputs");
        lowInputs.GetProperty("latent_image")[0].GetString().Should().Be("sampler_high");
        lowInputs.GetProperty("latent_image")[1].GetInt32().Should().Be(0);
        lowInputs.GetProperty("add_noise").GetString().Should().Be("disable");
        lowInputs.GetProperty("noise_seed").GetInt64().Should().Be(0);
        lowInputs.GetProperty("start_at_step").GetInt32().Should().Be(4);
        lowInputs.GetProperty("end_at_step").GetInt32().Should().Be(10000);

        // Final output should be latent_output -> sampler_low
        var (nodeId, _) = registry.GetRef("latent_output");
        nodeId.Should().Be("sampler_low");
    }

    [Fact]
    public void SamplerAdvanced_Metadata_ShouldHaveUIConfiguration()
    {
        var fragment = new SamplerAdvancedFragment();
        fragment.Metadata.Component.Should().Be("DoubleSamplerForm");
        fragment.Metadata.Icon.Should().Be("fa-solid fa-dice");
        fragment.Metadata.Order.Should().Be(35);
        fragment.Metadata.Collapsible.Should().BeFalse();
        fragment.Metadata.IsHidden.Should().BeFalse();
        fragment.Metadata.Parameters.Should().HaveCount(6);
    }

    [Fact]
    public void SamplerAdvanced_Metadata_ShouldIncludeAutoSplitToggle()
    {
        var fragment = new SamplerAdvancedFragment();
        var autoSplit = fragment.Metadata.Parameters.First(p => p.Name == "auto_split");
        autoSplit.Type.Should().Be(ParameterType.Checkbox);
        autoSplit.DefaultValue.Should().Be(true);
    }

    #endregion

    #region FrameInterpolationFragment

    [Fact]
    public void FrameInterpolation_Build_ShouldCreateThreeNodes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("image_output", "vae_decoder", 0);

        new FrameInterpolationFragment().Build(builder, registry, new FrameInterpolationFragment.Parameters());

        builder.NodeCount.Should().Be(3);
        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("upscale_frames", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("clean_upscale", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("frame_interpolation", out _).Should().BeTrue();
    }

    [Fact]
    public void FrameInterpolation_Build_ShouldSetCorrectClassTypes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("image_output", "vae_decoder", 0);

        new FrameInterpolationFragment().Build(builder, registry, new FrameInterpolationFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("upscale_frames").GetProperty("class_type").GetString()
            .Should().Be("ImageScaleBy");
        json.RootElement.GetProperty("clean_upscale").GetProperty("class_type").GetString()
            .Should().Be("easy cleanGpuUsed");
        json.RootElement.GetProperty("frame_interpolation").GetProperty("class_type").GetString()
            .Should().Be("RIFE VFI");
    }

    [Fact]
    public void FrameInterpolation_Build_ShouldChainNodesCorrectly()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("image_output", "vae_decoder", 0);

        new FrameInterpolationFragment().Build(builder, registry, new FrameInterpolationFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());

        // upscale_frames -> image_output (vae_decoder)
        var upscaleInputs = json.RootElement.GetProperty("upscale_frames").GetProperty("inputs");
        upscaleInputs.GetProperty("image")[0].GetString().Should().Be("vae_decoder");

        // clean_upscale -> upscale_frames
        var cleanInputs = json.RootElement.GetProperty("clean_upscale").GetProperty("inputs");
        cleanInputs.GetProperty("anything")[0].GetString().Should().Be("upscale_frames");

        // frame_interpolation -> clean_upscale
        var rifeInputs = json.RootElement.GetProperty("frame_interpolation").GetProperty("inputs");
        rifeInputs.GetProperty("frames")[0].GetString().Should().Be("clean_upscale");
    }

    [Fact]
    public void FrameInterpolation_Build_ShouldSetRifeParameters()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("image_output", "vae_decoder", 0);

        new FrameInterpolationFragment().Build(builder, registry, new FrameInterpolationFragment.Parameters
        {
            RifeModel = "rife48.pth",
            FrameMultiplier = 4,
            ScaleBy = 1.5
        });

        var json = JsonDocument.Parse(builder.ToJson());

        var upscaleInputs = json.RootElement.GetProperty("upscale_frames").GetProperty("inputs");
        upscaleInputs.GetProperty("scale_by").GetDouble().Should().BeApproximately(1.5, 0.01);

        var rifeInputs = json.RootElement.GetProperty("frame_interpolation").GetProperty("inputs");
        rifeInputs.GetProperty("ckpt_name").GetString().Should().Be("rife48.pth");
        rifeInputs.GetProperty("multiplier").GetInt32().Should().Be(4);
        rifeInputs.GetProperty("fast_mode").GetBoolean().Should().BeFalse();
        rifeInputs.GetProperty("ensemble").GetBoolean().Should().BeTrue();
        rifeInputs.GetProperty("clear_cache_after_n_frames").GetInt32().Should().Be(10);
        rifeInputs.GetProperty("scale_factor").GetInt32().Should().Be(1);
    }

    [Fact]
    public void FrameInterpolation_Build_ShouldRegisterFramesOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("image_output", "vae_decoder", 0);

        new FrameInterpolationFragment().Build(builder, registry, new FrameInterpolationFragment.Parameters());

        registry.HasOutput("frames_output").Should().BeTrue();
        var (nodeId, index) = registry.GetRef("frames_output");
        nodeId.Should().Be("frame_interpolation");
        index.Should().Be(0);
    }

    [Fact]
    public void FrameInterpolation_Metadata_ShouldHaveUIConfiguration()
    {
        var fragment = new FrameInterpolationFragment();
        fragment.Metadata.Component.Should().Be("FrameInterpolationForm");
        fragment.Metadata.Icon.Should().Be("fa-solid fa-film");
        fragment.Metadata.Order.Should().Be(70);
        fragment.Metadata.Collapsible.Should().BeTrue();
        fragment.Metadata.IsHidden.Should().BeFalse();
        fragment.Metadata.Parameters.Should().HaveCount(3);
    }

    [Fact]
    public void FrameInterpolation_Build_ShouldAcceptCustomImageInput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("custom_image", "some_node", 0);

        new FrameInterpolationFragment().Build(builder, registry, new FrameInterpolationFragment.Parameters
        {
            ImageInputName = "custom_image"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var upscaleInputs = json.RootElement.GetProperty("upscale_frames").GetProperty("inputs");
        upscaleInputs.GetProperty("image")[0].GetString().Should().Be("some_node");
    }

    #endregion

    #region Integration: Full Sampling Pipeline

    [Fact]
    public void FullSamplingPipeline_ShouldProduceCorrectChain()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        // Simulate upstream outputs
        registry.Register("high_sampled_model_output", "high_model_sampling", 0);
        registry.Register("low_sampled_model_output", "low_model_sampling", 0);
        registry.Register("painter_positive_output", "painter_i2v", 0);
        registry.Register("painter_negative_output", "painter_i2v", 1);
        registry.Register("painter_latent_output", "painter_i2v", 2);

        var steps = 8;
        var midpoint = steps / 2;

        // High sampler: 0 -> midpoint
        new SamplerAdvancedFragment().Build(builder, registry, new SamplerAdvancedFragment.Parameters
        {
            SamplerId = "sampler_high",
            Seed = 42,
            Steps = steps,
            StartAtStep = 0,
            EndAtStep = midpoint,
            AddNoise = "enable",
            ReturnWithLeftoverNoise = "enable",
            ModelInputName = "high_sampled_model_output",
            LatentOutputName = "high_latent_output"
        });

        // Low sampler: midpoint -> 10000
        new SamplerAdvancedFragment().Build(builder, registry, new SamplerAdvancedFragment.Parameters
        {
            SamplerId = "sampler_low",
            Seed = 0,
            Steps = steps,
            StartAtStep = midpoint,
            EndAtStep = 10000,
            AddNoise = "disable",
            ReturnWithLeftoverNoise = "disable",
            ModelInputName = "low_sampled_model_output",
            LatentInputName = "high_latent_output",
            LatentOutputName = "latent_output"
        });

        // Frame interpolation
        registry.Register("image_output", "vae_decoder", 0);
        new FrameInterpolationFragment().Build(builder, registry, new FrameInterpolationFragment.Parameters
        {
            FrameMultiplier = 2
        });

        // Verify node count: 2 samplers + 3 interpolation = 5
        builder.NodeCount.Should().Be(5);

        // Verify final outputs exist
        registry.HasOutput("latent_output").Should().BeTrue();
        registry.HasOutput("frames_output").Should().BeTrue();

        // Verify JSON is valid
        var action = () => JsonDocument.Parse(builder.ToJson());
        action.Should().NotThrow();

        // Verify chain integrity
        var json = JsonDocument.Parse(builder.ToJson());

        // Low sampler latent chains from high sampler
        var lowLatent = json.RootElement.GetProperty("sampler_low").GetProperty("inputs")
            .GetProperty("latent_image");
        lowLatent[0].GetString().Should().Be("sampler_high");

        // Interpolation chains from image_output
        var upscaleImage = json.RootElement.GetProperty("upscale_frames").GetProperty("inputs")
            .GetProperty("image");
        upscaleImage[0].GetString().Should().Be("vae_decoder");
    }

    #endregion

    #region Helpers

    private static NodeRegistry SetupSamplerRegistry()
    {
        var registry = new NodeRegistry();
        registry.Register("high_sampled_model_output", "high_model_sampling", 0);
        registry.Register("low_sampled_model_output", "low_model_sampling", 0);
        registry.Register("painter_positive_output", "painter_i2v", 0);
        registry.Register("painter_negative_output", "painter_i2v", 1);
        registry.Register("painter_latent_output", "painter_i2v", 2);
        return registry;
    }

    #endregion
}
