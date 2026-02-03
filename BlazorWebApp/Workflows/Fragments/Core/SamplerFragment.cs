using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that runs the KSampler to generate latent images.
/// Registers latent_output in the node registry (overwrites previous latent).
/// </summary>
public class SamplerFragment : IFragmentBuilder
{
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
                Source = new DynamicSource("Backend", "Samplers")
            },
            new FragmentParameter
            {
                Name = "scheduler",
                Label = "Scheduler",
                Type = ParameterType.Select,
                Source = new DynamicSource("Backend", "Schedulers")
            },
            new FragmentParameter
            {
                Name = "steps",
                Label = "Steps",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 150,
                Step = 1,
                DefaultValue = 20
            },
            new FragmentParameter
            {
                Name = "cfg",
                Label = "CFG Scale",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 30,
                Step = 0.5,
                DefaultValue = 7.0
            },
            new FragmentParameter
            {
                Name = "denoise",
                Label = "Denoise",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 1,
                Step = 0.01,
                DefaultValue = 1.0
            },
            new FragmentParameter
            {
                Name = "eta",
                Label = "Eta",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 1,
                Step = 0.01,
                DefaultValue = 0.5
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

    /// <summary>
    /// Parameters for the sampler fragment.
    /// </summary>
    public class Parameters
    {
        public string SamplerId { get; set; } = "sampler_main";
        public string Title { get; set; } = "Sampler";
        public string SamplerName { get; set; } = "euler";
        public string Scheduler { get; set; } = "normal";
        public int Steps { get; set; } = 20;
        public double Cfg { get; set; } = 7.0;
        public double Denoise { get; set; } = 1.0;
        public double Eta { get; set; } = 0.5;
        public long Seed { get; set; } = -1;
        public int StepsToRun { get; set; } = -1;
        public string SamplerMode { get; set; } = "standard";
        public string ClassType { get; set; } = "ClownsharKSampler_Beta";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);

        var samplerId = fragment?.GetString("sampler_id", "sampler_main") ?? "sampler_main";
        var title = fragment?.GetString("title", "Sampler") ?? "Sampler";
        var samplerName = fragment?.GetString("sampler_name", "euler") ?? "euler";
        var scheduler = fragment?.GetString("scheduler", "normal") ?? "normal";
        var steps = fragment?.GetInt("steps", 20) ?? 20;
        var cfg = fragment?.GetDouble("cfg", 7.0) ?? 7.0;
        var denoise = fragment?.GetDouble("denoise", 1.0) ?? 1.0;
        var eta = fragment?.GetDouble("eta", 0.5) ?? 0.5;
        var seed = fragment?.GetLong("seed", -1) ?? -1;
        var stepsToRun = fragment?.GetInt("steps_to_run", -1) ?? -1;
        var samplerMode = fragment?.GetString("sampler_mode", "standard") ?? "standard";
        var classType = fragment?.GetString("class_type", "ClownsharKSampler_Beta") ?? "ClownsharKSampler_Beta";

        BuildInternal(builder, registry, new Parameters
        {
            SamplerId = samplerId,
            Title = title,
            SamplerName = samplerName,
            Scheduler = scheduler,
            Steps = steps,
            Cfg = cfg,
            Denoise = denoise,
            Eta = eta,
            Seed = seed,
            StepsToRun = stepsToRun,
            SamplerMode = samplerMode,
            ClassType = classType
        }, scope);
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
        // Get references from registry with scope support
        var modelRef = registry.GetRef($"{scope}model_output");
        var positiveRef = registry.GetRef($"{scope}positive_output");
        var negativeRef = registry.GetRef($"{scope}negative_output");
        var latentRef = registry.GetRef($"{scope}latent_output");

        builder.AddNode(p.SamplerId, node => node
            .Type(p.ClassType)
            .Title(p.Title)
            .Input("eta", p.Eta)
            .Input("sampler_name", p.SamplerName)
            .Input("scheduler", p.Scheduler)
            .Input("steps", p.Steps)
            .Input("steps_to_run", p.StepsToRun)
            .Input("denoise", p.Denoise)
            .Input("cfg", p.Cfg)
            .Input("seed", p.Seed)
            .Input("sampler_mode", p.SamplerMode)
            .Input("bongmath", true)
            .InputRef("model", modelRef)
            .InputRef("positive", positiveRef)
            .InputRef("negative", negativeRef)
            .InputRef("latent_image", latentRef));

        // Overwrite latent_output with sampler output
        registry.Register("latent_output", p.SamplerId, 0);
    }
}
