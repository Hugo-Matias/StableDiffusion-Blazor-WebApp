using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;

namespace BlazorWebApp.Tests.Services
{
    /// <summary>
    /// Unit tests for IOService file system operations.
    /// Uses temporary directories for isolated testing.
    /// </summary>
    public class IOServiceTests : IDisposable
    {
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly IOService _sut;
        private readonly string _testDirectory;

        public IOServiceTests()
        {
            _mockConfig = new Mock<IConfiguration>();
            
            // Setup test directory
            _testDirectory = Path.Combine(Path.GetTempPath(), "IOServiceTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);

            // Setup configuration paths
            _mockConfig.Setup(c => c["OutputDir"]).Returns(Path.Combine(_testDirectory, "output"));
            _mockConfig.Setup(c => c["ResourcePreviewsPath"]).Returns(Path.Combine(_testDirectory, "previews"));

            _sut = new IOService(_mockConfig.Object);
        }

        public void Dispose()
        {
            // Cleanup test directory
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        #region File Operations Tests

        [Fact]
        public async Task SaveFileToDisk_CreatesDirectories()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "subdir1", "subdir2", "testfile.txt");
            var fileData = System.Text.Encoding.UTF8.GetBytes("test content");

            // Act
            await _sut.SaveFileToDisk(filePath, fileData);

            // Assert
            File.Exists(filePath).Should().BeTrue();
            var content = await File.ReadAllTextAsync(filePath);
            content.Should().Be("test content");
        }

        [Fact]
        public void MoveFile_PreservesContent()
        {
            // Arrange
            var sourcePath = Path.Combine(_testDirectory, "source.txt");
            var destPath = Path.Combine(_testDirectory, "subdir", "dest.txt");
            File.WriteAllText(sourcePath, "content to move");

            // Act
            _sut.MoveFile(sourcePath, destPath);

            // Assert
            File.Exists(sourcePath).Should().BeFalse();
            File.Exists(destPath).Should().BeTrue();
            File.ReadAllText(destPath).Should().Be("content to move");
        }

        [Fact]
        public void DeleteFile_RemovesFile()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "todelete.txt");
            File.WriteAllText(filePath, "test");
            var fileInfo = new FileInfo(filePath);

            // Act
            _sut.DeleteFile(fileInfo);

