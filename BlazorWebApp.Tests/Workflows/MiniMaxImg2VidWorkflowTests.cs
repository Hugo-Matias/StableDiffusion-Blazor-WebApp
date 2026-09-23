using System.Text.Json;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Templates.MiniMax;
using FluentAssertions;

namespace BlazorWebApp.Tests.Workflows;

public class MiniMaxImg2VidWorkflowTests
{
    private readonly MiniMaxImg2VidWorkflow _workflow = new();

    [Fact]
    public void GetFragments_ShouldExposeRtxUpscaleEnhancement()
    {
        var fragments = _workflow.GetFragments().Select(fragment => fragment.Metadata).ToList();

        var rtx = fragments.Should().Contain(fragment => fragment.Id == "rtx_upscale").Subject;
        rtx.Component.Should().Be("RTXUpscaleForm");
        rtx.Type.Should().Be(BlazorWebApp.Workflows.Models.FragmentType.Enhancement);
        rtx.Collapsible.Should().BeTrue();
    }

    [Fact]
    public void Build_WithoutRtxUpscale_ShouldNotContainRtxNode()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters()).Json);

        json.RootElement.TryGetProperty("rtx_upscale", out _).Should().BeFalse();
        InputRef(Inputs(json, "minimax_create_video"), "images").Should().Equal("minimax_vae_decode", "0");
    }

    [Fact]
    public void Build_WithRtxUpscaleActive_ShouldWireBetweenDecodeAndCreateVideo()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(rtxActive: true)).Json);

        NodeType(json, "rtx_upscale").Should().Be("RTXVideoSuperResolution");
        InputRef(Inputs(json, "rtx_upscale"), "images").Should().Equal("minimax_vae_decode", "0");
        Inputs(json, "rtx_upscale").GetProperty("resize_type").GetString().Should().Be("scale by multiplier");
        Inputs(json, "rtx_upscale").GetProperty("resize_type.scale").GetDouble().Should().Be(2.0);
        Inputs(json, "rtx_upscale").GetProperty("quality").GetString().Should().Be("ULTRA");

        InputRef(Inputs(json, "minimax_create_video"), "images").Should().Equal("rtx_upscale", "0");
    }

    [Fact]
    public void Build_WithRtxUpscaleDimensionsMode_ShouldUseTargetDimensions()
    {
        using var json = JsonDocument.Parse(_workflow.Build(CreateParameters(rtxActive: true, rtxResizeMode: "dimensions", rtxWidth: 1280, rtxHeight: 720)).Json);

        var rtxInputs = Inputs(json, "rtx_upscale");
        rtxInputs.GetProperty("resize_type").GetString().Should().Be("target dimensions");
        rtxInputs.GetProperty("resize_type.width").GetInt32().Should().Be(1280);
        rtxInputs.GetProperty("resize_type.height").GetInt32().Should().Be(720);
    }

    private static GenerationParameters CreateParameters(
        bool rtxActive = false,
        string rtxResizeMode = "multiplier",
        double rtxScale = 2.0,
        int rtxWidth = 1920,
        int rtxHeight = 1080,
        string rtxQuality = "ULTRA")
    {
        var parameters = new GenerationParameters
        {
            Assets = new Dictionary<string, string>
            {
                ["UNet"] = "minimax_h3_fl2va_pruned_int8_convrot.safetensors",
                ["Clip"] = "qwen3vl_32b_minimax_h3_nvfp4_awq.safetensors",
                ["VideoVae"] = "minimax_h3_video_vae_fp16.safetensors",
                ["AudioVae"] = "minimax_h3_audio_vae_fp32.safetensors"
            }
        };

        var prompts = parameters.GetOrCreateFragment("prompts");
        prompts.SetValue("positive", "a cat walking");

        var rtx = parameters.GetOrCreateFragment("rtx_upscale");
        rtx.IsActive = rtxActive;
        rtx.SetValue("resize_mode", rtxResizeMode);
        rtx.SetValue("scale", rtxScale);
        rtx.SetValue("width", rtxWidth);
        rtx.SetValue("height", rtxHeight);
        rtx.SetValue("quality", rtxQuality);

        return parameters;
    }

    private static string NodeType(JsonDocument json, string nodeId)
    {
        return json.RootElement.GetProperty(nodeId).GetProperty("class_type").GetString()!;
    }

    private static JsonElement Inputs(JsonDocument json, string nodeId)
    {
        return json.RootElement.GetProperty(nodeId).GetProperty("inputs");
    }

    private static string[] InputRef(JsonElement inputs, string inputName)
    {
        return inputs.GetProperty(inputName).EnumerateArray()
            .Select(value => value.ValueKind == JsonValueKind.Number ? value.GetInt32().ToString() : value.GetString()!)
            .ToArray();
    }
}
