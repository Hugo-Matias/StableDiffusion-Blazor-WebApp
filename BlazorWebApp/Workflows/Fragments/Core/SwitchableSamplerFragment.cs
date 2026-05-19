using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Sampler fragment that can emit either ComfyUI's KSamplerAdvanced node or the
/// RES4LYF ClownsharK sampler while preserving the standard latent_output contract.
/// </summary>
public class SwitchableSamplerFragment : IFragmentBuilder
{
    public const string KSamplerAdvancedEngine = "KSamplerAdvanced";
    public const string Res4LyfEngine = "RES4LYF";

    public string Id { get; init; } = "main_sampler";
    public string Title { get; init; } = "Sampler";
    public FragmentType Type { get; init; } = FragmentType.Sampler;
    public int Order { get; init; } = 50;
    public bool Collapsible { get; init; }
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
                Name = "sampler_engine",
                Label = "Sampler Engine",
                Type = ParameterType.Select,
                DefaultValue = Defaults.SamplerEngine,
                Options = [KSamplerAdvancedEngine, Res4LyfEngine]
            },
            new FragmentParameter
            {
                Name = "ksampler_sampler_name",
                Label = "Sampler",
                Type = ParameterType.Select,
                DefaultValue = Defaults.KSamplerSamplerName,
                Source = new DynamicSource("KSamplerAdvanced", "sampler_name")
            },
            new FragmentParameter
            {
                Name = "ksampler_scheduler",
                Label = "Scheduler",
                Type = ParameterType.Select,
                DefaultValue = Defaults.KSamplerScheduler,
                Source = new DynamicSource("KSamplerAdvanced", "scheduler")
            },
            new FragmentParameter
            {
                Name = "res4lyf_sampler_name",
                Label = "Sampler",
                Type = ParameterType.Select,
                DefaultValue = Defaults.Res4LyfSamplerName,
                Source = new DynamicSource("ClownsharKSampler_Beta", "sampler_name")
            },
            new FragmentParameter
            {
                Name = "res4lyf_scheduler",
                Label = "Scheduler",
                Type = ParameterType.Select,
                DefaultValue = Defaults.Res4LyfScheduler,
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
                Name = "eta",
                Label = "Eta",
                Type = ParameterType.Slider,
                Min = -100,
                Max = 100,
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
            },
            new FragmentParameter
            {
                Name = "add_noise",
                Label = "Add Noise",
                Type = ParameterType.Select,
                DefaultValue = Defaults.AddNoise,
                Options = ["enable", "disable"]
            },
            new FragmentParameter
            {
                Name = "start_at_step",
                Label = "Start At Step",
                Type = ParameterType.Number,
                Min = 0,
                Max = 10000,
                DefaultValue = Defaults.StartAtStep
            },
            new FragmentParameter
            {
                Name = "end_at_step",
                Label = "End At Step",
                Type = ParameterType.Number,
                Min = 0,
                Max = 10000,
                DefaultValue = Defaults.EndAtStep
            },
            new FragmentParameter
            {
                Name = "return_with_leftover_noise",
                Label = "Return With Leftover Noise",
                Type = ParameterType.Select,
                DefaultValue = Defaults.ReturnWithLeftoverNoise,
                Options = ["disable", "enable"]
            },
            new FragmentParameter
            {
                Name = "steps_to_run",
                Label = "Steps To Run",
                Type = ParameterType.Number,
                Min = -1,
                Max = 10000,
                DefaultValue = Defaults.StepsToRun
            },
            new FragmentParameter
            {
                Name = "sampler_mode",
                Label = "Sampler Mode",
                Type = ParameterType.Select,
                DefaultValue = Defaults.SamplerMode,
                Options = ["unsample", "standard", "resample"]
            }
        ]
    };

    public class Parameters
    {
        public string SamplerId { get; set; } = "sampler_main";
        public string Title { get; set; } = "Sampler";
        public string SamplerEngine { get; set; } = KSamplerAdvancedEngine;
        public string KSamplerSamplerName { get; set; } = "euler";
        public string KSamplerScheduler { get; set; } = "normal";
        public string Res4LyfSamplerName { get; set; } = "multistep/res_2m";
        public string Res4LyfScheduler { get; set; } = "beta";
        public int Steps { get; set; } = 20;
        public double Cfg { get; set; } = 7.0;
        public double Denoise { get; set; } = 1.0;
        public double Eta { get; set; } = 0.5;
        public long Seed { get; set; } = -1;
        public string AddNoise { get; set; } = "enable";
        public int StartAtStep { get; set; }
        public int EndAtStep { get; set; } = 10000;
        public string ReturnWithLeftoverNoise { get; set; } = "disable";
        public int StepsToRun { get; set; } = -1;
        public string SamplerMode { get; set; } = "standard";
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

        Build(builder, registry, new Parameters
        {
            SamplerId = Id == "main_sampler" ? "sampler_main" : Id,
            Title = Title,
            SamplerEngine = fragment?.GetString("sampler_engine", Defaults.SamplerEngine) ?? Defaults.SamplerEngine,
            KSamplerSamplerName = GetString(fragment, "ksampler_sampler_name", "sampler_name", Defaults.KSamplerSamplerName),
            KSamplerScheduler = GetString(fragment, "ksampler_scheduler", "scheduler", Defaults.KSamplerScheduler),
            Res4LyfSamplerName = GetString(fragment, "res4lyf_sampler_name", "sampler_name", Defaults.Res4LyfSamplerName),
            Res4LyfScheduler = GetString(fragment, "res4lyf_scheduler", "scheduler", Defaults.Res4LyfScheduler),
            Steps = fragment?.GetInt("steps", Defaults.Steps) ?? Defaults.Steps,
            Cfg = fragment?.GetDouble("cfg", Defaults.Cfg) ?? Defaults.Cfg,
            Denoise = fragment?.GetDouble("denoise", Defaults.Denoise) ?? Defaults.Denoise,
            Eta = fragment?.GetDouble("eta", Defaults.Eta) ?? Defaults.Eta,
            Seed = seed,
            AddNoise = fragment?.GetString("add_noise", Defaults.AddNoise) ?? Defaults.AddNoise,
            StartAtStep = fragment?.GetInt("start_at_step", Defaults.StartAtStep) ?? Defaults.StartAtStep,
            EndAtStep = fragment?.GetInt("end_at_step", Defaults.EndAtStep) ?? Defaults.EndAtStep,
            ReturnWithLeftoverNoise = fragment?.GetString("return_with_leftover_noise", Defaults.ReturnWithLeftoverNoise) ?? Defaults.ReturnWithLeftoverNoise,
            StepsToRun = fragment?.GetInt("steps_to_run", Defaults.StepsToRun) ?? Defaults.StepsToRun,
            SamplerMode = fragment?.GetString("sampler_mode", Defaults.SamplerMode) ?? Defaults.SamplerMode
        }, scope);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "")
    {
        var seed = fragmentParams.Seed < 0
            ? Random.Shared.NextInt64(0, int.MaxValue)
            : fragmentParams.Seed;

        var modelRef = fragmentParams.ModelOverride ?? registry.GetRef($"{scope}model_output");
        var positiveRef = registry.GetRef($"{scope}positive_output");
        var negativeRef = registry.GetRef($"{scope}negative_output");
        var latentRef = registry.GetRef($"{scope}latent_output");

        if (IsRes4Lyf(fragmentParams.SamplerEngine))
        {
            builder.AddNode(fragmentParams.SamplerId, node => node
                .Type("ClownsharKSampler_Beta")
                .Title(fragmentParams.Title)
                .Input("eta", fragmentParams.Eta)
                .Input("sampler_name", fragmentParams.Res4LyfSamplerName)
                .Input("scheduler", fragmentParams.Res4LyfScheduler)
                .Input("steps", fragmentParams.Steps)
                .Input("steps_to_run", fragmentParams.StepsToRun)
                .Input("denoise", fragmentParams.Denoise)
                .Input("cfg", fragmentParams.Cfg)
                .Input("seed", seed)
                .Input("sampler_mode", fragmentParams.SamplerMode)
                .Input("bongmath", true)
                .InputRef("model", modelRef)
                .InputRef("positive", positiveRef)
                .InputRef("negative", negativeRef)
                .InputRef("latent_image", latentRef));
        }
        else
        {
            builder.AddNode(fragmentParams.SamplerId, node => node
                .Type("KSamplerAdvanced")
                .Title(fragmentParams.Title)
                .Input("add_noise", fragmentParams.AddNoise)
                .Input("noise_seed", seed)
                .Input("steps", fragmentParams.Steps)
                .Input("cfg", fragmentParams.Cfg)
                .Input("sampler_name", fragmentParams.KSamplerSamplerName)
                .Input("scheduler", fragmentParams.KSamplerScheduler)
                .Input("start_at_step", fragmentParams.StartAtStep)
                .Input("end_at_step", fragmentParams.EndAtStep)
                .Input("return_with_leftover_noise", fragmentParams.ReturnWithLeftoverNoise)
                .InputRef("model", modelRef)
                .InputRef("positive", positiveRef)
                .InputRef("negative", negativeRef)
                .InputRef("latent_image", latentRef));
        }

        if (fragmentParams.RegisterLatentOutput)
        {
            registry.Register("latent_output", fragmentParams.SamplerId, 0);
        }
    }

    public static bool IsRes4Lyf(string? samplerEngine)
        => string.Equals(samplerEngine, Res4LyfEngine, StringComparison.OrdinalIgnoreCase);

    private static string GetString(FragmentParameters? fragment, string primaryKey, string legacyKey, string defaultValue)
    {
        if (fragment is null)
        {
            return defaultValue;
        }

        var value = fragment.GetString(primaryKey);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        value = fragment.GetString(legacyKey);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }
}
