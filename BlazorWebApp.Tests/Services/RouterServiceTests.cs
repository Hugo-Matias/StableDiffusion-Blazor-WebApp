using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using Microsoft.Extensions.Logging;
using Moq;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Tests.Services;

public class RouterServiceTests
{
    private readonly Mock<IComfyUIService> _mockComfyUI;
    private readonly Mock<IBackendService> _mockBackend;
    private readonly Mock<ILogger<RouterService>> _mockLogger;

    public RouterServiceTests()
    {
        _mockComfyUI = new Mock<IComfyUIService>();
        _mockBackend = new Mock<IBackendService>();
        _mockLogger = new Mock<ILogger<RouterService>>();

        // Default setup
        _mockBackend.Setup(b => b.IsBackendAvailable).Returns(true);
        _mockBackend.Setup(b => b.ComfyWSClientId).Returns("test-client-id");
    }

    private RouterService CreateService()
    {
        return new RouterService(
            _mockComfyUI.Object,
            _mockBackend.Object,
            _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldCreateService()
    {
        // Act
        var service = CreateService();

        // Assert
        Assert.NotNull(service);
    }

    #endregion

    #region SearchLoras Tests

    [Fact]
    public async Task SearchLoras_WithEmptySearch_ShouldReturnAllLoras()
    {
        // Arrange
        var expectedLoras = new List<string> { "lora1", "lora2", "lora3" };
        _mockComfyUI.Setup(c => c.GetLoras()).ReturnsAsync(expectedLoras);
        var service = CreateService();

        // Act
        var result = await service.SearchLoras(Backend.ComfyUI, "");

        // Assert
        Assert.Equal(expectedLoras, result);
        _mockComfyUI.Verify(c => c.GetLoras(), Times.Once);
        _mockComfyUI.Verify(c => c.SearchLoras(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SearchLoras_WithSearchQuery_ShouldCallSearchLoras()
    {
        // Arrange
        var searchQuery = "anime";
        var expectedLoras = new List<string> { "anime_lora1", "anime_lora2" };
        _mockComfyUI.Setup(c => c.SearchLoras(searchQuery)).ReturnsAsync(expectedLoras);
        var service = CreateService();

        // Act
        var result = await service.SearchLoras(Backend.ComfyUI, searchQuery);

        // Assert
        Assert.Equal(expectedLoras, result);
        _mockComfyUI.Verify(c => c.SearchLoras(searchQuery), Times.Once);
        _mockComfyUI.Verify(c => c.GetLoras(), Times.Never);
    }

    [Fact]
    public async Task SearchLoras_WhenNoLoras_ShouldReturnEmptyCollection()
    {
        // Arrange
        _mockComfyUI.Setup(c => c.GetLoras()).ReturnsAsync(new List<string>());
        var service = CreateService();

        // Act
        var result = await service.SearchLoras(Backend.ComfyUI, "");

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region PostGenerationAsync Tests

    [Fact]
    public async Task PostGenerationAsync_WhenBackendNotAvailable_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _mockBackend.Setup(b => b.IsBackendAvailable).Returns(false);
        var service = CreateService();
        var parameters = new GenerationParameters();
        var workflow = new Workflow { Id = Guid.NewGuid(), Mode = ModeType.Txt2Img };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PostGenerationAsync(parameters, workflow));
        Assert.Contains("not available", exception.Message);
    }

    [Fact]
    public async Task PostGenerationAsync_WhenWorkflowIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateService();
        var parameters = new GenerationParameters();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.PostGenerationAsync(parameters, null!));
    }

    [Fact]
    public async Task PostGenerationAsync_ShouldCallComfyUIService()
    {
        // Arrange
        var workflow = new Workflow { Id = Guid.NewGuid(), Title = "Test Workflow", Mode = ModeType.Txt2Img };
        var parameters = new GenerationParameters();
        var expectedResult = new GeneratedImages { Images = new List<string> { "base64data" } };
        
        _mockComfyUI.Setup(c => c.PostGenerationAsync(It.IsAny<GenerationParameters>(), It.IsAny<string>(), It.IsAny<Workflow>()))
            .ReturnsAsync(expectedResult);
        
        var service = CreateService();

        // Act
        var result = await service.PostGenerationAsync(parameters, workflow);

        // Assert
        Assert.Equal(expectedResult, result);
        _mockComfyUI.Verify(c => c.PostGenerationAsync(parameters, "test-client-id", workflow), Times.Once);
    }

    #endregion

    #region PostVideoGenerationAsync Tests

    [Fact]
    public async Task PostVideoGenerationAsync_WhenBackendNotAvailable_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _mockBackend.Setup(b => b.IsBackendAvailable).Returns(false);
        var service = CreateService();
        var parameters = new GenerationParameters();
        var workflow = new Workflow { Id = Guid.NewGuid(), Mode = ModeType.Img2Vid };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>
            (() => service.PostVideoGenerationAsync(parameters, workflow));
        Assert.Contains("not available", exception.Message);
    }

    [Fact]
    public async Task PostVideoGenerationAsync_WhenWorkflowIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = CreateService();
        var parameters = new GenerationParameters();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.PostVideoGenerationAsync(parameters, null!));
    }

    [Fact]
    public async Task PostVideoGenerationAsync_ShouldCallComfyUIService()
    {
        // Arrange
        var workflow = new Workflow { Id = Guid.NewGuid(), Title = "Video Workflow", Mode = ModeType.Img2Vid };
        var parameters = new GenerationParameters();
        var expectedResult = new GeneratedVideos { Videos = new List<GeneratedVideo>() };
        
        _mockComfyUI.Setup(c => c.PostVideoGenerationAsync(It.IsAny<GenerationParameters>(), It.IsAny<string>(), It.IsAny<Workflow>()))
            .ReturnsAsync(expectedResult);
        
        var service = CreateService();

        // Act
        var result = await service.PostVideoGenerationAsync(parameters, workflow);

        // Assert
        Assert.Equal(expectedResult, result);
        _mockComfyUI.Verify(c => c.PostVideoGenerationAsync(parameters, "test-client-id", workflow), Times.Once);
    }

    #endregion

    #region Backend Integration Tests

    [Fact]
    public void Service_ShouldRequireBackendServiceDependency()
    {
        // Arrange & Act
        var service = CreateService();

        // Assert - service should be created with backend dependency
        Assert.NotNull(service);
        _mockBackend.VerifyGet(b => b.IsBackendAvailable, Times.Never); // Not called until operation
    }

    [Fact]
    public void Service_ShouldUseClientIdFromBackendService()
    {
        // Arrange
        _mockBackend.Setup(b => b.ComfyWSClientId).Returns("unique-client-id");
        var service = CreateService();

        // Assert
        _mockBackend.VerifyGet(b => b.ComfyWSClientId, Times.Never); // Not called until operation
    }

    #endregion
}
