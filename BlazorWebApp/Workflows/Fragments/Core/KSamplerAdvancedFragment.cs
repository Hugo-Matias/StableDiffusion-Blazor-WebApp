using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment for ComfyUI's KSamplerAdvanced node, used when a workflow needs the
/// first pass to leave a latent for a second sampler.
/// </summary>
public class KSamplerAdvancedFragment : IFragmentBuilder
{
    public string Id { get; init; } = "base_sampler";
    public string Title { get; init; } = "Base Sampler";
    public FragmentType Type { get; init; } = FragmentType.Sampler;
    public int Order { get; init; } = 50;
    public bool Collapsible { get; init; } = false;
    public bool? DefaultActive { get; init; } = true;
    public Parameters Defaults { get; init; } = new();

    public FragmentMetadata Metadata => new()
    {
        Id = Id,
        Type = Type,
        Title = Title,
        Component = "SamplerForm",
        Icon = "fa-solid fa-dice",
        Order = Order,
        Collapsible = Collapsible,
        DefaultActive = DefaultActive,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "sampler_name",
                Label = "Sampler",
                Type = ParameterType.Select,
                DefaultValue = Defaults.SamplerName,
                Source = new DynamicSource("KSamplerAdvanced", "sampler_name")
            },
            new FragmentParameter
            {
                Name = "scheduler",
                Label = "Scheduler",
                Type = ParameterType.Select,
                DefaultValue = Defaults.Scheduler,
                Source = new DynamicSource("KSamplerAdvanced", "scheduler")
            },
            new FragmentParameter
            {
                Name = "steps",
                Label = "Steps",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 150,
                Step = 1,
                DefaultValue = Defaults.Steps
            },
            new FragmentParameter
            {
                Name = "cfg",
                Label = "CFG Scale",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 30,
                Step = 0.5,
                DefaultValue = Defaults.Cfg
            },
            new FragmentParameter
            {
                Name = "seed",
                Label = "Seed",
                Type = ParameterType.Number,
                Min = -1,
                DefaultValue = -1L
            }
        ]
    };

    public class Parameters
    {
        public string SamplerId { get; set; } = "sampler_base";
        public string Title { get; set; } = "Base Sampler";
        public string SamplerName { get; set; } = "res_multistep";
        public string Scheduler { get; set; } = "simple";
        public int Steps { get; set; } = 8;
        public double Cfg { get; set; } = 4.0;
        public long Seed { get; set; } = -1;
        public string AddNoise { get; set; } = "enable";
        public int StartAtStep { get; set; } = 0;
        public int EndAtStep { get; set; } = 20;
        public string ReturnWithLeftoverNoise { get; set; } = "disable";
        public (string nodeId, int outputIndex)? ModelOverride { get; set; }
        public bool RegisterLatentOutput { get; set; } = true;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Id);
        var seed = fragment?.GetLong("seed", -1) ?? -1;
        if (seed < 0) seed = Random.Shared.NextInt64(0, int.MaxValue);

        BuildInternal(builder, registry, new Parameters
        {
            SamplerId = Id == "base_sampler" ? "sampler_base" : Id,
            Title = Title,
            SamplerName = fragment?.GetString("sampler_name", Defaults.SamplerName) ?? Defaults.SamplerName,
            Scheduler = fragment?.GetString("scheduler", Defaults.Scheduler) ?? Defaults.Scheduler,
            Steps = fragment?.GetInt("steps", Defaults.Steps) ?? Defaults.Steps,
            Cfg = fragment?.GetDouble("cfg", Defaults.Cfg) ?? Defaults.Cfg,
            Seed = seed,
            AddNoise = Defaults.AddNoise,
            StartAtStep = Defaults.StartAtStep,
            EndAtStep = Defaults.EndAtStep,
            ReturnWithLeftoverNoise = Defaults.ReturnWithLeftoverNoise
        }, scope);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "")
    {
        BuildInternal(builder, registry, fragmentParams, scope);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope)
    {
        var modelRef = p.ModelOverride ?? registry.GetRef($"{scope}model_output");
        var positiveRef = registry.GetRef($"{scope}positive_output");
        var negativeRef = registry.GetRef($"{scope}negative_output");
        var latentRef = registry.GetRef($"{scope}latent_output");

        builder.AddNode(p.SamplerId, node => node
            .Type("KSamplerAdvanced")
            .Title(p.Title)
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

        if (p.RegisterLatentOutput)
        {
            registry.Register("latent_output", p.SamplerId, 0);
        }
    }
}