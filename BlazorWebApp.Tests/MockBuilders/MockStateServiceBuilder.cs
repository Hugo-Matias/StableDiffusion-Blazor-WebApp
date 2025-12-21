using BlazorWebApp.Models;
using BlazorWebApp.Models.Fragments;
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
            
            // Create default fragments using typed fragments
            var promptsFragment = new PromptsFragment
            {
                Id = FragmentKeys.Fragments.Prompts,
                IsActive = true,
                Positive = "",
                Negative = ""
            };
            _generationParameters.Fragments[FragmentKeys.Fragments.Prompts] = promptsFragment;
            
            var samplerFragment = new SamplerFragment
            {
                Id = FragmentKeys.Fragments.MainSampler,
                IsActive = true,
                Seed = -1,
                Steps = 30,
                Cfg = 7.5f,
                SamplerName = "euler",
                Scheduler = "normal",
                Denoise = 0.52f
            };
            _generationParameters.Fragments[FragmentKeys.Fragments.MainSampler] = samplerFragment;
            
            var latentFragment = new LatentFragment
            {
                Id = FragmentKeys.Fragments.Latent,
                IsActive = true,
                Width = 512,
                Height = 768,
                BatchSize = 4
            };
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
            var promptsFragment = GetOrCreatePromptsFragment();
            promptsFragment.Positive = positive;
            promptsFragment.Negative = negative;
            return this;
        }

        /// <summary>
        /// Sets sampler values
        /// </summary>
        public MockStateServiceBuilder WithSamplerSettings(int steps, double cfg, long seed = -1)
        {
            var samplerFragment = GetOrCreateSamplerFragment();
            samplerFragment.Steps = steps;
            samplerFragment.Cfg = (float)cfg;
            samplerFragment.Seed = seed;
            return this;
        }

        /// <summary>
        /// Sets resolution values
        /// </summary>
        public MockStateServiceBuilder WithResolution(int width, int height)
        {
            var latentFragment = GetOrCreateLatentFragment();
            latentFragment.Width = width;
            latentFragment.Height = height;
            return this;
        }

        /// <summary>
        /// Helper method to get or create a prompts fragment
        /// </summary>
        private PromptsFragment GetOrCreatePromptsFragment()
        {
            if (!_generationParameters!.Fragments.TryGetValue(FragmentKeys.Fragments.Prompts, out var fragment))
            {
                fragment = new PromptsFragment { Id = FragmentKeys.Fragments.Prompts, IsActive = true };
                _generationParameters.Fragments[FragmentKeys.Fragments.Prompts] = fragment;
            }
            return (PromptsFragment)fragment;
        }

        /// <summary>
        /// Helper method to get or create a sampler fragment
        /// </summary>
        private SamplerFragment GetOrCreateSamplerFragment()
        {
            if (!_generationParameters!.Fragments.TryGetValue(FragmentKeys.Fragments.MainSampler, out var fragment))
            {
                fragment = new SamplerFragment { Id = FragmentKeys.Fragments.MainSampler, IsActive = true };
                _generationParameters.Fragments[FragmentKeys.Fragments.MainSampler] = fragment;
            }
            return (SamplerFragment)fragment;
        }

        /// <summary>
        /// Helper method to get or create a latent fragment
        /// </summary>
        private LatentFragment GetOrCreateLatentFragment()
        {
            if (!_generationParameters!.Fragments.TryGetValue(FragmentKeys.Fragments.Latent, out var fragment))
            {
                fragment = new LatentFragment { Id = FragmentKeys.Fragments.Latent, IsActive = true };
                _generationParameters.Fragments[FragmentKeys.Fragments.Latent] = fragment;
            }
            return (LatentFragment)fragment;
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
