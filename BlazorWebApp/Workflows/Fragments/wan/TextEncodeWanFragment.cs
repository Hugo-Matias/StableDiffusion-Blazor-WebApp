using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that encodes text using WanVideoTextEncodeCached.
/// Registers text_embeds.
/// </summary>
public class TextEncodeWanFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "text_encode_wan",
        Type = FragmentType.Prompts,
        Title = "WanVideo Text Encode",
        IsHidden = true
    };

    public class Parameters
    {
        public string TextEncoderName { get; set; } = "umt5_xxl_fp16.safetensors";
        public string Precision { get; set; } = "bf16";
        public string Positive { get; set; } = "";
        public string Negative { get; set; } = "";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment("prompts");

        BuildInternal(builder, registry, new Parameters
        {
            Positive = fragment?.GetString("positive", "") ?? "",
            Negative = fragment?.GetString("negative", "") ?? ""
        }, scope, scopeTitle);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, fragmentParams, scope, scopeTitle);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope,
        string scopeTitle)
    {
        var nodeId = $"{scope}text_encode";

        builder.AddNode(nodeId, node => node
            .Type("WanVideoTextEncodeCached")
            .Title($"{scopeTitle}WanVideo TextEncode")
            .Input("model_name", p.TextEncoderName)
            .Input("precision", p.Precision)
            .Input("positive_prompt", p.Positive)
            .Input("negative_prompt", p.Negative)
            .Input("quantization", "disabled")
            .Input("use_disk_cache", true)
            .Input("device", "gpu"));

        registry.Register($"{scope}text_embeds", nodeId, 0);
    }
}