            // Assert
            File.Exists(filePath).Should().BeFalse();
        }

        [Fact]
        public void GetFilesByName_FindsMatches()
        {
            // Arrange
            var testName = "myfile";
            File.WriteAllText(Path.Combine(_testDirectory, "myfile.txt"), "test");
            File.WriteAllText(Path.Combine(_testDirectory, "myfile.png"), "test");
            File.WriteAllText(Path.Combine(_testDirectory, "other.txt"), "test");

            // Act
            var result = _sut.GetFilesByName(_testDirectory, testName);

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(f => f.Extension == ".txt");
            result.Should().Contain(f => f.Extension == ".png");
        }

        [Fact]
        public void LoadText_ReadsContent()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "read.txt");
            File.WriteAllText(filePath, "content to read");

            // Act
            var result = _sut.LoadText(filePath);

            // Assert
            result.Should().Be("content to read");
        }

        [Fact]
        public void LoadText_ReturnsNull_WhenFileDoesNotExist()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "nonexistent.txt");

            // Act
            var result = _sut.LoadText(filePath);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void SaveText_CreatesFile()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "saved.txt");
            var content = "saved content";

            // Act
            _sut.SaveText(filePath, content);

            // Assert
            File.Exists(filePath).Should().BeTrue();
            File.ReadAllText(filePath).Should().Be(content);
        }

        [Fact]
        public void SaveText_WithOverwriteFalse_DoesNotOverwrite()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "existing.txt");
            File.WriteAllText(filePath, "original");

            // Act
            _sut.SaveText(filePath, "new content", overwrite: false);

            // Assert
            File.ReadAllText(filePath).Should().Be("original");
        }

        #endregion

        #region Directory Operations Tests

        [Fact]
        public void CreateDirectory_CreatesPath()
        {
            // Arrange
            var dirPath = Path.Combine(_testDirectory, "sub1", "sub2", "sub3");

            // Act
            var result = _sut.CreateDirectory(dirPath);

            // Assert
            result.Should().NotBeNull();
            result.Exists.Should().BeTrue();
            Directory.Exists(dirPath).Should().BeTrue();
        }

        [Fact]
        public void GetOrderedFiles_SortsCorrectly()
        {
            // Arrange
            File.WriteAllText(Path.Combine(_testDirectory, "file3.txt"), "3");
            File.WriteAllText(Path.Combine(_testDirectory, "file1.txt"), "1");
            File.WriteAllText(Path.Combine(_testDirectory, "file2.txt"), "2");

            // Act
            var result = _sut.GetOrderedFiles(_testDirectory);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result.Select(f => f.Name).Should().BeInAscendingOrder();
            result.First().Name.Should().Be("file1.txt");
        }

        [Fact]
        public void GetOrderedFiles_ReturnsNull_WhenDirectoryDoesNotExist()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testDirectory, "nonexistent");

            // Act
            var result = _sut.GetOrderedFiles(nonExistentPath);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetFilesRecursive_IncludesSubdirectories()
        {
            // Arrange
            var subDir = Path.Combine(_testDirectory, "subdir");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(_testDirectory, "root.txt"), "root");
            File.WriteAllText(Path.Combine(subDir, "sub.txt"), "sub");

            // Act
            var result = _sut.GetFilesRecursive(_testDirectory);

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(f => f.Name == "root.txt");
            result.Should().Contain(f => f.Name == "sub.txt");
        }

        [Fact]
        public void GetFilesRecursive_RespectsExtensionWhitelist()
        {
            // Arrange
            File.WriteAllText(Path.Combine(_testDirectory, "file.txt"), "text");
            File.WriteAllText(Path.Combine(_testDirectory, "file.png"), "image");
            File.WriteAllText(Path.Combine(_testDirectory, "file.jpg"), "image");

            // Act
            var result = _sut.GetFilesRecursive(_testDirectory, extensionsWhitelist: new List<string> { ".txt", ".png" });

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(f => f.Extension == ".txt");
            result.Should().Contain(f => f.Extension == ".png");
            result.Should().NotContain(f => f.Extension == ".jpg");
        }

        [Fact]
        public void GetFilesRecursive_RespectsExtensionBlacklist()
        {
            // Arrange
            File.WriteAllText(Path.Combine(_testDirectory, "file.txt"), "text");
            File.WriteAllText(Path.Combine(_testDirectory, "file.tmp"), "temp");
            File.WriteAllText(Path.Combine(_testDirectory, "file.bak"), "backup");

            // Act
            var result = _sut.GetFilesRecursive(_testDirectory, extensionsBlacklist: new List<string> { ".tmp", ".bak" });

            // Assert
            result.Should().HaveCount(1);
            result.Should().Contain(f => f.Extension == ".txt");
            result.Should().NotContain(f => f.Extension == ".tmp");
            result.Should().NotContain(f => f.Extension == ".bak");
        }

        [Fact]
        public void GetFolderByName_FindsMatch()
        {
            // Arrange
            var folderName = "testfolder";
            var fullPath = Path.Combine(_testDirectory, folderName);
            Directory.CreateDirectory(fullPath);

            // Act
            var result = _sut.GetFolderByName(_testDirectory, folderName);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().StartWith(folderName);
        }

        [Fact]
        public void DeleteFolder_RemovesDirectory()
        {
            // Arrange
            var folderPath = Path.Combine(_testDirectory, "todelete");
            Directory.CreateDirectory(folderPath);
            var dirInfo = new DirectoryInfo(folderPath);

            // Act
            _sut.DeleteFolder(dirInfo, isRecursive: false);

            // Assert
            Directory.Exists(folderPath).Should().BeFalse();
        }

        #endregion

        #region Base64 Conversion Tests

        [Fact]
        public void GetBase64FromFile_EncodesCorrectly()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "image.png");
            var imageData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
            File.WriteAllBytes(filePath, imageData);

            // Act
            var result = _sut.GetBase64FromFile(filePath);

            // Assert
            result.Should().NotBeNullOrEmpty();
            var decoded = Convert.FromBase64String(result);
            decoded.Should().BeEquivalentTo(imageData);
        }

        [Fact]
        public async Task GetBase64FromFileAsync_EncodesCorrectly()
        {
            // Arrange
            var filePath = Path.Combine(_testDirectory, "image_async.png");
            var imageData = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
            await File.WriteAllBytesAsync(filePath, imageData);

            // Act
            var result = await _sut.GetBase64FromFileAsync(filePath);

            // Assert
            result.Should().NotBeNullOrEmpty();
            var decoded = Convert.FromBase64String(result);
            decoded.Should().BeEquivalentTo(imageData);
        }

        #endregion

        #region Path Utilities Tests

        [Fact]
        public void GetImageStaticFile_TransformsPath()
        {
            // Arrange
            var outputDir = _mockConfig.Object["OutputDir"];
            var imagePath = Path.Combine(outputDir, "project1", "image.png");

            // Act
            var result = _sut.GetImageStaticFile(imagePath);

            // Assert
            result.Should().StartWith("/image");
            result.Should().Contain("project1");
            result.Should().EndWith("image.png");
        }

        [Fact]
        public void GetImageStaticFile_ReturnsOriginal_WhenAlreadyTransformed()
        {
            // Arrange
            var staticPath = "/image/project1/image.png";

            // Act
            var result = _sut.GetImageStaticFile(staticPath);

            // Assert
            result.Should().Be(staticPath);
        }

        [Fact]
        public void GetImageStaticFile_ReturnsEmpty_WhenPathCannotBeNormalized()
        {
            // Arrange
            var invalidPath = "bad:///:not-a-local-image";

            // Act
            var result = _sut.GetImageStaticFile(invalidPath);

            // Assert
            result.Should().BeEmpty();
        }

        #endregion
    }
}
