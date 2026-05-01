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
    /// <summary>
    /// Workflow-specific defaults. Set once per workflow in the field initializer using the
    /// existing <see cref="Parameters"/> type. Both <see cref="Metadata"/> (for first-load
    /// UI initialization) and the <see cref="Build(ComfyWorkflowBuilder, GenerationParameters, Builders.NodeRegistry, string, string)"/>
    /// fallback values read from here, making this the single source of truth.
    /// </summary>
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
                Source = new DynamicSource("ClownsharKSampler_Beta", "sampler_name")
            },
            new FragmentParameter
            {
                Name = "scheduler",
                Label = "Scheduler",
                Type = ParameterType.Select,
                DefaultValue = Defaults.Scheduler,
                Source = new DynamicSource("ClownsharKSampler_Beta", "scheduler")
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
                Min = 1,
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
                Name = "eta",
                Label = "Eta",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 1,
                Step = 0.01,
                DefaultValue = Defaults.Eta
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

        /// <summary>
        /// Optional override for the model input. When set, takes precedence over the
        /// scoped <c>model_output</c> registry lookup. Used by workflows that need to
        /// route a sampler to a model branch other than the main pipeline (e.g. a
        /// per-sampler LoRA stack).
        /// </summary>
        public (string nodeId, int outputIndex)? ModelOverride { get; set; }

        /// <summary>
        /// Optional reference to a node that produces a sampler <c>OPTIONS</c> bundle
        /// (e.g. <c>SharkOptions_Beta</c>). When set, the sampler wires the <c>options</c>
        /// input. Only meaningful for ClownsharKSampler-style class types.
        /// </summary>
        public (string nodeId, int outputIndex)? OptionsRef { get; set; }

        /// <summary>
        /// When false, the sampler does not overwrite <c>latent_output</c>. Use for
        /// branched topologies where a downstream stage needs both the original and
        /// sampled latents. Default true preserves the standard pipeline contract.
        /// </summary>
        public bool RegisterLatentOutput { get; set; } = true;
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
        var samplerName = fragment?.GetString("sampler_name", Defaults.SamplerName) ?? Defaults.SamplerName;
        var scheduler = fragment?.GetString("scheduler", Defaults.Scheduler) ?? Defaults.Scheduler;
        var steps = fragment?.GetInt("steps", Defaults.Steps) ?? Defaults.Steps;
        var cfg = fragment?.GetDouble("cfg", Defaults.Cfg) ?? Defaults.Cfg;
        var denoise = fragment?.GetDouble("denoise", Defaults.Denoise) ?? Defaults.Denoise;
        var eta = fragment?.GetDouble("eta", Defaults.Eta) ?? Defaults.Eta;
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
        // Get references from registry with scope support. ModelOverride bypasses the
        // registry lookup so workflows can route a sampler to a side model branch.
        var modelRef = p.ModelOverride ?? registry.GetRef($"{scope}model_output");
        var positiveRef = registry.GetRef($"{scope}positive_output");
        var negativeRef = registry.GetRef($"{scope}negative_output");
        var latentRef = registry.GetRef($"{scope}latent_output");

        builder.AddNode(p.SamplerId, node =>
        {
            node.Type(p.ClassType)
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
                .InputRef("latent_image", latentRef);

            if (p.OptionsRef.HasValue)
            {
                node.InputRef("options", p.OptionsRef.Value);
            }
        });

        if (p.RegisterLatentOutput)
        {
            // Overwrite latent_output with sampler output
            registry.Register("latent_output", p.SamplerId, 0);
        }
    }
}
