using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Generic KSamplerAdvanced fragment with fully parameterized inputs.
/// No dual-model awareness - the workflow orchestrates high/low split logic.
/// UI metadata includes auto_split toggle for QoL auto-midpoint calculation.
/// </summary>
public class SamplerAdvancedFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "sampler_advanced",
        Type = FragmentType.Sampler,
        Title = "Video Sampling",
        Component = "DoubleSamplerForm",
        Icon = "fa-solid fa-dice",
        Order = 35,
        Collapsible = false,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "sampler_name",
                Label = "Sampler",
                Type = ParameterType.Select,
                Source = new DynamicSource("KSamplerAdvanced", "sampler_name")
            },
            new FragmentParameter
            {
                Name = "scheduler",
                Label = "Scheduler",
                Type = ParameterType.Select,
                Source = new DynamicSource("KSamplerAdvanced", "scheduler")
            },
            new FragmentParameter
            {
                Name = "steps",
                Label = "Steps",
                Type = ParameterType.Slider,
                Min = 2,
                Max = 50,
                Step = 2,
                DefaultValue = 8
            },
            new FragmentParameter
            {
                Name = "cfg",
                Label = "CFG Scale",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 15,
                Step = 0.5,
                DefaultValue = 1.0
            },
            new FragmentParameter
            {
                Name = "seed",
                Label = "Seed",
                Type = ParameterType.Number,
                Min = -1,
                DefaultValue = 42L
            },
            new FragmentParameter
            {
                Name = "auto_split",
                Label = "Auto Split Steps",
                Type = ParameterType.Checkbox,
                DefaultValue = true,
                Description = "Automatically split steps at midpoint between high/low samplers"
            }
        ]
    };

    public class Parameters
    {
        public string SamplerId { get; set; } = "sampler";
        public long Seed { get; set; } = 42;
        public int Steps { get; set; } = 8;
        public double Cfg { get; set; } = 1.0;
        public string SamplerName { get; set; } = "euler";
        public string Scheduler { get; set; } = "simple";
        public string AddNoise { get; set; } = "enable";
        public string ReturnWithLeftoverNoise { get; set; } = "disable";
        public int StartAtStep { get; set; } = 0;
        public int EndAtStep { get; set; } = 10000;
        public string ModelInputName { get; set; } = "model_output";
        public string PositiveInputName { get; set; } = "painter_positive_output";
        public string NegativeInputName { get; set; } = "painter_negative_output";
        public string LatentInputName { get; set; } = "painter_latent_output";
        public string LatentOutputName { get; set; } = "latent_output";
        public string Title { get; set; } = "KSampler (Advanced)";
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
            Seed = fragment?.GetLong("seed", 42) ?? 42,
            Steps = fragment?.GetInt("steps", 8) ?? 8,
            Cfg = fragment?.GetDouble("cfg", 1.0) ?? 1.0,
            SamplerName = fragment?.GetString("sampler_name", "euler") ?? "euler",
            Scheduler = fragment?.GetString("scheduler", "simple") ?? "simple"
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
        var nodeId = $"{scope}{p.SamplerId}";
        var modelRef = registry.GetRef(p.ModelInputName);
        var positiveRef = registry.GetRef(p.PositiveInputName);
        var negativeRef = registry.GetRef(p.NegativeInputName);
        var latentRef = registry.GetRef(p.LatentInputName);

        builder.AddNode(nodeId, node => node
            .Type("KSamplerAdvanced")
            .Title($"{scopeTitle}{p.Title}")
            .Input("add_noise", p.AddNoise)
            .Input("noise_seed", p.Seed)
            .Input("steps", p.Steps)
            .Input("cfg", p.Cfg)
            .Input("sampler_name", p.SamplerName)
            .Input("scheduler", p.Scheduler)
            .Input("start_at_step", p.StartAtStep)
            .Input("end_at_step", p.EndAtStep)
            .Input("return_with_leftover_noise", p.ReturnWithLeftoverNoise)
            .InputRef("model", modelRef)
            .InputRef("positive", positiveRef)
            .InputRef("negative", negativeRef)
            .InputRef("latent_image", latentRef));

        registry.Register(p.LatentOutputName, nodeId, 0);
    }
}
