using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Wan;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace BlazorWebApp.Tests.Workflows.Fragments;

/// <summary>
/// Tests for SteadyDancer-specific fragments (Phase 6 Step 6).
/// </summary>
public class SteadyDancerFragmentTests
{
    #region LoadWanModelFragment

    [Fact]
    public void LoadWanModel_Build_ShouldCreateSixNodeChain()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadWanModelFragment().Build(builder, registry, new LoadWanModelFragment.Parameters
        {
            ModelName = "steadydancer.safetensors",
            LoraName = "speed_lora.safetensors"
        });

        builder.NodeCount.Should().Be(6);
        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("compile_settings", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("model_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("block_swap", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("set_block_swap", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("lora_select", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("set_loras", out _).Should().BeTrue();
    }

    [Fact]
    public void LoadWanModel_Build_ShouldChainNodesCorrectly()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadWanModelFragment().Build(builder, registry, new LoadWanModelFragment.Parameters
        {
            ModelName = "model.safetensors",
            LoraName = "lora.safetensors"
        });

        var json = JsonDocument.Parse(builder.ToJson());

        // model_loader -> compile_settings
        var loaderInputs = json.RootElement.GetProperty("model_loader").GetProperty("inputs");
        loaderInputs.GetProperty("compile_args")[0].GetString().Should().Be("compile_settings");

        // set_block_swap -> model_loader + block_swap
        var setSwapInputs = json.RootElement.GetProperty("set_block_swap").GetProperty("inputs");
        setSwapInputs.GetProperty("model")[0].GetString().Should().Be("model_loader");
        setSwapInputs.GetProperty("block_swap_args")[0].GetString().Should().Be("block_swap");

        // set_loras -> set_block_swap + lora_select
        var setLorasInputs = json.RootElement.GetProperty("set_loras").GetProperty("inputs");
        setLorasInputs.GetProperty("model")[0].GetString().Should().Be("set_block_swap");
        setLorasInputs.GetProperty("lora")[0].GetString().Should().Be("lora_select");
    }

    [Fact]
    public void LoadWanModel_Build_ShouldRegisterModelOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadWanModelFragment().Build(builder, registry, new LoadWanModelFragment.Parameters());

