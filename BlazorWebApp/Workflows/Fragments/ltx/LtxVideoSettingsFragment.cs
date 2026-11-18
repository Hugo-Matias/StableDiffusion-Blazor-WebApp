using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// UI-only fragment that exposes LTX video generation settings.
/// Parameters are consumed by the workflow's Build() method.
/// No nodes are created by this fragment.
/// </summary>
public class LtxVideoSettingsFragment : IFragmentBuilder
{
    private readonly bool _includeResolution;

    public LtxVideoSettingsFragment(bool includeResolution = true)
    {
        _includeResolution = includeResolution;
    }

    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_video_settings",
        Type = FragmentType.Settings,
        Title = "Video Settings",
        Component = "LtxVideoSettingsForm",
        Icon = "fa-solid fa-video",
        Order = 30,
        Collapsible = false,
        Parameters = BuildParameters()
    };

    private IEnumerable<FragmentParameter> BuildParameters()
    {
        if (_includeResolution)
        {
            yield return new FragmentParameter
            {
                Name = "width",
                Label = "Width",
                Type = ParameterType.Slider,
                Min = 256,
                Max = 1920,
                Step = 32,
                DefaultValue = 1280,
                Description = "Used by Txt2Vid workflows; Img2Vid reads dimensions from the source image."
            };
            yield return new FragmentParameter
            {
                Name = "height",
                Label = "Height",
                Type = ParameterType.Slider,
                Min = 256,
                Max = 1920,
                Step = 32,
                DefaultValue = 720,
                Description = "Used by Txt2Vid workflows; Img2Vid reads dimensions from the source image."
            };
        }

        yield return new FragmentParameter
        {
            Name = "duration",
            Label = "Duration (seconds)",
            Type = ParameterType.Slider,
            Min = 1,
            Max = 15,
            Step = 1,
            DefaultValue = 5
        };
        yield return new FragmentParameter
        {
            Name = "frame_rate",
            Label = "Frame Rate",
            Type = ParameterType.Slider,
            Min = 8,
            Max = 30,
            Step = 1,
            DefaultValue = 25
        };
        yield return new FragmentParameter
        {
            Name = "img_compression",
            Label = "Image Compression",
            Type = ParameterType.Slider,
            Min = 1,
            Max = 30,
            Step = 1,
            DefaultValue = 18
        };
        yield return new FragmentParameter
        {
            Name = "i2v_strength",
            Label = "I2V Strength",
            Type = ParameterType.Slider,
            Min = 0.0,
            Max = 1.0,
            Step = 0.05,
            DefaultValue = 0.7
        };
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        // UI-only fragment - parameters are consumed by the workflow's Build() method
    }
}
