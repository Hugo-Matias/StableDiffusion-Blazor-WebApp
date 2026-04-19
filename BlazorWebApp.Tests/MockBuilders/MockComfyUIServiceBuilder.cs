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
        /// Builds the mock with all configured behaviors
        /// </summary>
        public Mock<IComfyUIService> Build()
        {
            // Setup health check
            _mock.Setup(x => x.CheckComfyUIState())
                .ReturnsAsync(_isAvailable);

            return _mock;
        }

        /// <summary>
        /// Builds and returns the mock object directly
        /// </summary>
        public IComfyUIService BuildObject() => Build().Object;
    }
}
