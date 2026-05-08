using System.Net;
using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BlazorWebApp.Tests.Services;

public class CivitaiServiceTests
{
    [Fact]
    public async Task GetImages_WhenHttpFails_ReturnsEmptyResponse()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var result = await service.GetImages(new CivitaiImagesRequest { Limit = 5, Page = 1 });

        result.Images.Should().BeEmpty();
        result.Metadata.Should().NotBeNull();
    }

    [Fact]
    public async Task GetImages_WhenPayloadHasNullItemsAndMetadata_ReturnsEmptyCollections()
    {
        var service = CreateService(_ => JsonResponse("""
        {
          "items": null,
          "metadata": null
        }
        """));

        var result = await service.GetImages(new CivitaiImagesRequest { Limit = 5, Page = 1 });

        result.Images.Should().BeEmpty();
        result.Metadata.Should().NotBeNull();
    }

    [Fact]
    public async Task GetImages_WhenDtoTypeIsVideo_SetsVideoSentinelWithoutRangeProbe()
    {
        var requestCount = 0;
        var service = CreateService(request =>
        {
            requestCount++;
            return JsonResponse("""
            {
              "items": [
                {
                  "id": 123,
                  "url": "https://media.example.test/asset-without-extension",
                  "type": "video",
                  "meta": null
                }
              ],
              "metadata": {}
            }
            """);
        });

        var result = await service.GetImages(new CivitaiImagesRequest { Limit = 5, Page = 1 });

        result.Images.Should().ContainSingle();
        result.Images[0].ImageType.Should().Equal(new byte[] { 0 });
        requestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetImageByModelVersionId_WhenCursorExhausts_ReturnsOriginalStub()
    {
        var service = CreateService(_ => JsonResponse("""
        {
          "items": [
            { "id": 999, "url": "https://media.example.test/other.jpg", "type": "image" }
          ],
          "metadata": { "nextCursor": null }
        }
        """));
        var stub = new CivitaiImageDto
        {
            Id = 42,
            Url = "https://media.example.test/stub.jpg",
            Type = "image"
        };

        var result = await service.GetImageByModelVersionId(77, stub);

        result.Should().BeSameAs(stub);
        result!.Url.Should().Be("https://media.example.test/stub.jpg");
    }

    [Fact]
    public async Task DownloadResource_WhenAutomaticImageDownloadsDisabled_DoesNotDownloadPreviewImage()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "civitai-service-tests", Guid.NewGuid().ToString("N"));
        var imageService = new Mock<IImageService>();
        var database = new Mock<IDatabaseService>();
        var progress = new Mock<IProgressService>();
        database.Setup(db => db.CheckResourceExistsByFilename(It.IsAny<string>())).ReturnsAsync(false);
        database.Setup(db => db.CreateResource(It.IsAny<Resource>())).ReturnsAsync(true);

        var service = CreateService(
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1, 2, 3 }) },
            imageService,
            database,
            progress,
            tempRoot);

        var model = new CivitaiModelDto
        {
            Id = 10,
            Name = "Model",
            Type = "Checkpoint",
            Creator = new CivitaiBaseModelCreatorDto { Username = "tester" },
            Tags = new List<string>()
        };
        var version = new CivitaiModelVersionDto
        {
            Id = 20,
            BaseModel = "SDXL",
            TrainedWords = new List<string>(),
            Images = new List<CivitaiImageDto>
            {
                new() { Id = 30, Url = "https://media.example.test/preview.jpg", Type = "image" }
            }
        };
        var file = new CivitaiModelVersionFileDto
        {
            Name = "model.safetensors",
            Type = "Model",
            Metadata = new CivitaiBaseModelVersionMetadataDto { Format = "SafeTensor" },
            VirusScanResult = "Success"
        };

        try
        {
            var result = await service.DownloadResource(model, version, file, downloadResourceImages: false);

            result.Should().Be(CivitaiDownloadStatus.Success);
            imageService.Verify(img => img.DownloadImageAsPng(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            File.Exists(Path.Combine(tempRoot, "resources", "Checkpoint", file.Name)).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static CivitaiService CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        Mock<IImageService>? imageService = null,
        Mock<IDatabaseService>? database = null,
        Mock<IProgressService>? progress = null,
        string? tempRoot = null)
    {
        tempRoot ??= Path.Combine(Path.GetTempPath(), "civitai-service-tests", Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CivitaiApiToken"] = "test-token",
                ["ResourcesPath"] = Path.Combine(tempRoot, "resources"),
                ["ResourcePreviewsPath"] = Path.Combine(tempRoot, "previews"),
                ["OutputDir"] = Path.Combine(tempRoot, "output")
            })
            .Build();

        var io = new Mock<IIOService>();
        io.Setup(service => service.LoadText(It.IsAny<string>())).Returns((string?)null);
        io.Setup(service => service.SaveText(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, string, bool>((path, _, _) => Directory.CreateDirectory(Path.GetDirectoryName(path)!));

        return new CivitaiService(
            new HttpClient(new StubHttpMessageHandler(handler)),
            configuration,
            imageService?.Object ?? Mock.Of<IImageService>(),
            io.Object,
            Mock.Of<IEventService>(),
            database?.Object ?? Mock.Of<IDatabaseService>(),
            progress?.Object ?? Mock.Of<IProgressService>(),
            NullLogger<CivitaiService>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}