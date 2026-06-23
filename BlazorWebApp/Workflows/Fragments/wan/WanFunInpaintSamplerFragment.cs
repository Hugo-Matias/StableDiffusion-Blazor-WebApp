using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

public class WanFunInpaintSamplerFragment : IFragmentBuilder
{
    public const string LowSeedParameter = "low_seed";
    public const string SplitAtStepParameter = "split_at_step";
    public const string LockEndAtStepsParameter = "lock_end_at_steps";

    private readonly string _id;
    private readonly string _title;
    private readonly FragmentType _type;
    private readonly int _order;

    public WanFunInpaintSamplerFragment(
        string id,
        string title,
        FragmentType type = FragmentType.Sampler,
        int order = 40)
    {
        _id = id;
        _title = title;
        _type = type;
        _order = order;
    }

    public Parameters Defaults { get; init; } = new();

    public FragmentMetadata Metadata => new()
    {
        Id = _id,
        Type = _type,
        Title = _title,
        Component = "WanFunInpaintSamplerForm",
        Icon = "fa-solid fa-dice",
        Order = _order,
        Collapsible = false,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "sampler_name",
                Label = "Sampler",
                Type = ParameterType.Select,
                Source = new DynamicSource("KSamplerAdvanced", "sampler_name"),
                DefaultValue = Defaults.SamplerName
            },
            new FragmentParameter
            {
                Name = "scheduler",
                Label = "Scheduler",
                Type = ParameterType.Select,
                Source = new DynamicSource("KSamplerAdvanced", "scheduler"),
                DefaultValue = Defaults.Scheduler
            },
            new FragmentParameter
            {
                Name = "steps",
                Label = "Steps",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 100,
                Step = 1,
                DefaultValue = Defaults.Steps
            },
            new FragmentParameter
            {
                Name = "cfg",
                Label = "CFG Scale",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 20,
                Step = 0.1,
                DefaultValue = Defaults.Cfg
            },
            new FragmentParameter
            {
                Name = "seed",
                Label = "High Seed",
                Type = ParameterType.Number,
                Min = -1,
                DefaultValue = Defaults.Seed
            },
            new FragmentParameter
            {
                Name = LowSeedParameter,
                Label = "Low Seed",
                Type = ParameterType.Number,
                Min = -1,
                DefaultValue = Defaults.LowSeed
            },
            new FragmentParameter
            {
                Name = "start_at_step",
                Label = "Start Step",
                Type = ParameterType.Number,
                Min = 0,
                Max = 10000,
                Step = 1,
                DefaultValue = Defaults.StartAtStep
            },
            new FragmentParameter
            {
                Name = SplitAtStepParameter,
                Label = "Middle Step",
                Type = ParameterType.Number,
                Min = 0,
                Max = 10000,
                Step = 1,
                DefaultValue = Defaults.SplitAtStep
            },
            new FragmentParameter
            {
                Name = "end_at_step",
                Label = "End Step",
                Type = ParameterType.Number,
                Min = 0,
                Max = 10000,
                Step = 1,
                DefaultValue = Defaults.EndAtStep
            },
            new FragmentParameter
            {
                Name = LockEndAtStepsParameter,
                Label = "Lock End To Steps",
                Type = ParameterType.Checkbox,
                DefaultValue = Defaults.LockEndAtSteps
            }
        ]
    };

    public class Parameters
    {
        public string HighNodeId { get; set; } = "sampler_high";
        public string LowNodeId { get; set; } = "sampler_low";
        public string SamplerName { get; set; } = "euler";
        public string Scheduler { get; set; } = "simple";
        public int Steps { get; set; } = 4;
        public double Cfg { get; set; } = 1;
        public long Seed { get; set; } = 0;
        public long LowSeed { get; set; } = 0;
        public int StartAtStep { get; set; }
        public int SplitAtStep { get; set; } = 2;
        public int EndAtStep { get; set; } = 4;
        public bool LockEndAtSteps { get; set; } = true;
        public string HighModelInputName { get; set; } = "high_sampled_model_output";
        public string LowModelInputName { get; set; } = "low_sampled_model_output";
        public string PositiveInputName { get; set; } = "positive_output";
        public string NegativeInputName { get; set; } = "negative_output";
        public string LatentInputName { get; set; } = "latent_output";
        public string LatentOutputName { get; set; } = "latent_output";
        public string HighTitle { get; set; } = "KSampler High Noise";
        public string LowTitle { get; set; } = "KSampler Low Noise";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        var steps = fragment?.GetInt("steps", Defaults.Steps) ?? Defaults.Steps;
        var startAtStep = fragment?.GetInt("start_at_step", Defaults.StartAtStep) ?? Defaults.StartAtStep;
        var lockEndAtSteps = fragment?.GetBool(LockEndAtStepsParameter, Defaults.LockEndAtSteps) ?? Defaults.LockEndAtSteps;
        var endAtStep = lockEndAtSteps
            ? steps
            : fragment?.GetInt("end_at_step", Defaults.EndAtStep) ?? Defaults.EndAtStep;
        var splitAtStep = fragment?.GetInt(SplitAtStepParameter, Defaults.SplitAtStep) ?? Defaults.SplitAtStep;
        NormalizeStepRange(ref startAtStep, ref splitAtStep, ref endAtStep);

        Build(builder, registry, new Parameters
        {
            HighNodeId = Defaults.HighNodeId,
            LowNodeId = Defaults.LowNodeId,
            SamplerName = fragment?.GetString("sampler_name", Defaults.SamplerName) ?? Defaults.SamplerName,
            Scheduler = fragment?.GetString("scheduler", Defaults.Scheduler) ?? Defaults.Scheduler,
            Steps = steps,
            Cfg = fragment?.GetDouble("cfg", Defaults.Cfg) ?? Defaults.Cfg,
            Seed = ResolveRuntimeSeed(fragment?.GetLong("seed", Defaults.Seed) ?? Defaults.Seed),
            LowSeed = ResolveRuntimeSeed(fragment?.GetLong(LowSeedParameter, Defaults.LowSeed) ?? Defaults.LowSeed),
            StartAtStep = startAtStep,
            SplitAtStep = splitAtStep,
            EndAtStep = endAtStep,
            LockEndAtSteps = lockEndAtSteps,
            HighModelInputName = Defaults.HighModelInputName,
            LowModelInputName = Defaults.LowModelInputName,
            PositiveInputName = Defaults.PositiveInputName,
            NegativeInputName = Defaults.NegativeInputName,
            LatentInputName = Defaults.LatentInputName,
            LatentOutputName = Defaults.LatentOutputName,
            HighTitle = Defaults.HighTitle,
            LowTitle = Defaults.LowTitle
        }, scope, scopeTitle);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        var startAtStep = fragmentParams.StartAtStep;
        var splitAtStep = fragmentParams.SplitAtStep;
        var endAtStep = fragmentParams.EndAtStep;
        NormalizeStepRange(ref startAtStep, ref splitAtStep, ref endAtStep);

        AddSamplerNode(
            builder,
            registry,
            $"{scope}{fragmentParams.HighNodeId}",
            $"{scopeTitle}{fragmentParams.HighTitle}",
            seed: fragmentParams.Seed,
            addNoise: "enable",
            returnWithLeftoverNoise: "enable",
            samplerName: fragmentParams.SamplerName,
            scheduler: fragmentParams.Scheduler,
            steps: fragmentParams.Steps,
            cfg: fragmentParams.Cfg,
            startAtStep: startAtStep,
            endAtStep: splitAtStep,
            modelInputName: fragmentParams.HighModelInputName,
            positiveInputName: fragmentParams.PositiveInputName,
            negativeInputName: fragmentParams.NegativeInputName,
            latentInputName: fragmentParams.LatentInputName,
            latentOutputName: "high_latent_output");

        AddSamplerNode(
            builder,
            registry,
            $"{scope}{fragmentParams.LowNodeId}",
            $"{scopeTitle}{fragmentParams.LowTitle}",
            seed: fragmentParams.LowSeed,
            addNoise: "disable",
            returnWithLeftoverNoise: "disable",
            samplerName: fragmentParams.SamplerName,
            scheduler: fragmentParams.Scheduler,
            steps: fragmentParams.Steps,
            cfg: fragmentParams.Cfg,
            startAtStep: splitAtStep,
            endAtStep: endAtStep,
            modelInputName: fragmentParams.LowModelInputName,
            positiveInputName: fragmentParams.PositiveInputName,
            negativeInputName: fragmentParams.NegativeInputName,
            latentInputName: "high_latent_output",
            latentOutputName: fragmentParams.LatentOutputName);
    }

    private static void AddSamplerNode(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string nodeId,
        string title,
        long seed,
        string addNoise,
        string returnWithLeftoverNoise,
        string samplerName,
        string scheduler,
        int steps,
        double cfg,
        int startAtStep,
        int endAtStep,
        string modelInputName,
        string positiveInputName,
        string negativeInputName,
        string latentInputName,
        string latentOutputName)
    {
        builder.AddNode(nodeId, node => node
            .Type("KSamplerAdvanced")
            .Title(title)
            .Input("add_noise", addNoise)
            .Input("noise_seed", seed)
            .Input("steps", steps)
            .Input("cfg", cfg)
            .Input("sampler_name", samplerName)
            .Input("scheduler", scheduler)
            .Input("start_at_step", startAtStep)
            .Input("end_at_step", endAtStep)
            .Input("return_with_leftover_noise", returnWithLeftoverNoise)
            .InputRef("model", registry.GetRef(modelInputName))
            .InputRef("positive", registry.GetRef(positiveInputName))
            .InputRef("negative", registry.GetRef(negativeInputName))
            .InputRef("latent_image", registry.GetRef(latentInputName)));

        registry.Register(latentOutputName, nodeId, 0);
    }

    private static long ResolveRuntimeSeed(long seed)
    {
        return seed < 0 ? Random.Shared.NextInt64(0, int.MaxValue) : seed;
    }

    private static void NormalizeStepRange(ref int startAtStep, ref int splitAtStep, ref int endAtStep)
    {
        startAtStep = Math.Max(0, startAtStep);
        endAtStep = Math.Max(startAtStep, endAtStep);
        splitAtStep = Math.Clamp(splitAtStep, startAtStep, endAtStep);
    }
}
