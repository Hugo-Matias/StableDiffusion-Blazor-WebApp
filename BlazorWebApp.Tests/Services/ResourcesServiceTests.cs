using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace BlazorWebApp.Tests.Services
{
    /// <summary>
    /// Tests for ResourcesService focusing on resource management operations.
    /// Note: Some tests are limited due to file system dependencies.
    /// </summary>
    public class ResourcesServiceTests
    {
        private readonly Mock<IStateService> _mockState;
        private readonly Mock<IIOService> _mockIO;
        private readonly Mock<IDatabaseService> _mockDb;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly ResourcesService _service;

        public ResourcesServiceTests()
        {
            _mockState = new Mock<IStateService>();
            _mockIO = new Mock<IIOService>();
            _mockDb = new Mock<IDatabaseService>();
            _mockConfig = new Mock<IConfiguration>();

            // Setup default configuration
            _mockConfig.Setup(x => x["ResourcesPath"]).Returns(Path.Combine(Path.GetTempPath(), "resources"));
            _mockConfig.Setup(x => x["ResourcePreviewsPath"]).Returns(Path.Combine(Path.GetTempPath(), "previews"));
            _mockConfig.Setup(x => x["OutputDir"]).Returns(Path.Combine(Path.GetTempPath(), "output"));

            // Setup default state
            _mockState.Setup(x => x.State).Returns(new AppState
            {
                Resources = new AppStateResources { Weight = 1.0f, LoadTriggerWords = true }
            });
            _mockState.Setup(x => x.ParametersTxt2Img).Returns(new Txt2ImgParameters
            {
                Prompt = "",
                NegativePrompt = "",
                Loras = new List<Lora>()
            });
            _mockState.Setup(x => x.ParametersImg2Img).Returns(new Img2ImgParameters
            {
                Prompt = "",
                NegativePrompt = "",
                Loras = new List<Lora>()
            });

            _service = new ResourcesService(_mockState.Object, _mockIO.Object, _mockDb.Object, _mockConfig.Object);
        }

        #region CreateLocalResourcesByType Tests

        [Fact]
        public async Task CreateLocalResourcesByType_NoResources_ReturnsEmptyList()
        {
            // Arrange
            _mockDb.Setup(x => x.GetResources(It.IsAny<int>())).ReturnsAsync(new List<Resource>());

            // Act
            var result = await _service.CreateLocalResourcesByType(1);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateLocalResourcesByType_WithVaeType_AddsNoneAndAutomatic()
        {
            // Arrange
            var resources = new List<Resource>
            {
                new Resource
                {
                    Id = 1,
                    Title = "Test VAE",
                    Filename = "test.safetensors",
                    Type = new ResourceType { Name = "VAE" },
                    IsEnabled = true,
                    CreatedDate = DateTime.Now
                }
            };
            _mockDb.Setup(x => x.GetResources(It.IsAny<int>())).ReturnsAsync(resources);
            _mockIO.Setup(x => x.GetResourceImagePath(It.IsAny<string>(), It.IsAny<string>())).Returns("/path/to/image.png");

            // Act
            var result = await _service.CreateLocalResourcesByType(1);

            // Assert
            result.Should().Contain(r => r.Title == "None");
            result.Should().Contain(r => r.Title == "Automatic");
        }

        [Fact]
        public async Task CreateLocalResourcesByType_WithLoraType_DoesNotAddNoneAndAutomatic()
        {
            // Arrange
            var resources = new List<Resource>
            {
                new Resource
                {
                    Id = 1,
                    Title = "Test LoRA",
                    Filename = "test.safetensors",
                    Type = new ResourceType { Name = "LORA" },
                    IsEnabled = true,
                    CreatedDate = DateTime.Now
                }
            };
            _mockDb.Setup(x => x.GetResources(It.IsAny<int>())).ReturnsAsync(resources);
            _mockIO.Setup(x => x.GetResourceImagePath(It.IsAny<string>(), It.IsAny<string>())).Returns("/path/to/image.png");

            // Act
            var result = await _service.CreateLocalResourcesByType(1);

            // Assert
            result.Should().NotContain(r => r.Title == "None");
            result.Should().NotContain(r => r.Title == "Automatic");
        }

        [Fact]
        public async Task CreateLocalResourcesByType_SortsByTitle()
        {
            // Arrange
            var resources = new List<Resource>
            {
                new Resource
                {
                    Id = 1,
                    Title = "Zebra LoRA",
                    Filename = "zebra.safetensors",
                    Type = new ResourceType { Name = "LORA" },
                    IsEnabled = true,
                    CreatedDate = DateTime.Now
                },
                new Resource
                {
                    Id = 2,
                    Title = "Alpha LoRA",
                    Filename = "alpha.safetensors",
                    Type = new ResourceType { Name = "LORA" },
                    IsEnabled = true,
                    CreatedDate = DateTime.Now
                }
            };
            _mockDb.Setup(x => x.GetResources(It.IsAny<int>())).ReturnsAsync(resources);
            _mockIO.Setup(x => x.GetResourceImagePath(It.IsAny<string>(), It.IsAny<string>())).Returns("/path/to/image.png");

            // Act
            var result = await _service.CreateLocalResourcesByType(1);

            // Assert
            result[0].Title.Should().Be("Alpha LoRA");
            result[1].Title.Should().Be("Zebra LoRA");
        }

        [Fact]
        public async Task CreateLocalResourcesByType_GroupsResourcesByTitle()
        {
            // Arrange
            var resources = new List<Resource>
            {
                new Resource
                {
                    Id = 1,
                    Title = "Shared Title",
                    Filename = "v1.safetensors",
                    Type = new ResourceType { Name = "LORA" },
                    IsEnabled = true,
                    CreatedDate = DateTime.Now.AddDays(-1)
                },
                new Resource
                {
                    Id = 2,
                    Title = "Shared Title",
                    Filename = "v2.safetensors",
                    Type = new ResourceType { Name = "LORA" },
                    IsEnabled = true,
                    CreatedDate = DateTime.Now
                }
            };
            _mockDb.Setup(x => x.GetResources(It.IsAny<int>())).ReturnsAsync(resources);
            _mockIO.Setup(x => x.GetResourceImagePath(It.IsAny<string>(), It.IsAny<string>())).Returns("/path/to/image.png");

            // Act
            var result = await _service.CreateLocalResourcesByType(1);

            // Assert
            result.Should().HaveCount(1);
            result[0].Files.Should().HaveCount(2);
        }

        #endregion

        #region ToggleResource Tests

        [Fact]
        public async Task ToggleResource_EnabledResource_MovesToStorageAndUpdatesDb()
        {
            // Arrange
            var resource = new LocalResource
            {
                Title = "Test LoRA",
                Type = new ResourceType { Name = "LORA" }
            };
            var file = new LocalResourceFile
            {
                Filename = "test.safetensors",
                IsEnabled = true,
                ResourceId = 1,
                File = new FileInfo(Path.Combine(Path.GetTempPath(), "test.safetensors"))
            };

            // Act
            await _service.ToggleResource(resource, file);

            // Assert
            _mockIO.Verify(x => x.MoveFile(It.IsAny<string>(), It.Is<string>(s => s.Contains("_storage"))), Times.Once);
            _mockDb.Verify(x => x.ToggleResourceState(1), Times.Once);
        }

        [Fact]
        public async Task ToggleResource_DisabledResource_MovesToEnabledPath()
        {
            // Arrange
            var resource = new LocalResource
            {
                Title = "Test LoRA",
                Type = new ResourceType { Name = "LORA" }
            };
            var file = new LocalResourceFile
            {
                Filename = "test.safetensors",
                IsEnabled = false,
                ResourceId = 1,
                File = new FileInfo(Path.Combine(Path.GetTempPath(), "_storage", "test.safetensors"))
            };

            // Act
            await _service.ToggleResource(resource, file);

            // Assert
            _mockIO.Verify(x => x.MoveFile(It.IsAny<string>(), It.Is<string>(s => !s.Contains("_storage"))), Times.Once);
            _mockDb.Verify(x => x.ToggleResourceState(1), Times.Once);
        }

        [Fact]
        public async Task ToggleResource_WithSubtype_IncludesSubtypeInPath()
        {
            // Arrange
            var resource = new LocalResource
            {
                Title = "Test LoRA",
                Type = new ResourceType { Name = "LORA" },
                SubType = new ResourceSubType { Name = "Character" }
            };
            var file = new LocalResourceFile
            {
                Filename = "test.safetensors",
                IsEnabled = true,
                ResourceId = 1,
                File = new FileInfo(Path.Combine(Path.GetTempPath(), "test.safetensors"))
            };

            // Act
            await _service.ToggleResource(resource, file);

            // Assert
            _mockIO.Verify(x => x.MoveFile(It.IsAny<string>(), It.Is<string>(s => s.Contains("Character"))), Times.Once);
        }

        #endregion

        #region DeleteResource Tests

        [Fact]
        public async Task DeleteResource_WithoutDeleteFiles_OnlyDeletesFromDb()
        {
            // Arrange
            var resource = new Resource
            {
                Id = 1,
                CivitaiModelId = 123,
                CivitaiModelVersionId = 456,
                Type = new ResourceType { Name = "LORA" }
            };

            // Act
            await _service.DeleteResource(resource, deleteFiles: false, "", "");

            // Assert
            _mockDb.Verify(x => x.DeleteResource(1), Times.Once);
            _mockDb.Verify(x => x.DeleteResourceImage(456), Times.Once);
            _mockIO.Verify(x => x.DeleteFile(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteResource_WithDeleteFiles_DeletesFilesAndDb()
        {
            // Arrange
            var resource = new Resource
            {
                Id = 1,
                CivitaiModelId = 123,
                CivitaiModelVersionId = 456,
                Type = new ResourceType { Name = "LORA" }
            };

            var testFiles = new List<FileInfo>
            {
                new FileInfo(Path.Combine(Path.GetTempPath(), "test.safetensors")),
                new FileInfo(Path.Combine(Path.GetTempPath(), "test.txt"))
            };
            _mockIO.Setup(x => x.GetFilesByName(It.IsAny<string>(), It.IsAny<string>())).Returns(testFiles);
            _mockIO.Setup(x => x.GetFolderByName(It.IsAny<string>(), It.IsAny<string>())).Returns((DirectoryInfo?)null);

            // Act
            await _service.DeleteResource(resource, deleteFiles: true, Path.GetTempPath(), "test");

            // Assert
            _mockDb.Verify(x => x.DeleteResource(1), Times.Once);
            _mockDb.Verify(x => x.DeleteResourceImage(456), Times.Once);
            _mockIO.Verify(x => x.DeleteFile(It.IsAny<string>()), Times.Exactly(2));
        }

        #endregion

        #region LoadPrompt Tests

        [Fact]
        public async Task LoadPrompt_TextualInversion_AddsKeywordToPrompt()
        {
            // Arrange
            var file = new LocalResourceFile
            {
                Filename = "embedding.safetensors",
                File = new FileInfo(Path.Combine(Path.GetTempPath(), "embedding.safetensors"))
            };
            var txt2imgParams = new Txt2ImgParameters
            {
                Prompt = "initial prompt",
                NegativePrompt = "",
                Loras = new List<Lora>()
            };
            _mockState.Setup(x => x.ParametersTxt2Img).Returns(txt2imgParams);

            // Act
            await _service.LoadPrompt(file, "TextualInversion", (ModeType.Txt2Img, true));

            // Assert
            txt2imgParams.Prompt.Should().Contain("embedding");
        }

        [Fact]
        public async Task LoadPrompt_TextualInversion_WithWeight_AddsWeightedKeyword()
        {
            // Arrange
            var state = new AppState
            {
                Resources = new AppStateResources { Weight = 0.8f, LoadTriggerWords = false }
            };
            _mockState.Setup(x => x.State).Returns(state);

            var file = new LocalResourceFile
            {
                Filename = "embedding.safetensors",
                File = new FileInfo(Path.Combine(Path.GetTempPath(), "embedding.safetensors"))
            };
            var txt2imgParams = new Txt2ImgParameters
            {
                Prompt = "",
                NegativePrompt = "",
                Loras = new List<Lora>()
            };
            _mockState.Setup(x => x.ParametersTxt2Img).Returns(txt2imgParams);

            // Act
            await _service.LoadPrompt(file, "TextualInversion", (ModeType.Txt2Img, true));

            // Assert - check for weighted syntax pattern (locale-independent)
            txt2imgParams.Prompt.Should().Contain("(embedding:");
            txt2imgParams.Prompt.Should().Contain("8)"); // Weight contains 8 regardless of decimal separator
        }

        [Fact]
        public async Task LoadPrompt_Lora_AddsToLorasList()
        {
            // Arrange
            var loraPath = Path.Combine(Path.GetTempPath(), "resources", "LORA", "test_lora.safetensors");
            var file = new LocalResourceFile
            {
                Filename = "test_lora.safetensors",
                File = new FileInfo(loraPath)
            };
            var txt2imgParams = new Txt2ImgParameters
            {
                Prompt = "",
                NegativePrompt = "",
                Loras = new List<Lora>()
            };
            _mockState.Setup(x => x.ParametersTxt2Img).Returns(txt2imgParams);

            // Act
            await _service.LoadPrompt(file, "LORA", (ModeType.Txt2Img, true));

            // Assert
            txt2imgParams.Loras.Should().HaveCount(1);
            txt2imgParams.Loras[0].Name.Should().Be("test_lora");
            txt2imgParams.Loras[0].IsEnabled.Should().BeTrue();
        }

        [Fact]
        public async Task LoadPrompt_Hypernetwork_AddsHypernetSyntax()
        {
            // Arrange
            var file = new LocalResourceFile
            {
                Filename = "hypernet.pt",
                File = new FileInfo(Path.Combine(Path.GetTempPath(), "hypernet.pt"))
            };
            var txt2imgParams = new Txt2ImgParameters
            {
                Prompt = "",
                NegativePrompt = "",
                Loras = new List<Lora>()
            };
            _mockState.Setup(x => x.ParametersTxt2Img).Returns(txt2imgParams);

            // Act
            await _service.LoadPrompt(file, "Hypernetwork", (ModeType.Txt2Img, true));

            // Assert
            txt2imgParams.Prompt.Should().Contain("<hypernet:hypernet:");
        }

        [Fact]
        public async Task LoadPrompt_NegativePromptTarget_AddsToNegativePrompt()
        {
            // Arrange
            var file = new LocalResourceFile
            {
                Filename = "embedding.safetensors",
                File = new FileInfo(Path.Combine(Path.GetTempPath(), "embedding.safetensors"))
            };
            var txt2imgParams = new Txt2ImgParameters
            {
                Prompt = "",
                NegativePrompt = "initial negative",
                Loras = new List<Lora>()
            };
            _mockState.Setup(x => x.ParametersTxt2Img).Returns(txt2imgParams);

            // Act
            await _service.LoadPrompt(file, "TextualInversion", (ModeType.Txt2Img, false)); // false = negative prompt

            // Assert
            txt2imgParams.NegativePrompt.Should().Contain("embedding");
        }

        [Fact]
        public async Task LoadPrompt_WithTriggerWords_AppendsTriggerWords()
        {
            // Arrange
            var file = new LocalResourceFile
            {
                Filename = "lora.safetensors",
                File = new FileInfo(Path.Combine(Path.GetTempPath(), "lora.safetensors")),
                TriggerWords = new List<string> { "word1", "word2" }
            };
            var txt2imgParams = new Txt2ImgParameters
            {
                Prompt = "base",
                NegativePrompt = "",
                Loras = new List<Lora>()
            };
            _mockState.Setup(x => x.ParametersTxt2Img).Returns(txt2imgParams);
            _mockState.Setup(x => x.State).Returns(new AppState
            {
                Resources = new AppStateResources { Weight = 1.0f, LoadTriggerWords = true }
            });

            // Act
            await _service.LoadPrompt(file, "LORA", (ModeType.Txt2Img, true));

            // Assert
            txt2imgParams.Prompt.Should().Contain("word1");
            txt2imgParams.Prompt.Should().Contain("word2");
        }

        [Fact]
        public async Task LoadPrompt_Img2ImgMode_AddsToImg2ImgParams()
        {
            // Arrange
            var file = new LocalResourceFile
            {
                Filename = "embedding.safetensors",
                File = new FileInfo(Path.Combine(Path.GetTempPath(), "embedding.safetensors"))
            };
            var img2imgParams = new Img2ImgParameters
            {
                Prompt = "",
                NegativePrompt = "",
                Loras = new List<Lora>()
            };
            _mockState.Setup(x => x.ParametersImg2Img).Returns(img2imgParams);

            // Act
            await _service.LoadPrompt(file, "TextualInversion", (ModeType.Img2Img, true));

            // Assert
            img2imgParams.Prompt.Should().Contain("embedding");
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_InitializesResourceTypeDirectories()
        {
            // Arrange & Act
            var mockState = new Mock<IStateService>();
            var mockIO = new Mock<IIOService>();
            var mockDb = new Mock<IDatabaseService>();
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(x => x["ResourcesPath"]).Returns("/test/path");

            var service = new ResourcesService(mockState.Object, mockIO.Object, mockDb.Object, mockConfig.Object);

            // Assert - service should be created without errors
            service.Should().NotBeNull();
        }

        #endregion
    }
}
