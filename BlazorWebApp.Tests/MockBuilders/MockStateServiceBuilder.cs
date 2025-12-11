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
        private Txt2ImgParameters? _txt2ImgParams;
        private Img2ImgParameters? _img2ImgParams;
        private Img2VidParameters? _img2VidParams;
        private UpscaleParameters? _upscaleParams;

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

            // Set up default parameters
            _txt2ImgParams = new Txt2ImgParameters
            {
                WorkflowAssets = new Dictionary<string, string>()
            };

            _img2ImgParams = new Img2ImgParameters
            {
                WorkflowAssets = new Dictionary<string, string>()
            };

            _img2VidParams = new Img2VidParameters
            {
                WorkflowAssets = new Dictionary<string, string>()
            };

            _upscaleParams = new UpscaleParameters
            {
                WorkflowAssets = new Dictionary<string, string>()
            };
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
        /// Sets Txt2Img parameters
        /// </summary>
        public MockStateServiceBuilder WithTxt2ImgParameters(Txt2ImgParameters parameters)
        {
            _txt2ImgParams = parameters;
            return this;
        }

        /// <summary>
        /// Sets Img2Img parameters
        /// </summary>
        public MockStateServiceBuilder WithImg2ImgParameters(Img2ImgParameters parameters)
        {
            _img2ImgParams = parameters;
            return this;
        }

        /// <summary>
        /// Sets Img2Vid parameters
        /// </summary>
        public MockStateServiceBuilder WithImg2VidParameters(Img2VidParameters parameters)
        {
            _img2VidParams = parameters;
            return this;
        }

        /// <summary>
        /// Sets Upscale parameters
        /// </summary>
        public MockStateServiceBuilder WithUpscaleParameters(UpscaleParameters parameters)
        {
            _upscaleParams = parameters;
            return this;
        }

        /// <summary>
        /// Sets workflow assets for Txt2Img
        /// </summary>
        public MockStateServiceBuilder WithTxt2ImgWorkflowAssets(Dictionary<string, string> assets)
        {
            _txt2ImgParams.WorkflowAssets = assets;
            return this;
        }

        /// <summary>
        /// Builds the mock with all configured behaviors
        /// </summary>
        public Mock<IStateService> Build()
        {
            // Setup state property
            _mock.Setup(x => x.State).Returns(_state);

            // Setup parameters properties
            _mock.Setup(x => x.ParametersTxt2Img).Returns(_txt2ImgParams);
            _mock.Setup(x => x.ParametersImg2Img).Returns(_img2ImgParams);
            _mock.Setup(x => x.ParametersImg2Vid).Returns(_img2VidParams);
            _mock.Setup(x => x.ParametersUpscale).Returns(_upscaleParams);

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
