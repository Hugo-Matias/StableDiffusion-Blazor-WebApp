using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that runs a standard KSampler node for image generation.
/// Used by StableDiffusion and Qwen workflows that don't need the ClownsharK sampler.
/// Registers latent_output in the node registry (overwrites previous latent).
/// </summary>
public class SamplerStandardFragment : IFragmentBuilder
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
                Name = "seed",
                Label = "Seed",
                Type = ParameterType.Number,
                Min = -1,
                DefaultValue = -1L
            }
        ]
    };

    /// <summary>
    /// Parameters for the standard sampler fragment.
    /// </summary>
    public class Parameters
    {
        public string SamplerId { get; set; } = "sampler_main";
        public string Title { get; set; } = "KSampler";
        public string SamplerName { get; set; } = "euler";
        public string Scheduler { get; set; } = "normal";
        public int Steps { get; set; } = 20;
        public double Cfg { get; set; } = 7.0;
        public double Denoise { get; set; } = 1.0;
        public long Seed { get; set; } = 42;
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

        BuildInternal(builder, registry, new Parameters
        {
            SamplerId = fragment?.GetString("sampler_id", "sampler_main") ?? "sampler_main",
            Title = fragment?.GetString("title", "KSampler") ?? "KSampler",
            SamplerName = fragment?.GetString("sampler_name", "euler") ?? "euler",
            Scheduler = fragment?.GetString("scheduler", "normal") ?? "normal",
            Steps = fragment?.GetInt("steps", 20) ?? 20,
            Cfg = fragment?.GetDouble("cfg", 7.0) ?? 7.0,
            Denoise = fragment?.GetDouble("denoise", 1.0) ?? 1.0,
            Seed = fragment?.GetLong("seed", 42) ?? 42,
            Scope = scope
        });
    }

    /// <summary>
    /// Builds the fragment with explicit parameters.
    /// </summary>
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

        builder.AddNode(p.SamplerId, node => node
            .Type("KSampler")
            .Title(p.Title)
            .Input("seed", p.Seed)
            .Input("steps", p.Steps)
            .Input("cfg", p.Cfg)
            .Input("sampler_name", p.SamplerName)
            .Input("scheduler", p.Scheduler)
            .Input("denoise", p.Denoise)
            .InputRef("model", modelRef)
            .InputRef("positive", positiveRef)
            .InputRef("negative", negativeRef)
            .InputRef("latent_image", latentRef));

        registry.Register("latent_output", p.SamplerId, 0);
    }
}
