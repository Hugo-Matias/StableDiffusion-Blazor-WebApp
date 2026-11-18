using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

public class WanFunInpaintSettingsFragment : IFragmentBuilder
{
    public const string FragmentId = "wan_fun_inpaint_settings";

    public Parameters Defaults { get; init; } = new();

    public FragmentMetadata Metadata => new()
    {
        Id = FragmentId,
        Type = FragmentType.Settings,
        Title = "Video Settings",
        Component = "WanFunInpaintSettingsForm",
        Icon = "fa-solid fa-video",
        Order = 30,
        Collapsible = false,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "length",
                Label = "Video Length",
                Type = ParameterType.Number,
                Min = 1,
                Max = 257,
                Step = 4,
                DefaultValue = Defaults.Length
            },
            new FragmentParameter
            {
                Name = "shift",
                Label = "Model Shift",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 20,
                Step = 0.01,
                DefaultValue = Defaults.Shift
            },
            new FragmentParameter
            {
                Name = "high_speed_lora_strength",
                Label = "High Speed LoRA Strength",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 2,
                Step = 0.05,
                DefaultValue = Defaults.HighSpeedLoraStrength
            },
            new FragmentParameter
            {
                Name = "low_speed_lora_strength",
                Label = "Low Speed LoRA Strength",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 2,
                Step = 0.05,
                DefaultValue = Defaults.LowSpeedLoraStrength
            }
        ]
    };

    public class Parameters
    {
        public int Length { get; set; } = 81;
        public double Shift { get; set; } = 8;
        public double HighSpeedLoraStrength { get; set; } = 1;
        public double LowSpeedLoraStrength { get; set; } = 1;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
    }
}
