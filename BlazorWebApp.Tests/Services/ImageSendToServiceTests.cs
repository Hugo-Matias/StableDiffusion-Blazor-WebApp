using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Moq;

namespace BlazorWebApp.Tests.Services;

/// <summary>
/// Unit tests for ImageSendToService, focusing on the IsLocal helper.
/// </summary>
public class ImageSendToServiceTests
{
    private readonly ImageSendToService _service;

    public ImageSendToServiceTests()
    {
        _service = new ImageSendToService(
            Mock.Of<IBackendService>(),
            Mock.Of<IStateService>(),
            Mock.Of<ISessionService>(),
            Mock.Of<IIOService>(),
            Mock.Of<IOrchestratorService>(),
            Mock.Of<NavigationManager>(),
            Mock.Of<MudBlazor.ISnackbar>());
    }

    [Theory]
    [InlineData("/image/foo.png", true)]
    [InlineData("/files/danbooru/general/score_0/123.jpg", true)]
    [InlineData("https://cdn.donmai.us/sample.jpg", false)]
    [InlineData("http://example.com/image.png", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("  ", false)]
    [InlineData("C:\\local\\image.png", true)]
    public void IsLocal_ReturnsExpectedValue(string? path, bool expected)
    {
        _service.IsLocal(path).Should().Be(expected);
    }
}
