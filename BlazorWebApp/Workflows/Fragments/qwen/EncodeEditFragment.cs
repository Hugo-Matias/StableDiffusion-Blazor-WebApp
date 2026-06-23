using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Qwen;

/// <summary>
/// Fragment that encodes positive and negative prompts for Qwen image editing.
/// Uses TextEncodeQwenImageEditPlus which requires clip, vae, and image references.
/// Registers positive_output and negative_output.
/// </summary>
public class EncodeEditFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "encode_edit",
        Type = FragmentType.Conditioning,
        Title = "Encode Edit (Qwen)",
        IsHidden = true
    };

    /// <summary>
    /// Parameters for the encode edit fragment.
    /// </summary>
    public class Parameters
    {
        public string Positive { get; set; } = "";
        public string Negative { get; set; } = "";
        public string ImageRef { get; set; } = "image_input";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        var promptsFragment = parameters.GetFragment("prompts");

        BuildInternal(builder, registry, new Parameters
        {
            Positive = fragment?.GetString("positive") ?? promptsFragment?.GetString("positive", "") ?? "",
            Negative = fragment?.GetString("negative") ?? promptsFragment?.GetString("negative", "") ?? ""
        }, scope, scopeTitle);
    }

    /// <summary>
    /// Builds the fragment with explicit parameters.
    /// </summary>
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
        var clipRef = registry.GetRef($"{scope}clip_output");
        var vaeRef = registry.GetRef($"{scope}vae_output");
        var imageRef = registry.GetRef(p.ImageRef);

        // Encode Positive
        builder.AddNode($"{scope}encode_positive", node => node
            .Type("TextEncodeQwenImageEditPlus")
            .Title($"{scopeTitle}Encode Positive (Qwen Edit)")
            .Input("prompt", p.Positive)
            .InputRef("clip", clipRef)
            .InputRef("vae", vaeRef)
            .InputRef("image1", imageRef));

        // Encode Negative
        builder.AddNode($"{scope}encode_negative", node => node
            .Type("TextEncodeQwenImageEditPlus")
            .Title($"{scopeTitle}Encode Negative (Qwen Edit)")
            .Input("prompt", p.Negative)
            .InputRef("clip", clipRef)
            .InputRef("vae", vaeRef)
            .InputRef("image1", imageRef));

        // Register outputs
        registry.Register($"{scope}positive_output", $"{scope}encode_positive", 0);
        registry.Register($"{scope}negative_output", $"{scope}encode_negative", 0);
    }
}
