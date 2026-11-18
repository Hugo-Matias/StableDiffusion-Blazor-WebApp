using System.Net;
using BlazorWebApp.Data.Dtos.HuggingFace;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BlazorWebApp.Tests.Services;

public class HuggingFaceServiceTests
{
    // -------------------------------------------------------------------------
    // QuickSearch
    // -------------------------------------------------------------------------

    [Fact]
    public async Task QuickSearchAsync_WhenQueryIsEmpty_ReturnsEmptyDtoWithoutHttpCall()
    {
        var requestCount = 0;
        var service = CreateService(_ =>
        {
            requestCount++;
            return JsonResponse("{}");
        });

        var result = await service.QuickSearchAsync(string.Empty);

        requestCount.Should().Be(0);
        result.Should().NotBeNull();
        result!.Models.Should().BeNull();
    }

    [Fact]
    public async Task QuickSearchAsync_WhenHttpFails_ReturnsNull()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var result = await service.QuickSearchAsync("flux");

        result.Should().BeNull();
    }

    [Fact]
    public async Task QuickSearchAsync_WhenPayloadValid_ReturnsModels()
    {
        var service = CreateService(_ => JsonResponse("""
        {
          "models": [
            { "id": "black-forest-labs/FLUX.1-dev", "downloads": 12345, "likes": 500 }
          ],
          "datasets": [],
          "spaces": []
        }
        """));

        var result = await service.QuickSearchAsync("flux");

        result.Should().NotBeNull();
        result!.Models.Should().ContainSingle();
        result.Models![0].Id.Should().Be("black-forest-labs/FLUX.1-dev");
        result.Models![0].DisplayName.Should().Be("FLUX.1-dev");
        result.Models![0].ResolvedAuthor.Should().Be("black-forest-labs");
    }

    // -------------------------------------------------------------------------
    // GetModels
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetModelsAsync_WhenHttpFails_ReturnsEmptyList()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var (models, cursor) = await service.GetModelsAsync(new HuggingFaceModelsRequest { Query = "flux" });

        models.Should().BeEmpty();
        cursor.Should().BeNull();
    }

    [Fact]
    public async Task GetModelsAsync_BuiltsCorrectQueryParams()
    {
        string? capturedUrl = null;
        var service = CreateService(request =>
        {
            capturedUrl = request.RequestUri?.ToString();
            return JsonResponse("[]");
        });

        await service.GetModelsAsync(new HuggingFaceModelsRequest
        {
            Query = "flux",
            Author = "black-forest-labs",
            PipelineTag = "text-to-image",
            Sort = HuggingFaceSort.Likes,
            Limit = 10
        });

        capturedUrl.Should().NotBeNull();
        capturedUrl.Should().Contain("search=flux");
        capturedUrl.Should().Contain("author=black-forest-labs");
        capturedUrl.Should().Contain("pipeline_tag=text-to-image");
        capturedUrl.Should().Contain("sort=likes");
        capturedUrl.Should().Contain("limit=10");
    }

    [Fact]
    public async Task GetModelsAsync_WhenResponseIsEmptyArray_ReturnsEmptyList()
    {
        var service = CreateService(_ => JsonResponse("[]"));

        var (models, cursor) = await service.GetModelsAsync(new HuggingFaceModelsRequest());

        models.Should().BeEmpty();
        cursor.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // GetModel (detail)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetModelAsync_WhenAuthorOrRepoEmpty_ReturnsNull()
    {
        var service = CreateService(_ => JsonResponse("{}"));

        var result1 = await service.GetModelAsync(string.Empty, "FLUX.1-dev");
        var result2 = await service.GetModelAsync("black-forest-labs", string.Empty);

        result1.Should().BeNull();
        result2.Should().BeNull();
    }

    [Fact]
    public async Task GetModelAsync_WhenHttpFails_ReturnsNull()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await service.GetModelAsync("nonexistent", "nonexistent-model");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetModelAsync_WhenPayloadValid_MapsFields()
    {
        var service = CreateService(_ => JsonResponse("""
        {
          "id": "black-forest-labs/FLUX.1-dev",
          "author": "black-forest-labs",
          "sha": "abc123",
          "private": false,
          "pipeline_tag": "text-to-image",
          "tags": ["diffusers", "flux"],
          "downloads": 99,
          "likes": 42
        }
        """));

        var result = await service.GetModelAsync("black-forest-labs", "FLUX.1-dev");

        result.Should().NotBeNull();
        result!.Id.Should().Be("black-forest-labs/FLUX.1-dev");
        result.Author.Should().Be("black-forest-labs");
        result.PipelineTag.Should().Be("text-to-image");
        result.Tags.Should().Contain("diffusers");
        result.Downloads.Should().Be(99);
    }

    // -------------------------------------------------------------------------
    // GetTree
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetTreeAsync_WhenAuthorOrRepoEmpty_ReturnsEmptyList()
    {
        var service = CreateService(_ => JsonResponse("[]"));

        var result = await service.GetTreeAsync(string.Empty, "FLUX.1-dev");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTreeAsync_WhenHttpFails_ReturnsEmptyList()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var result = await service.GetTreeAsync("black-forest-labs", "FLUX.1-dev");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTreeAsync_WhenRecursiveTrue_AddsRecursiveParam()
    {
        string? capturedUrl = null;
        var service = CreateService(request =>
        {
            capturedUrl = request.RequestUri?.ToString();
            return JsonResponse("[]");
        });

        await service.GetTreeAsync("author", "repo", recursive: true);

        capturedUrl.Should().Contain("recursive=true");
    }

    [Fact]
    public async Task GetTreeAsync_WhenEntriesReturned_MapsFields()
    {
        var service = CreateService(_ => JsonResponse("""
        [
          {
            "type": "file",
            "oid": "sha256abc",
            "size": 4096000,
            "path": "model.safetensors",
            "lfs": { "oid": "lfsoid", "size": 4096000, "pointerSize": 135 }
          },
          {
            "type": "directory",
            "oid": "diroid",
            "size": 0,
            "path": "subfolder"
          }
        ]
        """));

        var result = await service.GetTreeAsync("author", "repo");

        result.Should().HaveCount(2);
        result[0].IsFile.Should().BeTrue();
        result[0].Filename.Should().Be("model.safetensors");
        result[0].Extension.Should().Be(".safetensors");
        result[0].Lfs.Should().NotBeNull();
        result[0].SizeMb.Should().BeApproximately(3.91, 0.01);
        result[1].IsDirectory.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // GetReadme
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetReadmeAsync_WhenNotFound_ReturnsNull()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await service.GetReadmeAsync("author", "repo");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetReadmeAsync_WhenFound_ReturnsText()
    {
        const string readmeContent = "# My Model\nA great diffusion model.";
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(readmeContent, System.Text.Encoding.UTF8, "text/plain")
        });

        var result = await service.GetReadmeAsync("author", "repo");

        result.Should().Be(readmeContent);
    }

    // -------------------------------------------------------------------------
    // GetResolveUrl
    // -------------------------------------------------------------------------

    [Fact]
    public void GetResolveUrl_ReturnsCorrectUrl()
    {
        var service = CreateService(_ => JsonResponse("{}"));

        var url = service.GetResolveUrl("black-forest-labs", "FLUX.1-dev", "flux1-dev.safetensors");

        url.Should().Be("https://huggingface.co/black-forest-labs/FLUX.1-dev/resolve/main/flux1-dev.safetensors");
    }

    [Fact]
    public void GetResolveUrl_WithCustomRevision_IncludesRevisionInUrl()
    {
        var service = CreateService(_ => JsonResponse("{}"));

        var url = service.GetResolveUrl("author", "repo", "subdir/file.bin", "v1.0");

        url.Should().Be("https://huggingface.co/author/repo/resolve/v1.0/subdir/file.bin");
    }

    [Fact]
    public void GetResolveUrl_WithLeadingSlashInPath_StripLeadingSlash()
    {
        var service = CreateService(_ => JsonResponse("{}"));

        var url = service.GetResolveUrl("author", "repo", "/some/file.safetensors");

        url.Should().Be("https://huggingface.co/author/repo/resolve/main/some/file.safetensors");
    }

    // -------------------------------------------------------------------------
    // Token header behavior
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetModelAsync_WhenTokenConfigured_SendsBearerHeader()
    {
        string? authHeader = null;
        var service = CreateService(request =>
        {
            authHeader = request.Headers.Authorization?.ToString();
            return JsonResponse("{}");
        }, apiToken: "my-secret-token");

        await service.GetModelAsync("author", "repo");

        authHeader.Should().Be("Bearer my-secret-token");
    }

    [Fact]
    public async Task GetModelAsync_WhenTokenNotConfigured_SendsNoAuthHeader()
    {
        string? authHeader = "not-set";
        var service = CreateService(request =>
        {
            authHeader = request.Headers.Authorization?.ToString();
            return JsonResponse("{}");
        }, apiToken: null);

        await service.GetModelAsync("author", "repo");

        authHeader.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Computed properties
    // -------------------------------------------------------------------------

    [Fact]
    public void HuggingFaceModelSummaryDto_IsGated_WhenGatedIsTrue()
    {
        var json = """{ "id": "author/repo", "gated": true }""";
        var dto = System.Text.Json.JsonSerializer.Deserialize<HuggingFaceModelSummaryDto>(json)!;
        dto.IsGated.Should().BeTrue();
    }

    [Fact]
    public void HuggingFaceModelSummaryDto_IsGated_WhenGatedIsStringAuto()
    {
        var json = """{ "id": "author/repo", "gated": "auto" }""";
        var dto = System.Text.Json.JsonSerializer.Deserialize<HuggingFaceModelSummaryDto>(json)!;
        dto.IsGated.Should().BeTrue();
    }

    [Fact]
    public void HuggingFaceModelSummaryDto_IsGated_WhenGatedIsFalse()
    {
        var json = """{ "id": "author/repo", "gated": false }""";
        var dto = System.Text.Json.JsonSerializer.Deserialize<HuggingFaceModelSummaryDto>(json)!;
        dto.IsGated.Should().BeFalse();
    }

    [Fact]
    public void HuggingFaceCardDataDto_BaseModelString_WhenArray_ReturnsFirstElement()
    {
        var json = """{ "base_model": ["black-forest-labs/FLUX.1-dev", "another-model"] }""";
        var dto = System.Text.Json.JsonSerializer.Deserialize<HuggingFaceCardDataDto>(json)!;
        dto.BaseModelString.Should().Be("black-forest-labs/FLUX.1-dev");
    }

    [Fact]
    public void HuggingFaceCardDataDto_BaseModelString_WhenString_ReturnsValue()
    {
        var json = """{ "base_model": "black-forest-labs/FLUX.1-dev" }""";
        var dto = System.Text.Json.JsonSerializer.Deserialize<HuggingFaceCardDataDto>(json)!;
        dto.BaseModelString.Should().Be("black-forest-labs/FLUX.1-dev");
    }

    // -------------------------------------------------------------------------
    // DownloadAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DownloadAsync_WhenModelIdEmpty_ReturnsFail()
    {
        var svc = CreateDownloadService(null);
        var request = new HuggingFaceDownloadRequest { ModelId = "", TypeName = "LORA", Files = [new() { RepoFilePath = "a.safetensors", IsResourceFile = true }] };

        var result = await svc.DownloadAsync(request);

        result.Status.Should().Be(HuggingFaceDownloadStatus.Error);
    }

    [Fact]
    public async Task DownloadAsync_WhenTypeNameEmpty_ReturnsFail()
    {
        var svc = CreateDownloadService(null);
        var request = new HuggingFaceDownloadRequest { ModelId = "author/repo", TypeName = "", Files = [new() { RepoFilePath = "a.safetensors", IsResourceFile = true }] };

        var result = await svc.DownloadAsync(request);

        result.Status.Should().Be(HuggingFaceDownloadStatus.Error);
    }

    [Fact]
    public async Task DownloadAsync_WhenNoFiles_ReturnsFail()
    {
        var svc = CreateDownloadService(null);
        var request = new HuggingFaceDownloadRequest { ModelId = "author/repo", TypeName = "LORA", Files = [] };

        var result = await svc.DownloadAsync(request);

        result.Status.Should().Be(HuggingFaceDownloadStatus.Error);
    }

    [Fact]
    public async Task DownloadAsync_WhenInvalidModelIdFormat_ReturnsFail()
    {
        var svc = CreateDownloadService(null);
        var request = new HuggingFaceDownloadRequest { ModelId = "no-slash", TypeName = "LORA", Files = [new() { RepoFilePath = "a.safetensors", IsResourceFile = true }] };

        var result = await svc.DownloadAsync(request);

        result.Status.Should().Be(HuggingFaceDownloadStatus.Error);
    }

    [Fact]
    public async Task DownloadAsync_WhenFlatCollision_ReturnsConflict()
    {
        var svc = CreateDownloadService(null);
        var request = new HuggingFaceDownloadRequest
        {
            ModelId = "author/repo",
            TypeName = "LORA",
            KeepFolderStructure = false,
            Files =
            [
                new() { RepoFilePath = "a/model.safetensors", IsResourceFile = true },
                new() { RepoFilePath = "b/model.safetensors", IsResourceFile = true },
            ]
        };

        var result = await svc.DownloadAsync(request);

        result.Status.Should().Be(HuggingFaceDownloadStatus.PathConflict);
        result.ConflictingFilenames.Should().Contain("model.safetensors");
    }

    [Fact]
    public async Task DownloadAsync_WhenFileAlreadyOnDisk_SkipsFile()
    {
        var dir = Directory.CreateTempSubdirectory("hf_test_").FullName;
        try
        {
            var resourcesPath = dir;
            // Pre-create the destination file
            var destDir = Path.Combine(resourcesPath, "LORA");
            Directory.CreateDirectory(destDir);
            var destFile = Path.Combine(destDir, "model.safetensors");
            await File.WriteAllTextAsync(destFile, "dummy");

            var db = new Mock<IDatabaseService>();
            db.Setup(d => d.CheckResourceExistsByFilename(It.IsAny<string>())).ReturnsAsync(false);

            var svc = CreateDownloadService(
                handler: null,
                resourcesPath: resourcesPath,
                db: db.Object);

            var request = new HuggingFaceDownloadRequest
            {
                ModelId = "author/repo",
                TypeName = "LORA",
                Files = [new() { RepoFilePath = "model.safetensors", IsResourceFile = true }]
            };

            var result = await svc.DownloadAsync(request);

            result.SkippedFiles.Should().Contain("model.safetensors");
            result.DownloadedFiles.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAsync_WhenFileAlreadyInDb_SkipsFile()
    {
        var dir = Directory.CreateTempSubdirectory("hf_test_").FullName;
        try
        {
            var db = new Mock<IDatabaseService>();
            db.Setup(d => d.CheckResourceExistsByFilename("model.safetensors")).ReturnsAsync(true);

            var svc = CreateDownloadService(
                handler: null,
                resourcesPath: dir,
                db: db.Object);

            var request = new HuggingFaceDownloadRequest
            {
                ModelId = "author/repo",
                TypeName = "LORA",
                Files = [new() { RepoFilePath = "model.safetensors", IsResourceFile = true }]
            };

            var result = await svc.DownloadAsync(request);

            result.SkippedFiles.Should().Contain("model.safetensors");
            result.DownloadedFiles.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAsync_WhenHttpReturnsError_AddsToFailedFiles()
    {
        var dir = Directory.CreateTempSubdirectory("hf_test_").FullName;
        try
        {
            var db = new Mock<IDatabaseService>();
            db.Setup(d => d.CheckResourceExistsByFilename(It.IsAny<string>())).ReturnsAsync(false);

            var svc = CreateDownloadService(
                handler: _ => new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden),
                resourcesPath: dir,
                db: db.Object);

            var request = new HuggingFaceDownloadRequest
            {
                ModelId = "author/repo",
                TypeName = "LORA",
                Files = [new() { RepoFilePath = "model.safetensors", IsResourceFile = true }]
            };

            var result = await svc.DownloadAsync(request);

            result.FailedFiles.Should().Contain("model.safetensors");
            result.Status.Should().Be(HuggingFaceDownloadStatus.Error);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAsync_WhenSuccessful_CreatesResourceAndPublishesEvents()
    {
        var dir = Directory.CreateTempSubdirectory("hf_test_").FullName;
        try
        {
            var db = new Mock<IDatabaseService>();
            db.Setup(d => d.CheckResourceExistsByFilename(It.IsAny<string>())).ReturnsAsync(false);
            db.Setup(d => d.CreateResource(It.IsAny<Resource>())).ReturnsAsync(true);

            var events = new Mock<IEventService>();
            var progress = new Mock<IProgressService>();

            var svc = CreateDownloadService(
                handler: _ => OctetResponse("fake-bytes"),
                resourcesPath: dir,
                db: db.Object,
                events: events.Object,
                progress: progress.Object);

            var request = new HuggingFaceDownloadRequest
            {
                ModelId = "author/repo",
                TypeName = "LORA",
                DownloadPreview = false,
                Files = [new() { RepoFilePath = "model.safetensors", IsResourceFile = true, SizeKb = 100 }]
            };

            var result = await svc.DownloadAsync(request);

            result.Status.Should().Be(HuggingFaceDownloadStatus.Success);
            result.DownloadedFiles.Should().Contain("model.safetensors");
            db.Verify(d => d.CreateResource(It.Is<Resource>(r =>
                r.HuggingFaceModelId == "author/repo" &&
                r.HuggingFaceFilePath == "model.safetensors")), Times.Once);
            events.Verify(e => e.Publish(It.IsAny<ResourcesChangedEventArgs>()), Times.Once);
            events.Verify(e => e.Publish(It.IsAny<DownloadCompletedEventArgs>()), Times.Once);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAsync_WhenKeepFolderStructure_PlacesFileInSubFolder()
    {
        var dir = Directory.CreateTempSubdirectory("hf_test_").FullName;
        try
        {
            var db = new Mock<IDatabaseService>();
            db.Setup(d => d.CheckResourceExistsByFilename(It.IsAny<string>())).ReturnsAsync(false);
            db.Setup(d => d.CreateResource(It.IsAny<Resource>())).ReturnsAsync(true);

            var svc = CreateDownloadService(
                handler: _ => OctetResponse("fake"),
                resourcesPath: dir,
                db: db.Object);

            var request = new HuggingFaceDownloadRequest
            {
                ModelId = "author/repo",
                TypeName = "LORA",
                KeepFolderStructure = true,
                DownloadPreview = false,
                Files = [new() { RepoFilePath = "subfolder/model.safetensors", IsResourceFile = true }]
            };

            var result = await svc.DownloadAsync(request);

            result.DownloadedFiles.Should().Contain("subfolder/model.safetensors");
            var expectedPath = Path.Combine(dir, "LORA", "subfolder", "model.safetensors");
            File.Exists(expectedPath).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // ExtractFirstMarkdownImageUrl (static / internal)
    // -------------------------------------------------------------------------

    [Fact]
    public void ExtractFirstMarkdownImageUrl_WhenStandardMarkdownImage_ReturnsUrl()
    {
        const string md = "# Title\n![Preview](https://cdn.example.com/preview.png)\nSome text.";

        var url = HuggingFaceService.ExtractFirstMarkdownImageUrl(md, "author", "repo", "main");

        url.Should().Be("https://cdn.example.com/preview.png");
    }

    [Fact]
    public void ExtractFirstMarkdownImageUrl_WhenHtmlImgTag_ReturnsUrl()
    {
        const string md = """<img src="https://cdn.example.com/banner.jpg" />""";

        var url = HuggingFaceService.ExtractFirstMarkdownImageUrl(md, "author", "repo", "main");

        url.Should().Be("https://cdn.example.com/banner.jpg");
    }

    [Fact]
    public void ExtractFirstMarkdownImageUrl_WhenRelativePath_ReturnsResolvedUrl()
    {
        const string md = "![Preview](images/preview.png)";

        var url = HuggingFaceService.ExtractFirstMarkdownImageUrl(md, "author", "repo", "main");

        url.Should().StartWith("https://huggingface.co/author/repo/resolve/main/");
        url.Should().EndWith("images/preview.png");
    }

    [Fact]
    public void ExtractFirstMarkdownImageUrl_WhenFrontMatterPresent_SkipsFrontMatter()
    {
        const string md = """
        ---
        license: mit
        base_model: flux
        ---
        # Real content
        ![Preview](https://example.com/img.png)
        """;

        var url = HuggingFaceService.ExtractFirstMarkdownImageUrl(md, "author", "repo", "main");

        url.Should().Be("https://example.com/img.png");
    }

    [Fact]
    public void ExtractFirstMarkdownImageUrl_WhenNoImage_ReturnsNull()
    {
        const string md = "# Title\nNo images here.";

        var url = HuggingFaceService.ExtractFirstMarkdownImageUrl(md, "author", "repo", "main");

        url.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IHuggingFaceService CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> handler,
        string? apiToken = "test-token")
    {
        var settings = new Mock<ISettingsService>();
        var appSettings = new AppSettings();
        appSettings.Resources.HuggingFace.ApiToken = apiToken;
        settings.Setup(s => s.Settings).Returns(appSettings);

        return new HuggingFaceService(
            new HttpClient(new StubHttpMessageHandler(handler)),
            settings.Object,
            Mock.Of<IConfiguration>(),
            Mock.Of<IDatabaseService>(),
            Mock.Of<IEventService>(),
            Mock.Of<IProgressService>(),
            Mock.Of<IImageService>(),
            NullLogger<HuggingFaceService>.Instance);
    }

    private static HuggingFaceService CreateDownloadService(
        Func<HttpRequestMessage, HttpResponseMessage>? handler,
        string? resourcesPath = null,
        IDatabaseService? db = null,
        IEventService? events = null,
        IProgressService? progress = null,
        IImageService? img = null,
        string? apiToken = "test-token")
    {
        var settings = new Mock<ISettingsService>();
        var appSettings = new AppSettings();
        appSettings.Resources.HuggingFace.ApiToken = apiToken;
        settings.Setup(s => s.Settings).Returns(appSettings);

        var config = new Mock<IConfiguration>();
        config.Setup(c => c["ResourcesPath"]).Returns(resourcesPath ?? Path.GetTempPath());
        config.Setup(c => c["ResourcePreviewsPath"]).Returns((string?)null);

        var progressMock = progress ?? Mock.Of<IProgressService>();
        var dbMock = db ?? Mock.Of<IDatabaseService>();
        var eventsMock = events ?? Mock.Of<IEventService>();
        var imgMock = img ?? Mock.Of<IImageService>();

        Func<HttpRequestMessage, HttpResponseMessage> httpHandler = handler ?? (_ => JsonResponse("{}"));

        return new HuggingFaceService(
            new HttpClient(new StubHttpMessageHandler(httpHandler)),
            settings.Object,
            config.Object,
            dbMock,
            eventsMock,
            progressMock,
            imgMock,
            NullLogger<HuggingFaceService>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage OctetResponse(string body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(body))
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream") }
            }
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
