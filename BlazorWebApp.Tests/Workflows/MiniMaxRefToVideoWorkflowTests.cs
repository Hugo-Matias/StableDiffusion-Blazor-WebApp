using System.Text.Json;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Templates.MiniMax;
using FluentAssertions;

namespace BlazorWebApp.Tests.Workflows;

public class MiniMaxRefToVideoWorkflowTests
{
    private readonly MiniMaxRefToVideoWorkflow _workflow = new();

    [Fact]
    public void GetFragments_ShouldExposeRtxUpscaleEnhancement()
    {
        var fragments = _workflow.GetFragments().Select(fragment => fragment.Metadata).ToList();

        var rtx = fragments.Should().Contain(fragment => fragment.Id == "rtx_upscale").Subject;
        rtx.Component.Should().Be("RTXUpscaleForm");
        rtx.Type.Should().Be(BlazorWebApp.Workflows.Models.FragmentType.Enhancement);
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
        Inputs(json, "rtx_upscale").GetProperty("quality").GetString().Should().Be("ULTRA");
        InputRef(Inputs(json, "minimax_create_video"), "images").Should().Equal("rtx_upscale", "0");
    }

    private static GenerationParameters CreateParameters(bool rtxActive = false)
    {
        var parameters = new GenerationParameters
        {
            Assets = new Dictionary<string, string>
            {
                ["UNet"] = "minimax_h3_ref2va_pruned_int8_convrot.safetensors",
                ["Clip"] = "qwen3vl_32b_minimax_h3_nvfp4_awq.safetensors",
                ["VideoVae"] = "minimax_h3_video_vae_fp16.safetensors",
                ["AudioVae"] = "minimax_h3_audio_vae_fp32.safetensors"
            }
        };

        var prompts = parameters.GetOrCreateFragment("prompts");
        prompts.SetValue("positive", "<Picture 1> walking");

        var rtx = parameters.GetOrCreateFragment("rtx_upscale");
        rtx.IsActive = rtxActive;

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
