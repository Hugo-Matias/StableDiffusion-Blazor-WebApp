using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Enhancements;

/// <summary>
/// Fragment that enhances seed variance for more diverse outputs.
/// Overwrites positive_output in the node registry.
/// Conditional: Only builds when seed_variance_enhancer.IsActive is true.
/// </summary>
public class SeedVarianceEnhancerFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "seed_variance_enhancer",
        Type = FragmentType.Enhancement,
        Title = "Seed Variance Enhancer",
        Component = "SeedVarianceEnhancerForm",
        Icon = "fa-solid fa-shuffle",
        Order = 80,
        Collapsible = true,
        DefaultCollapsed = true,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "randomize_percent",
                Label = "Randomize Percent",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 100,
                Step = 5,
                DefaultValue = 50
            },
            new FragmentParameter
            {
                Name = "strength",
                Label = "Strength",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 100,
                Step = 1,
                DefaultValue = 20
            },
            new FragmentParameter
            {
                Name = "noise_insert",
                Label = "Noise Insert",
                Type = ParameterType.Select,
                Options = ["noise on beginning steps", "noise on ending steps", "noise on all steps", "disabled"],
                DefaultValue = "noise on beginning steps"
            },
            new FragmentParameter
            {
                Name = "steps_switchover_percent",
                Label = "Steps Switchover %",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 100,
                Step = 5,
                DefaultValue = 20
            },
            new FragmentParameter
            {
                Name = "seed",
                Label = "Seed",
                Type = ParameterType.Number,
                Min = -1,
                DefaultValue = 0L
            },
            new FragmentParameter
            {
                Name = "mask_starts_at",
                Label = "Mask Starts At",
                Type = ParameterType.Select,
                Options = ["beginning", "end"],
                DefaultValue = "beginning"
            },
            new FragmentParameter
            {
                Name = "mask_percent",
                Label = "Mask Percent",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 100,
                Step = 5,
                DefaultValue = 0
            },
            new FragmentParameter
            {
                Name = "log_to_console",
                Label = "Log to Console",
                Type = ParameterType.Checkbox,
                DefaultValue = false
            }
        ]
    };

    /// <summary>
    /// Parameters for the seed variance enhancer fragment.
    /// </summary>
    public class Parameters
    {
        public int RandomizePercent { get; set; } = 50;
        public int Strength { get; set; } = 20;
        public string NoiseInsert { get; set; } = "noise on beginning steps";
        public int StepsSwitchoverPercent { get; set; } = 20;
        public long Seed { get; set; } = 0;
        public string MaskStartsAt { get; set; } = "beginning";
        public int MaskPercent { get; set; } = 0;
        public bool LogToConsole { get; set; } = false;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        
        // Check if fragment is active
        if (fragment?.IsActive != true)
            return;

        var p = new Parameters
        {
            RandomizePercent = fragment.GetInt("randomize_percent", 50),
            Strength = fragment.GetInt("strength", 20),
            NoiseInsert = fragment.GetString("noise_insert", "noise on beginning steps"),
            StepsSwitchoverPercent = fragment.GetInt("steps_switchover_percent", 20),
            Seed = fragment.GetLong("seed", 0),
            MaskStartsAt = fragment.GetString("mask_starts_at", "beginning"),
            MaskPercent = fragment.GetInt("mask_percent", 0),
            LogToConsole = fragment.GetBool("log_to_console", false)
        };

        BuildInternal(builder, registry, p, scope);
    }

    /// <summary>
    /// Builds the fragment with explicit parameters.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "")
    {
        BuildInternal(builder, registry, fragmentParams, scope);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters p,
        string scope)
    {
        // Get positive conditioning reference
        var positiveRef = registry.GetRef($"{scope}positive_output");

        builder.AddNode("seed_variance_enhancer", node => node
            .Type("SeedVarianceEnhancer")
            .Title("SeedVarianceEnhancer")
            .Input("randomize_percent", p.RandomizePercent)
            .Input("strength", p.Strength)
            .Input("noise_insert", p.NoiseInsert)
            .Input("steps_switchover_percent", p.StepsSwitchoverPercent)
            .Input("seed", p.Seed)
            .Input("mask_starts_at", p.MaskStartsAt)
            .Input("mask_percent", p.MaskPercent)
            .Input("log_to_console", p.LogToConsole)
            .InputRef("conditioning", positiveRef));

        // Overwrite positive_output with enhanced version
        registry.Register("positive_output", "seed_variance_enhancer", 0);
    }
}
