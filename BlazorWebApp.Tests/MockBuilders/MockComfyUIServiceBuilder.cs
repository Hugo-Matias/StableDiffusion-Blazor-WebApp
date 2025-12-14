using BlazorWebApp.Models;
using BlazorWebApp.Services;
using Moq;

namespace BlazorWebApp.Tests.MockBuilders
{
    /// <summary>
    /// Fluent builder for creating IComfyUIService mocks with pre-configured behaviors
    /// </summary>
    public class MockComfyUIServiceBuilder
    {
        private readonly Mock<IComfyUIService> _mock;
        private bool _isAvailable = true;
        private List<Models.Sampler>? _samplers;
        private List<Scheduler>? _schedulers;
        private List<Upscaler>? _upscalers;

        public MockComfyUIServiceBuilder()
        {
            _mock = new Mock<IComfyUIService>();
        }

        /// <summary>
        /// Sets whether the ComfyUI backend is available
        /// </summary>
        public MockComfyUIServiceBuilder WithBackendAvailable(bool isAvailable)
        {
            _isAvailable = isAvailable;
            return this;
        }

        /// <summary>
        /// Sets the samplers that will be returned
        /// </summary>
        public MockComfyUIServiceBuilder WithSamplers(List<Models.Sampler> samplers)
        {
            _samplers = samplers;
            return this;
        }

        /// <summary>
        /// Sets the schedulers that will be returned
        /// </summary>
        public MockComfyUIServiceBuilder WithSchedulers(List<Scheduler> schedulers)
        {
            _schedulers = schedulers;
            return this;
        }

        /// <summary>
        /// Sets the upscalers that will be returned
        /// </summary>
        public MockComfyUIServiceBuilder WithUpscalers(List<Upscaler> upscalers)
        {
            _upscalers = upscalers;
            return this;
        }

        /// <summary>
        /// Builds the mock with all configured behaviors
        /// </summary>
        public Mock<IComfyUIService> Build()
        {
            // Setup health check
            _mock.Setup(x => x.CheckComfyUIState())
                .ReturnsAsync(_isAvailable);

            // Setup samplers
            _mock.Setup(x => x.GetSamplers())
                .ReturnsAsync(_samplers ?? new List<Models.Sampler>());

            // Setup schedulers
            _mock.Setup(x => x.GetSchedulers())
                .ReturnsAsync(_schedulers ?? new List<Scheduler>());

            // Setup upscalers
            _mock.Setup(x => x.GetUpscalers())
                .ReturnsAsync(_upscalers ?? new List<Upscaler>());

            return _mock;
        }

        /// <summary>
        /// Builds and returns the mock object directly
        /// </summary>
        public IComfyUIService BuildObject() => Build().Object;
    }
}
