using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that creates PainterI2V video conditioning.
/// Takes 7 inputs from registry: image dimensions, prompts, VAE, CLIP vision, and start image.
/// Registers painter_positive_output, painter_negative_output, painter_latent_output.
/// </summary>
public class PainterI2VFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "painter_i2v",
        Type = FragmentType.Settings,
        Title = "Video Settings",
        Component = "PainterI2VForm",
        Icon = "fa-solid fa-video",
        Order = 40,
        Collapsible = false,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "length",
                Label = "Video Length",
                Type = ParameterType.Slider,
                Min = 17,
                Max = 257,
                Step = 8,
                DefaultValue = 81
            },
            new FragmentParameter
            {
                Name = "motion_amplitude",
                Label = "Motion Amplitude",
                Type = ParameterType.Slider,
                Min = 0.1,
                Max = 3.0,
                Step = 0.1,
                DefaultValue = 1.1
            },
            new FragmentParameter
            {
                Name = "shift",
                Label = "Shift",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 20,
                Step = 1,
                DefaultValue = 5
            }
        ]
    };

    public class Parameters
    {
        public int Length { get; set; } = 81;
        public int BatchSize { get; set; } = 1;
        public double MotionAmplitude { get; set; } = 1.1;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);

        BuildInternal(builder, registry, new Parameters
        {
            Length = fragment?.GetInt("length", 81) ?? 81,
            BatchSize = fragment?.GetInt("batch_size", 1) ?? 1,
            MotionAmplitude = fragment?.GetDouble("motion_amplitude", 1.1) ?? 1.1
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
        var nodeId = $"{scope}painter_i2v";
        var widthRef = registry.GetRef($"{scope}image_width_output");
        var heightRef = registry.GetRef($"{scope}image_height_output");
        var positiveRef = registry.GetRef($"{scope}positive_output");
        var negativeRef = registry.GetRef($"{scope}negative_output");
        var vaeRef = registry.GetRef($"{scope}vae_output");
        var clipVisionRef = registry.GetRef($"{scope}clip_vision_output");
        var imageRef = registry.GetRef($"{scope}image_output");

        builder.AddNode(nodeId, node => node
            .Type("PainterI2V")
            .Title($"{scopeTitle}PainterI2V")
            .InputRef("width", widthRef)
            .InputRef("height", heightRef)
            .Input("length", p.Length)
            .Input("batch_size", p.BatchSize)
            .Input("motion_amplitude", p.MotionAmplitude)
            .InputRef("positive", positiveRef)
            .InputRef("negative", negativeRef)
            .InputRef("vae", vaeRef)
            .InputRef("clip_vision_output", clipVisionRef)
            .InputRef("start_image", imageRef));

        registry.Register($"{scope}painter_positive_output", nodeId, 0);
        registry.Register($"{scope}painter_negative_output", nodeId, 1);
        registry.Register($"{scope}painter_latent_output", nodeId, 2);
    }
}
