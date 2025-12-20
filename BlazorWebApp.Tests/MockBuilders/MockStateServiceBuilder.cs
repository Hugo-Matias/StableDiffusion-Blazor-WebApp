using BlazorWebApp.Models;
using BlazorWebApp.Services;
using Moq;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Tests.MockBuilders
{
    /// <summary>
    /// Fluent builder for creating IStateService mocks with pre-configured behaviors
    /// </summary>
    public class MockStateServiceBuilder
    {
        private readonly Mock<IStateService> _mock;
        private AppState? _state;
        private GenerationParameters? _generationParameters;

        public MockStateServiceBuilder()
        {
            _mock = new Mock<IStateService>();
            
            // Set up default state
            _state = new AppState
            {
                Generation = new AppStateGeneration
                {
                    Workflows = new List<Workflow>(),
                    CurrentWorkflowId = null,
                    WorkflowBase = ModelBase.StableDiffusion
                }
            };

            // Set up default GenerationParameters
            _generationParameters = new GenerationParameters();
            
            // Create default fragments
            var promptsFragment = new FragmentParameters
            {
                FragmentFile = FragmentKeys.Files.Prompts,
                IsActive = true
            };
            promptsFragment.SetValue(FragmentKeys.Params.Positive, "");
            promptsFragment.SetValue(FragmentKeys.Params.Negative, "");
            _generationParameters.Fragments[FragmentKeys.Fragments.Prompts] = promptsFragment;
            
            var samplerFragment = new FragmentParameters
            {
                FragmentFile = FragmentKeys.Files.Sampler,
                IsActive = true
            };
            samplerFragment.SetValue(FragmentKeys.Params.Seed, -1L);
            samplerFragment.SetValue(FragmentKeys.Params.Steps, 30);
            samplerFragment.SetValue(FragmentKeys.Params.Cfg, 7.5);
            samplerFragment.SetValue(FragmentKeys.Params.SamplerName, "euler");
            samplerFragment.SetValue(FragmentKeys.Params.Scheduler, "normal");
            samplerFragment.SetValue(FragmentKeys.Params.Denoise, 0.52);
            _generationParameters.Fragments[FragmentKeys.Fragments.MainSampler] = samplerFragment;
            
            var latentFragment = new FragmentParameters
            {
                FragmentFile = FragmentKeys.Files.EmptyLatent,
                IsActive = true
            };
            latentFragment.SetValue(FragmentKeys.Params.Width, 512);
            latentFragment.SetValue(FragmentKeys.Params.Height, 768);
            latentFragment.SetValue(FragmentKeys.Params.BatchSize, 4);
            _generationParameters.Fragments[FragmentKeys.Fragments.Latent] = latentFragment;
        }

        /// <summary>
        /// Sets the app state
        /// </summary>
        public MockStateServiceBuilder WithState(AppState state)
        {
            _state = state;
            return this;
        }

        /// <summary>
        /// Sets the workflows
        /// </summary>
        public MockStateServiceBuilder WithWorkflows(List<Workflow> workflows)
        {
            if (_state?.Generation != null)
                _state.Generation.Workflows = workflows;
            return this;
        }

        /// <summary>
        /// Sets the current workflow ID
        /// </summary>
        public MockStateServiceBuilder WithCurrentWorkflowId(Guid? workflowId)
        {
            if (_state?.Generation != null)
                _state.Generation.CurrentWorkflowId = workflowId;
            return this;
        }

        /// <summary>
        /// Sets the workflow base
        /// </summary>
        public MockStateServiceBuilder WithWorkflowBase(ModelBase workflowBase)
        {
            if (_state?.Generation != null)
                _state.Generation.WorkflowBase = workflowBase;
            return this;
        }

        /// <summary>
        /// Sets GenerationParameters
        /// </summary>
        public MockStateServiceBuilder WithGenerationParameters(GenerationParameters parameters)
        {
            _generationParameters = parameters;
            return this;
        }

        /// <summary>
        /// Sets workflow assets
        /// </summary>
        public MockStateServiceBuilder WithWorkflowAssets(Dictionary<string, string> assets)
        {
            foreach (var kvp in assets)
                _generationParameters.Assets[kvp.Key] = kvp.Value;
            return this;
        }

        /// <summary>
        /// Sets prompt values
        /// </summary>
        public MockStateServiceBuilder WithPrompts(string positive, string negative = "")
        {
            var promptsFragment = _generationParameters.GetOrCreateFragment(FragmentKeys.Fragments.Prompts, FragmentKeys.Files.Prompts);
            promptsFragment.SetValue(FragmentKeys.Params.Positive, positive);
            promptsFragment.SetValue(FragmentKeys.Params.Negative, negative);
            return this;
        }

        /// <summary>
        /// Sets sampler values
        /// </summary>
        public MockStateServiceBuilder WithSamplerSettings(int steps, double cfg, long seed = -1)
        {
            var samplerFragment = _generationParameters.GetOrCreateFragment(FragmentKeys.Fragments.MainSampler, FragmentKeys.Files.Sampler);
            samplerFragment.SetValue(FragmentKeys.Params.Steps, steps);
            samplerFragment.SetValue(FragmentKeys.Params.Cfg, cfg);
            samplerFragment.SetValue(FragmentKeys.Params.Seed, seed);
            return this;
        }

        /// <summary>
        /// Sets resolution values
        /// </summary>
        public MockStateServiceBuilder WithResolution(int width, int height)
        {
            var latentFragment = _generationParameters.GetOrCreateFragment(FragmentKeys.Fragments.Latent, FragmentKeys.Files.EmptyLatent);
            latentFragment.SetValue(FragmentKeys.Params.Width, width);
            latentFragment.SetValue(FragmentKeys.Params.Height, height);
            return this;
        }

        /// <summary>
        /// Adds LoRAs
        /// </summary>
        public MockStateServiceBuilder WithLoras(List<Lora> loras)
        {
            _generationParameters.Loras = loras;
            return this;
        }

        /// <summary>
        /// Builds the mock with all configured behaviors
        /// </summary>
        public Mock<IStateService> Build()
        {
            // Setup state property
            _mock.Setup(x => x.State).Returns(_state);

            // Setup GenerationParameters property
            _mock.Setup(x => x.GenerationParameters).Returns(_generationParameters);

            // Setup SaveState method
            _mock.Setup(x => x.SaveState()).Returns(Task.CompletedTask);

            return _mock;
        }

        /// <summary>
        /// Builds and returns the mock object directly
        /// </summary>
        public IStateService BuildObject() => Build().Object;
    }
}