        registry.HasOutput("model_output").Should().BeTrue();
        var (nodeId, index) = registry.GetRef("model_output");
        nodeId.Should().Be("set_loras");
        index.Should().Be(0);
    }

    [Fact]
    public void LoadWanModel_Build_ShouldSetCorrectClassTypes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadWanModelFragment().Build(builder, registry, new LoadWanModelFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("compile_settings").GetProperty("class_type").GetString().Should().Be("WanVideoTorchCompileSettings");
        json.RootElement.GetProperty("model_loader").GetProperty("class_type").GetString().Should().Be("WanVideoModelLoader");
        json.RootElement.GetProperty("block_swap").GetProperty("class_type").GetString().Should().Be("WanVideoBlockSwap");
        json.RootElement.GetProperty("set_block_swap").GetProperty("class_type").GetString().Should().Be("WanVideoSetBlockSwap");
        json.RootElement.GetProperty("lora_select").GetProperty("class_type").GetString().Should().Be("WanVideoLoraSelect");
        json.RootElement.GetProperty("set_loras").GetProperty("class_type").GetString().Should().Be("WanVideoSetLoRAs");
    }

    [Fact]
    public void LoadWanModel_Build_WithScope_ShouldPrefixAllNodeIds()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadWanModelFragment().Build(builder, registry, new LoadWanModelFragment.Parameters(), scope: "sd_");

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("sd_compile_settings", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("sd_model_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("sd_set_loras", out _).Should().BeTrue();
        registry.HasOutput("sd_model_output").Should().BeTrue();
    }

    [Fact]
    public void LoadWanModel_Build_ShouldSetLoraParameters()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadWanModelFragment().Build(builder, registry, new LoadWanModelFragment.Parameters
        {
            LoraName = "my_lora.safetensors",
            LoraStrength = 0.75
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var loraInputs = json.RootElement.GetProperty("lora_select").GetProperty("inputs");
        loraInputs.GetProperty("lora").GetString().Should().Be("my_lora.safetensors");
        loraInputs.GetProperty("strength").GetDouble().Should().BeApproximately(0.75, 0.01);
    }

    #endregion

    #region LoadWanVaeFragment

    [Fact]
    public void LoadWanVae_Build_ShouldCreateSingleNode()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadWanVaeFragment().Build(builder, registry, new LoadWanVaeFragment.Parameters
        {
            VaeName = "wan_vae.safetensors"
        });

        builder.NodeCount.Should().Be(1);
        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("vae_loader").GetProperty("class_type").GetString().Should().Be("WanVideoVAELoader");
    }

    [Fact]
    public void LoadWanVae_Build_ShouldRegisterVaeOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadWanVaeFragment().Build(builder, registry, new LoadWanVaeFragment.Parameters());

        registry.HasOutput("vae_output").Should().BeTrue();
        var (nodeId, _) = registry.GetRef("vae_output");
        nodeId.Should().Be("vae_loader");
    }

    [Fact]
    public void LoadWanVae_Build_ShouldSetPrecision()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadWanVaeFragment().Build(builder, registry, new LoadWanVaeFragment.Parameters
        {
            Precision = "fp32"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("vae_loader").GetProperty("inputs");
        inputs.GetProperty("precision").GetString().Should().Be("fp32");
    }

    #endregion

    #region TextEncodeWanFragment

    [Fact]
    public void TextEncodeWan_Build_ShouldCreateSingleNode()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new TextEncodeWanFragment().Build(builder, registry, new TextEncodeWanFragment.Parameters
        {
            Positive = "a dancing person",
            Negative = "blurry"
        });

        builder.NodeCount.Should().Be(1);
        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("text_encode").GetProperty("class_type").GetString().Should().Be("WanVideoTextEncodeCached");
    }

    [Fact]
    public void TextEncodeWan_Build_ShouldRegisterTextEmbeds()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new TextEncodeWanFragment().Build(builder, registry, new TextEncodeWanFragment.Parameters());

        registry.HasOutput("text_embeds").Should().BeTrue();
        var (nodeId, _) = registry.GetRef("text_embeds");
        nodeId.Should().Be("text_encode");
    }

    [Fact]
    public void TextEncodeWan_Build_ShouldSetPrompts()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new TextEncodeWanFragment().Build(builder, registry, new TextEncodeWanFragment.Parameters
        {
            TextEncoderName = "umt5.safetensors",
            Positive = "hello world",
            Negative = "bad quality"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("text_encode").GetProperty("inputs");
        inputs.GetProperty("model_name").GetString().Should().Be("umt5.safetensors");
        inputs.GetProperty("positive_prompt").GetString().Should().Be("hello world");
        inputs.GetProperty("negative_prompt").GetString().Should().Be("bad quality");
    }

    #endregion

    #region PoseDetectionFragment

    [Fact]
    public void PoseDetection_Build_ShouldCreateFourNodes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("width", "get_size", 1);
        registry.Register("height", "get_size", 2);
        registry.Register("image_size_info", "get_size", 0);

        new PoseDetectionFragment().Build(builder, registry, new PoseDetectionFragment.Parameters());

        builder.NodeCount.Should().Be(4);
        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("onnx_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("pose_detection", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("draw_pose", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("resize_pose", out _).Should().BeTrue();
    }

    [Fact]
    public void PoseDetection_Build_ShouldChainCorrectly()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("width", "get_size", 1);
        registry.Register("height", "get_size", 2);
        registry.Register("image_size_info", "get_size", 0);

        new PoseDetectionFragment().Build(builder, registry, new PoseDetectionFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());

        // pose_detection references onnx_loader and image_size_info
        var detInputs = json.RootElement.GetProperty("pose_detection").GetProperty("inputs");
        detInputs.GetProperty("model")[0].GetString().Should().Be("onnx_loader");
        detInputs.GetProperty("images")[0].GetString().Should().Be("get_size");

        // draw_pose references pose_detection
        var drawInputs = json.RootElement.GetProperty("draw_pose").GetProperty("inputs");
        drawInputs.GetProperty("pose_data")[0].GetString().Should().Be("pose_detection");

        // resize_pose references draw_pose
        var resizeInputs = json.RootElement.GetProperty("resize_pose").GetProperty("inputs");
        resizeInputs.GetProperty("image")[0].GetString().Should().Be("draw_pose");
    }

    [Fact]
    public void PoseDetection_Build_ShouldRegisterPoseImages()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("width", "get_size", 1);
        registry.Register("height", "get_size", 2);
        registry.Register("image_size_info", "get_size", 0);

        new PoseDetectionFragment().Build(builder, registry, new PoseDetectionFragment.Parameters());

        registry.HasOutput("pose_images").Should().BeTrue();
        var (nodeId, _) = registry.GetRef("pose_images");
        nodeId.Should().Be("resize_pose");
    }

    [Fact]
    public void PoseDetection_Build_ShouldSetCorrectClassTypes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("width", "get_size", 1);
        registry.Register("height", "get_size", 2);
        registry.Register("image_size_info", "get_size", 0);

        new PoseDetectionFragment().Build(builder, registry, new PoseDetectionFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("onnx_loader").GetProperty("class_type").GetString().Should().Be("OnnxDetectionModelLoader");
        json.RootElement.GetProperty("pose_detection").GetProperty("class_type").GetString().Should().Be("PoseAndFaceDetection");
        json.RootElement.GetProperty("draw_pose").GetProperty("class_type").GetString().Should().Be("DrawViTPose");
        json.RootElement.GetProperty("resize_pose").GetProperty("class_type").GetString().Should().Be("ImageResizeKJv2");
    }

    #endregion

    #region I2VEncodeFragment

    [Fact]
    public void I2VEncode_Build_ShouldCreateTwoNodes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = CreateI2VEncodeRegistry();

        new I2VEncodeFragment().Build(builder, registry, new I2VEncodeFragment.Parameters());

        builder.NodeCount.Should().Be(2);
        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("clip_vision_encode", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("i2v_encode", out _).Should().BeTrue();
    }

    [Fact]
    public void I2VEncode_Build_ShouldRegisterImageEmbeds()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = CreateI2VEncodeRegistry();

        new I2VEncodeFragment().Build(builder, registry, new I2VEncodeFragment.Parameters());

        registry.HasOutput("image_embeds").Should().BeTrue();
        var (nodeId, _) = registry.GetRef("image_embeds");
        nodeId.Should().Be("i2v_encode");
    }

    [Fact]
    public void I2VEncode_Build_ShouldChainClipVisionToI2V()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = CreateI2VEncodeRegistry();

        new I2VEncodeFragment().Build(builder, registry, new I2VEncodeFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        var i2vInputs = json.RootElement.GetProperty("i2v_encode").GetProperty("inputs");
        i2vInputs.GetProperty("clip_embeds")[0].GetString().Should().Be("clip_vision_encode");
    }

    [Fact]
    public void I2VEncode_Build_ShouldReferenceRegistryInputs()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = CreateI2VEncodeRegistry();

        new I2VEncodeFragment().Build(builder, registry, new I2VEncodeFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());

        var clipInputs = json.RootElement.GetProperty("clip_vision_encode").GetProperty("inputs");
        clipInputs.GetProperty("clip_vision")[0].GetString().Should().Be("clip_vision_loader");
        clipInputs.GetProperty("image_1")[0].GetString().Should().Be("resize_subject");

        var i2vInputs = json.RootElement.GetProperty("i2v_encode").GetProperty("inputs");
        i2vInputs.GetProperty("vae")[0].GetString().Should().Be("vae_loader");
        i2vInputs.GetProperty("start_image")[0].GetString().Should().Be("resize_subject");
    }

    #endregion

    #region SteadyDancerEmbedsFragment

    [Fact]
    public void SteadyDancerEmbeds_Build_ShouldCreateFourNodes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = CreateEmbedsRegistry();

        new SteadyDancerEmbedsFragment().Build(builder, registry, new SteadyDancerEmbedsFragment.Parameters());

        builder.NodeCount.Should().Be(4);
        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("pose_encode", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("get_first_pose", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("pose_clip_vision", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("add_steadydancer", out _).Should().BeTrue();
    }

    [Fact]
    public void SteadyDancerEmbeds_Build_ShouldRegisterOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = CreateEmbedsRegistry();

        new SteadyDancerEmbedsFragment().Build(builder, registry, new SteadyDancerEmbedsFragment.Parameters());

        registry.HasOutput("steadydancer_embeds").Should().BeTrue();
        var (nodeId, _) = registry.GetRef("steadydancer_embeds");
        nodeId.Should().Be("add_steadydancer");
    }

    [Fact]
    public void SteadyDancerEmbeds_Build_ShouldChainCorrectly()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = CreateEmbedsRegistry();

        new SteadyDancerEmbedsFragment().Build(builder, registry, new SteadyDancerEmbedsFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());

        // get_first_pose references pose_images
        var getFirstInputs = json.RootElement.GetProperty("get_first_pose").GetProperty("inputs");
        getFirstInputs.GetProperty("images")[0].GetString().Should().Be("resize_pose");

        // pose_clip_vision references clip_vision and get_first_pose
        var poseClipInputs = json.RootElement.GetProperty("pose_clip_vision").GetProperty("inputs");
        poseClipInputs.GetProperty("clip_vision")[0].GetString().Should().Be("clip_vision_loader");
        poseClipInputs.GetProperty("image_1")[0].GetString().Should().Be("get_first_pose");

        // add_steadydancer references image_embeds, pose_encode, pose_clip_vision
        var addInputs = json.RootElement.GetProperty("add_steadydancer").GetProperty("inputs");
        addInputs.GetProperty("embeds")[0].GetString().Should().Be("i2v_encode");
        addInputs.GetProperty("pose_latents_positive")[0].GetString().Should().Be("pose_encode");
        addInputs.GetProperty("clip_vision_embeds")[0].GetString().Should().Be("pose_clip_vision");
    }

    [Fact]
    public void SteadyDancerEmbeds_Build_ShouldSetStrengthParameters()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = CreateEmbedsRegistry();

        new SteadyDancerEmbedsFragment().Build(builder, registry, new SteadyDancerEmbedsFragment.Parameters
        {
            PoseStrengthSpatial = 0.8,
            PoseStrengthTemporal = 0.6
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("add_steadydancer").GetProperty("inputs");
        inputs.GetProperty("pose_strength_spatial").GetDouble().Should().BeApproximately(0.8, 0.01);
        inputs.GetProperty("pose_strength_temporal").GetDouble().Should().BeApproximately(0.6, 0.01);
    }

    #endregion

    #region ContextOptionsFragment

    [Fact]
    public void ContextOptions_Build_ShouldCreateSingleNode()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new ContextOptionsFragment().Build(builder, registry, new ContextOptionsFragment.Parameters());

        builder.NodeCount.Should().Be(1);
        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("context_opts").GetProperty("class_type").GetString().Should().Be("WanVideoContextOptions");
    }

    [Fact]
    public void ContextOptions_Build_ShouldRegisterOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new ContextOptionsFragment().Build(builder, registry, new ContextOptionsFragment.Parameters());

        registry.HasOutput("context_options").Should().BeTrue();
        var (nodeId, _) = registry.GetRef("context_options");
        nodeId.Should().Be("context_opts");
    }

    [Fact]
    public void ContextOptions_Build_ShouldSetParameters()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new ContextOptionsFragment().Build(builder, registry, new ContextOptionsFragment.Parameters
        {
            ContextFrames = 120,
            ContextOverlap = 32,
            ContextStride = 8,
            Freenoise = false
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("context_opts").GetProperty("inputs");
        inputs.GetProperty("context_frames").GetInt32().Should().Be(120);
        inputs.GetProperty("context_overlap").GetInt32().Should().Be(32);
        inputs.GetProperty("context_stride").GetInt32().Should().Be(8);
        inputs.GetProperty("freenoise").GetBoolean().Should().BeFalse();
    }

    #endregion

    #region SamplerWanFragment

    [Fact]
    public void SamplerWan_Build_ShouldCreateThreeNodes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = CreateSamplerWanRegistry();

        new SamplerWanFragment().Build(builder, registry, new SamplerWanFragment.Parameters());

        builder.NodeCount.Should().Be(3);
        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("scheduler", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("sampler_settings", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("sampler", out _).Should().BeTrue();
    }

    [Fact]
    public void SamplerWan_Build_ShouldRegisterLatentOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = CreateSamplerWanRegistry();

        new SamplerWanFragment().Build(builder, registry, new SamplerWanFragment.Parameters());

        registry.HasOutput("latent_output").Should().BeTrue();
        var (nodeId, _) = registry.GetRef("latent_output");
        nodeId.Should().Be("sampler");
    }

    [Fact]
    public void SamplerWan_Build_ShouldChainCorrectly()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = CreateSamplerWanRegistry();

        new SamplerWanFragment().Build(builder, registry, new SamplerWanFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());

        // sampler_settings references scheduler output 3
        var settingsInputs = json.RootElement.GetProperty("sampler_settings").GetProperty("inputs");
        settingsInputs.GetProperty("scheduler")[0].GetString().Should().Be("scheduler");
        settingsInputs.GetProperty("scheduler")[1].GetInt32().Should().Be(3);

        // sampler references sampler_settings
        var samplerInputs = json.RootElement.GetProperty("sampler").GetProperty("inputs");
        samplerInputs.GetProperty("sampler_inputs")[0].GetString().Should().Be("sampler_settings");
    }

    [Fact]
    public void SamplerWan_Build_ShouldSetSamplingParameters()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = CreateSamplerWanRegistry();

        new SamplerWanFragment().Build(builder, registry, new SamplerWanFragment.Parameters
        {
            Steps = 8,
            Cfg = 2.5,
            Shift = 10,
            Seed = 12345,
            Scheduler = "euler"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var schedulerInputs = json.RootElement.GetProperty("scheduler").GetProperty("inputs");
        schedulerInputs.GetProperty("steps").GetInt32().Should().Be(8);
        schedulerInputs.GetProperty("shift").GetInt32().Should().Be(10);
        schedulerInputs.GetProperty("scheduler").GetString().Should().Be("euler");

        var settingsInputs = json.RootElement.GetProperty("sampler_settings").GetProperty("inputs");
        settingsInputs.GetProperty("seed").GetInt64().Should().Be(12345);
        settingsInputs.GetProperty("cfg").GetDouble().Should().BeApproximately(2.5, 0.01);
    }

    [Fact]
    public void SamplerWan_Build_ShouldSetCorrectClassTypes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = CreateSamplerWanRegistry();

        new SamplerWanFragment().Build(builder, registry, new SamplerWanFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("scheduler").GetProperty("class_type").GetString().Should().Be("WanVideoScheduler");
        json.RootElement.GetProperty("sampler_settings").GetProperty("class_type").GetString().Should().Be("WanVideoSamplerSettings");
        json.RootElement.GetProperty("sampler").GetProperty("class_type").GetString().Should().Be("WanVideoSamplerFromSettings");
    }

    #endregion

    #region DecodeWanFragment

    [Fact]
    public void DecodeWan_Build_ShouldCreateSingleNode()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("vae_output", "vae_loader", 0);
        registry.Register("latent_output", "sampler", 0);

        new DecodeWanFragment().Build(builder, registry, new DecodeWanFragment.Parameters());

        builder.NodeCount.Should().Be(1);
        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("decode").GetProperty("class_type").GetString().Should().Be("WanVideoDecode");
    }

    [Fact]
    public void DecodeWan_Build_ShouldRegisterImageOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("vae_output", "vae_loader", 0);
        registry.Register("latent_output", "sampler", 0);

        new DecodeWanFragment().Build(builder, registry, new DecodeWanFragment.Parameters());

        registry.HasOutput("image_output").Should().BeTrue();
        var (nodeId, _) = registry.GetRef("image_output");
        nodeId.Should().Be("decode");
    }

    [Fact]
    public void DecodeWan_Build_ShouldReferenceVaeAndLatent()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("vae_output", "vae_loader", 0);
        registry.Register("latent_output", "sampler", 0);

        new DecodeWanFragment().Build(builder, registry, new DecodeWanFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("decode").GetProperty("inputs");
        inputs.GetProperty("vae")[0].GetString().Should().Be("vae_loader");
        inputs.GetProperty("samples")[0].GetString().Should().Be("sampler");
    }

    [Fact]
    public void DecodeWan_Build_ShouldSetTilingParameters()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("vae_output", "vae_loader", 0);
        registry.Register("latent_output", "sampler", 0);

        new DecodeWanFragment().Build(builder, registry, new DecodeWanFragment.Parameters
        {
            EnableVaeTiling = true,
            TileX = 512,
            TileY = 512
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("decode").GetProperty("inputs");
        inputs.GetProperty("enable_vae_tiling").GetBoolean().Should().BeTrue();
        inputs.GetProperty("tile_x").GetInt32().Should().Be(512);
        inputs.GetProperty("tile_y").GetInt32().Should().Be(512);
    }

    #endregion

    #region ConcatPreviewFragment

    [Fact]
    public void ConcatPreview_Build_ShouldCreateTwoNodes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("resized_image", "resize_subject", 0);
        registry.Register("pose_images", "resize_pose", 0);
        registry.Register("image_output", "decode", 0);

        new ConcatPreviewFragment().Build(builder, registry);

        builder.NodeCount.Should().Be(2);
        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("concat_source_pose", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("concat_final", out _).Should().BeTrue();
    }

    [Fact]
    public void ConcatPreview_Build_ShouldRegisterPreviewConcat()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("resized_image", "resize_subject", 0);
        registry.Register("pose_images", "resize_pose", 0);
        registry.Register("image_output", "decode", 0);

        new ConcatPreviewFragment().Build(builder, registry);

        registry.HasOutput("preview_concat").Should().BeTrue();
        var (nodeId, _) = registry.GetRef("preview_concat");
        nodeId.Should().Be("concat_final");
    }

    [Fact]
    public void ConcatPreview_Build_ShouldChainCorrectly()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("resized_image", "resize_subject", 0);
        registry.Register("pose_images", "resize_pose", 0);
        registry.Register("image_output", "decode", 0);

        new ConcatPreviewFragment().Build(builder, registry);

        var json = JsonDocument.Parse(builder.ToJson());

        // concat_source_pose: resized_image + pose_images
        var sourcePoseInputs = json.RootElement.GetProperty("concat_source_pose").GetProperty("inputs");
        sourcePoseInputs.GetProperty("image_1")[0].GetString().Should().Be("resize_subject");
        sourcePoseInputs.GetProperty("image_2")[0].GetString().Should().Be("resize_pose");

        // concat_final: image_output + concat_source_pose
        var finalInputs = json.RootElement.GetProperty("concat_final").GetProperty("inputs");
        finalInputs.GetProperty("image_1")[0].GetString().Should().Be("decode");
        finalInputs.GetProperty("image_2")[0].GetString().Should().Be("concat_source_pose");
    }

    #endregion

    #region Integration: Full SteadyDancer Fragment Pipeline

    [Fact]
    public void FullSteadyDancerPipeline_ShouldProduceValidJsonWithAllOutputs()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        // 1. LoadVideo (simulated - register outputs)
        registry.Register("video_frames", "load_video", 0);
        registry.Register("frame_count", "load_video", 1);
        registry.Register("audio", "load_video", 2);

        // 2. GetImageSize (simulated - register outputs)
        registry.Register("image_size_info", "get_size", 0);
        registry.Register("width", "get_size", 1);
        registry.Register("height", "get_size", 2);
        registry.Register("num_frames", "get_size", 3);

        // 3. LoadImage (simulated)
        registry.Register("image_input", "load_image", 0);

        // 4. ResizeImage (simulated)
        registry.Register("resized_image", "resize_subject", 0);

        // 5. LoadWanModel
        new LoadWanModelFragment().Build(builder, registry, new LoadWanModelFragment.Parameters
        {
            ModelName = "steadydancer.safetensors",
            LoraName = "speed_lora.safetensors"
        });

        // 6. LoadWanVae
        new LoadWanVaeFragment().Build(builder, registry, new LoadWanVaeFragment.Parameters());

        // 7. LoadClipVision
        new LoadClipVisionFragment().Build(builder, registry, new LoadClipVisionFragment.Parameters());

        // 8. TextEncodeWan
        new TextEncodeWanFragment().Build(builder, registry, new TextEncodeWanFragment.Parameters
        {
            Positive = "a person dancing",
            Negative = ""
        });

        // 9. PoseDetection
        new PoseDetectionFragment().Build(builder, registry, new PoseDetectionFragment.Parameters());

        // 10. I2VEncode
        new I2VEncodeFragment().Build(builder, registry, new I2VEncodeFragment.Parameters());

        // 11. SteadyDancerEmbeds
        new SteadyDancerEmbedsFragment().Build(builder, registry, new SteadyDancerEmbedsFragment.Parameters());

        // 12. ContextOptions
        new ContextOptionsFragment().Build(builder, registry, new ContextOptionsFragment.Parameters());

        // 13. SamplerWan
        new SamplerWanFragment().Build(builder, registry, new SamplerWanFragment.Parameters
        {
            Steps = 4,
            Seed = 42
        });

        // 14. DecodeWan
        new DecodeWanFragment().Build(builder, registry, new DecodeWanFragment.Parameters());

        // Verify all expected registry outputs
        registry.HasOutput("model_output").Should().BeTrue();
        registry.HasOutput("vae_output").Should().BeTrue();
        registry.HasOutput("clip_vision_output").Should().BeTrue();
        registry.HasOutput("text_embeds").Should().BeTrue();
        registry.HasOutput("pose_images").Should().BeTrue();
        registry.HasOutput("image_embeds").Should().BeTrue();
        registry.HasOutput("steadydancer_embeds").Should().BeTrue();
        registry.HasOutput("context_options").Should().BeTrue();
        registry.HasOutput("latent_output").Should().BeTrue();
        registry.HasOutput("image_output").Should().BeTrue();

        // Verify node count:
        // LoadWanModel=6, LoadWanVae=1, LoadClipVision=1, TextEncode=1,
        // PoseDetection=4, I2VEncode=2, Embeds=4, ContextOpts=1, SamplerWan=3, DecodeWan=1
        // = 24 nodes
        builder.NodeCount.Should().Be(24);

        // Verify valid JSON
        var action = () => JsonDocument.Parse(builder.ToJson());
        action.Should().NotThrow();
    }

    #endregion

    #region Helpers

    private static NodeRegistry CreateI2VEncodeRegistry()
    {
        var registry = new NodeRegistry();
        registry.Register("clip_vision_output", "clip_vision_loader", 0);
        registry.Register("resized_image", "resize_subject", 0);
        registry.Register("width", "get_size", 1);
        registry.Register("height", "get_size", 2);
        registry.Register("num_frames", "get_size", 3);
        registry.Register("vae_output", "vae_loader", 0);
        return registry;
    }

    private static NodeRegistry CreateEmbedsRegistry()
    {
        var registry = new NodeRegistry();
        registry.Register("vae_output", "vae_loader", 0);
        registry.Register("pose_images", "resize_pose", 0);
        registry.Register("clip_vision_output", "clip_vision_loader", 0);
        registry.Register("image_embeds", "i2v_encode", 0);
        return registry;
    }

    private static NodeRegistry CreateSamplerWanRegistry()
    {
        var registry = new NodeRegistry();
        registry.Register("model_output", "set_loras", 0);
        registry.Register("steadydancer_embeds", "add_steadydancer", 0);
        registry.Register("text_embeds", "text_encode", 0);
        registry.Register("context_options", "context_opts", 0);
        return registry;
    }

    #endregion
}
