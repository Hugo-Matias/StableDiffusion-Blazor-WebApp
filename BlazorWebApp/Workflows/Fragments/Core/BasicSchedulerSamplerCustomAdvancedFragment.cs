using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Custom advanced sampler pipeline backed by BasicScheduler.
/// Composes RandomNoise -> KSamplerSelect -> BasicScheduler -> CFGGuider -> SamplerCustomAdvanced.
/// </summary>
public class BasicSchedulerSamplerCustomAdvancedFragment : IFragmentBuilder
{
    public Parameters Defaults { get; init; } = new();

    public FragmentMetadata Metadata => new()
    {
        Id = "main_sampler",
        Type = FragmentType.Sampler,
        Title = "Sampler",
        Component = "SamplerForm",
        Icon = "fa-solid fa-dice",
        Order = 50,
        Collapsible = true,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "sampler_name",
                Label = "Sampler",
                Type = ParameterType.Select,
                DefaultValue = Defaults.SamplerName,
                Source = new DynamicSource("KSamplerSelect", "sampler_name")
            },
            new FragmentParameter
            {
                Name = "scheduler",
                Label = "Scheduler",
                Type = ParameterType.Select,
                DefaultValue = Defaults.Scheduler,
                Source = new DynamicSource("BasicScheduler", "scheduler")
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
                Name = "denoise",
                Label = "Denoise",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 1,
                Step = 0.01,
                DefaultValue = Defaults.Denoise
            },
            new FragmentParameter
            {
                Name = "seed",
                Label = "Seed",
                Type = ParameterType.Number,
                Min = -1,
                DefaultValue = Defaults.Seed
            }
        ]
    };

    public class Parameters
    {
        public string SamplerId { get; set; } = "sampler_main";
        public string Title { get; set; } = "SamplerCustomAdvanced";
        public string SamplerName { get; set; } = "euler";
        public string Scheduler { get; set; } = "simple";
        public int Steps { get; set; } = 20;
        public double Cfg { get; set; } = 8.0;
        public double Denoise { get; set; } = 1.0;
        public long Seed { get; set; } = -1;
        public string Scope { get; set; } = "";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        var seed = fragment?.GetLong("seed", Defaults.Seed) ?? Defaults.Seed;
        if (seed < 0)
        {
            seed = Random.Shared.NextInt64(0, int.MaxValue);
        }

        BuildInternal(builder, registry, new Parameters
        {
            SamplerId = fragment?.GetString("sampler_id", Defaults.SamplerId) ?? Defaults.SamplerId,
            Title = fragment?.GetString("title", Defaults.Title) ?? Defaults.Title,
            SamplerName = fragment?.GetString("sampler_name", Defaults.SamplerName) ?? Defaults.SamplerName,
            Scheduler = fragment?.GetString("scheduler", Defaults.Scheduler) ?? Defaults.Scheduler,
            Steps = fragment?.GetInt("steps", Defaults.Steps) ?? Defaults.Steps,
            Cfg = fragment?.GetDouble("cfg", Defaults.Cfg) ?? Defaults.Cfg,
            Denoise = fragment?.GetDouble("denoise", Defaults.Denoise) ?? Defaults.Denoise,
            Seed = seed,
            Scope = scope
        });
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams)
    {
        BuildInternal(builder, registry, fragmentParams);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p)
    {
        var scope = p.Scope;
        var modelRef = registry.GetRef($"{scope}model_output");
        var positiveRef = registry.GetRef($"{scope}positive_output");
        var negativeRef = registry.GetRef($"{scope}negative_output");
        var latentRef = registry.GetRef($"{scope}latent_output");

        var noiseId = $"{scope}{p.SamplerId}_noise";
        var selectId = $"{scope}{p.SamplerId}_select";
        var schedulerId = $"{scope}{p.SamplerId}_scheduler";
        var guiderId = $"{scope}{p.SamplerId}_guider";

        builder.AddNode(noiseId, node => node
            .Type("RandomNoise")
            .Title("RandomNoise")
            .Input("noise_seed", p.Seed));

        builder.AddNode(selectId, node => node
            .Type("KSamplerSelect")
            .Title("KSamplerSelect")
            .Input("sampler_name", p.SamplerName));

        builder.AddNode(schedulerId, node => node
            .Type("BasicScheduler")
            .Title("BasicScheduler")
            .InputRef("model", modelRef)
            .Input("scheduler", p.Scheduler)
            .Input("steps", p.Steps)
            .Input("denoise", p.Denoise));

        builder.AddNode(guiderId, node => node
            .Type("CFGGuider")
            .Title("CFGGuider")
            .InputRef("model", modelRef)
            .InputRef("positive", positiveRef)
            .InputRef("negative", negativeRef)
            .Input("cfg", p.Cfg));

        builder.AddNode(p.SamplerId, node => node
            .Type("SamplerCustomAdvanced")
            .Title(p.Title)
            .InputRef("noise", (noiseId, 0))
            .InputRef("guider", (guiderId, 0))
            .InputRef("sampler", (selectId, 0))
            .InputRef("sigmas", (schedulerId, 0))
            .InputRef("latent_image", latentRef));

        registry.Register("latent_output", p.SamplerId, 0);
    }
}