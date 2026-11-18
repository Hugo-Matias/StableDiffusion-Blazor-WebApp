using BlazorWebApp.Services;
using Moq;

namespace BlazorWebApp.Tests.MockBuilders
{
    /// <summary>
    /// Fluent builder for creating IBackendService mocks with pre-configured behaviors
    /// </summary>
    public class MockBackendServiceBuilder
    {
        private readonly Mock<IBackendService> _mock;
        private bool _isBackendAvailable = true;

        public MockBackendServiceBuilder()
        {
            _mock = new Mock<IBackendService>();
        }

        /// <summary>
        /// Sets whether the backend is available
        /// </summary>
        public MockBackendServiceBuilder WithBackendAvailable(bool isAvailable)
        {
            _isBackendAvailable = isAvailable;
            return this;
        }

        /// <summary>
        /// Builds the mock with all configured behaviors
        /// </summary>
        public Mock<IBackendService> Build()
        {
            // Setup IsBackendAvailable property
            _mock.Setup(x => x.IsBackendAvailable).Returns(_isBackendAvailable);

            return _mock;
        }

        /// <summary>
        /// Builds and returns the mock object directly
        /// </summary>
        public IBackendService BuildObject() => Build().Object;
    }
}
