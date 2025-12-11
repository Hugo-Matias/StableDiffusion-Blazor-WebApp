using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Text.Json;

namespace BlazorWebApp.Tests.Services
{
    /// <summary>
    /// Unit tests for SettingsService
    /// </summary>
    public class SettingsServiceTests : IDisposable
    {
        private readonly IOService _io;
        private readonly string _tempFile;
        private readonly SettingsService _sut;

        public SettingsServiceTests()
        {
            // Use a temp file for testing
            _tempFile = Path.GetTempFileName();

            // Create actual IOService with mock configuration
            var mockConfig = new Mock<IConfiguration>();
            _io = new IOService(mockConfig.Object);

            // Delete temp file so SettingsService creates defaults
            if (File.Exists(_tempFile))
                File.Delete(_tempFile);

            _sut = new SettingsService(_io);
        }

        public void Dispose()
        {
            // Cleanup temp file
            if (File.Exists(_tempFile))
                File.Delete(_tempFile);

            // Cleanup default settings file
            if (File.Exists("BlazorDiffusion.json"))
                File.Delete("BlazorDiffusion.json");
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ShouldInitializeWithDefaults_WhenNoSettingsFileExists()
        {
            // Assert
            _sut.Settings.Should().NotBeNull();
            _sut.Settings.Should().BeOfType<AppSettings>();
        }

        [Fact]
        public void Constructor_ShouldCreateDefaultSettingsFile()
        {
            // Assert
            File.Exists("BlazorDiffusion.json").Should().BeTrue();
        }

        #endregion

        #region LoadSettings Tests

        [Fact]
        public void LoadSettings_ShouldDeserializeJson_WhenFileExists()
        {
            // Arrange
            var testSettings = new AppSettings { IsDarkMode = true };
            var json = JsonSerializer.Serialize(testSettings);
            File.WriteAllText("BlazorDiffusion.json", json);

            // Act
            _sut.LoadSettings();

            // Assert
            _sut.Settings.IsDarkMode.Should().BeTrue();
        }

        [Fact]
        public void LoadSettings_ShouldUseDefaults_WhenJsonIsInvalid()
        {
            // Arrange
            File.WriteAllText("BlazorDiffusion.json", "invalid json {{{");

            // Act
            _sut.LoadSettings();

            // Assert
            _sut.Settings.Should().NotBeNull();
            _sut.Settings.Should().BeOfType<AppSettings>();
        }

        [Fact]
        public void LoadSettings_CanBeCalledMultipleTimes()
        {
            // Arrange
            var testSettings1 = new AppSettings { IsDarkMode = true };
            File.WriteAllText("BlazorDiffusion.json", JsonSerializer.Serialize(testSettings1));

            _sut.LoadSettings();
            _sut.Settings.IsDarkMode.Should().BeTrue();

            var testSettings2 = new AppSettings { IsDarkMode = false };
            File.WriteAllText("BlazorDiffusion.json", JsonSerializer.Serialize(testSettings2));

            // Act
            _sut.LoadSettings();

            // Assert
            _sut.Settings.IsDarkMode.Should().BeFalse();
        }

        #endregion

        #region SaveSettings Tests

        [Fact]
        public void SaveSettings_ShouldSerializeToJson()
        {
            // Arrange
            _sut.Settings.IsDarkMode = true;

            // Act
            _sut.SaveSettings();

            // Assert
            var json = File.ReadAllText("BlazorDiffusion.json");
            json.Should().NotBeNullOrEmpty();
            json.Should().Contain("\"IsDarkMode\": true");
        }

        [Fact]
        public void SaveSettings_ShouldUseIndentedJson()
        {
            // Act
            _sut.SaveSettings();

            // Assert
            var json = File.ReadAllText("BlazorDiffusion.json");
            json.Should().Contain("\n"); // Indented JSON contains newlines
        }

        [Fact]
        public void SaveSettings_ShouldPersistChanges()
        {
            // Arrange
            _sut.Settings.IsDarkMode = true;
            _sut.Settings.Generation.Shared.Steps.Value = 50;

            // Act
            _sut.SaveSettings();

            // Assert
            var json = File.ReadAllText("BlazorDiffusion.json");
            var deserialized = JsonSerializer.Deserialize<AppSettings>(json);
            deserialized!.IsDarkMode.Should().BeTrue();
            deserialized.Generation.Shared.Steps.Value.Should().Be(50);
        }

        #endregion

        #region Settings Property Tests

        [Fact]
        public void Settings_ShouldBeAccessible()
        {
            // Assert
            _sut.Settings.Should().NotBeNull();
            _sut.Settings.Should().BeOfType<AppSettings>();
        }

        [Fact]
        public void Settings_ShouldBeMutable()
        {
            // Arrange
            var originalValue = _sut.Settings.IsDarkMode;

            // Act
            _sut.Settings.IsDarkMode = !originalValue;

            // Assert
            _sut.Settings.IsDarkMode.Should().NotBe(originalValue);
        }

        [Fact]
        public void Settings_ShouldHaveGenerationDefaults()
        {
            // Assert
            _sut.Settings.Generation.Should().NotBeNull();
            _sut.Settings.Generation.Shared.Should().NotBeNull();
            _sut.Settings.Generation.Shared.Steps.Should().NotBeNull();
        }

        [Fact]
        public void Settings_ShouldHaveScriptDefaults()
        {
            // Assert
            _sut.Settings.Scripts.Should().NotBeNull();
            _sut.Settings.Scripts.ControlNet.Should().NotBeNull();
            _sut.Settings.Scripts.ADetailer.Should().NotBeNull();
        }

        #endregion

        #region Real-World Usage Tests

        [Fact]
        public void Settings_ShouldProvideControlNetDefaults()
        {
            // Assert - Verify ControlNet settings can be used for script initialization
            _sut.Settings.Scripts.ControlNet.Preprocessor.Should().Be(Data.Dtos.WebUI.ControlNetPreprocessor.none);
            _sut.Settings.Scripts.ControlNet.Model.Should().NotBeNullOrEmpty();
            _sut.Settings.Scripts.ControlNet.Weight.Value.Should().BeGreaterThan(0);
            _sut.Settings.Scripts.ControlNet.Guidance.Start.Should().BeGreaterThanOrEqualTo(0);
            _sut.Settings.Scripts.ControlNet.Guidance.End.Should().BeGreaterThan(0);
            _sut.Settings.Scripts.ControlNet.ResizeModes.Should().NotBeEmpty();
            _sut.Settings.Scripts.ControlNet.ControlModes.Should().NotBeEmpty();
        }

        [Fact]
        public void Settings_ShouldProvideDynamicPromptsDefaults()
        {
            // Assert - Verify DynamicPrompts settings match expectations
            _sut.Settings.Scripts.DynamicPrompts.IsEnabled.Should().BeFalse(); // Default disabled
            _sut.Settings.Scripts.DynamicPrompts.Combinatorial.Batches.Value.Should().BeGreaterThan(0);
            _sut.Settings.Scripts.DynamicPrompts.PromptMagic.Length.Value.Should().BeGreaterThan(0);
            _sut.Settings.Scripts.DynamicPrompts.PromptMagic.MagicModelList.Should().NotBeEmpty();
        }

        [Fact]
        public void Settings_ShouldProvideADetailerDefaults()
        {
            // Assert - Verify ADetailer settings are complete
            _sut.Settings.Scripts.ADetailer.Models.Should().NotBeEmpty();
            _sut.Settings.Scripts.ADetailer.Model.Should().NotBeNullOrEmpty();
            _sut.Settings.Scripts.ADetailer.Confidence.Value.Should().BeGreaterThan(0);
            _sut.Settings.Scripts.ADetailer.DenoisingStrength.Value.Should().BeGreaterThan(0);
            _sut.Settings.Scripts.ADetailer.MaskBlur.Value.Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public void Settings_ShouldProvideSharedGenerationDefaults()
        {
            // Assert - Verify shared generation settings are usable
            _sut.Settings.Generation.Shared.Sampler.Should().NotBeNullOrEmpty();
            _sut.Settings.Generation.Shared.Steps.Value.Should().BeGreaterThan(0);
            _sut.Settings.Generation.Shared.Resolution.Width.Should().BeGreaterThan(0);
            _sut.Settings.Generation.Shared.Resolution.Height.Should().BeGreaterThan(0);
            _sut.Settings.Generation.Shared.CfgScale.Value.Should().BeGreaterThan(0);
            _sut.Settings.Generation.Shared.QuickResolutions.Should().NotBeEmpty();
        }

        [Fact]
        public void Settings_ShouldProvideWildcardsDefaults()
        {
            // Assert - Components directly access M.Settings.Prompts.Wildcards
            _sut.Settings.Prompts.Wildcards.PromptTextfieldLines.Should().BeGreaterThan(0);
            _sut.Settings.Prompts.Wildcards.Generation.Value.Should().BeGreaterThan(0);
            _sut.Settings.Prompts.Wildcards.Generation.Min.Should().BeGreaterThan(0);
            _sut.Settings.Prompts.Wildcards.Generation.Max.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Settings_ControlNetPreprocessorSettings_ShouldHaveCompleteEntries()
        {
            // Assert - Verify that preprocessor settings dictionary is properly initialized
            _sut.Settings.Scripts.ControlNet.PreprocessorSettings.Should().NotBeEmpty();
            _sut.Settings.Scripts.ControlNet.PreprocessorSettings.Should().ContainKey(Data.Dtos.WebUI.ControlNetPreprocessor.none);
            _sut.Settings.Scripts.ControlNet.PreprocessorSettings.Should().ContainKey(Data.Dtos.WebUI.ControlNetPreprocessor.canny);
        }

        [Fact]
        public void Settings_Img2VidDefaults_ShouldBeValid()
        {
            // Assert - Verify Img2Vid settings that are accessed from components
            _sut.Settings.Generation.Img2Vid.Models.HighModel.Should().NotBeNullOrEmpty();
            _sut.Settings.Generation.Img2Vid.Models.LowModel.Should().NotBeNullOrEmpty();
            _sut.Settings.Generation.Img2Vid.Models.Clip.Should().NotBeNullOrEmpty();
            _sut.Settings.Generation.Img2Vid.Models.ClipVision.Should().NotBeNullOrEmpty();
            _sut.Settings.Generation.Img2Vid.Models.Vae.Should().NotBeNullOrEmpty();
            _sut.Settings.Generation.Img2Vid.Video.Length.Value.Should().BeGreaterThan(0);
            _sut.Settings.Generation.Img2Vid.Sampling.Sampler.Should().NotBeNullOrEmpty();
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void RoundTrip_SettingsShouldPersist()
        {
            // Arrange
            _sut.Settings.IsDarkMode = true;
            _sut.Settings.Generation.Shared.Steps.Value = 50;

            // Act - Save
            _sut.SaveSettings();

            // Create new service instance to load
            var mockConfig2 = new Mock<IConfiguration>();
            var io2 = new IOService(mockConfig2.Object);
            var service2 = new SettingsService(io2);

            // Assert - Verify loaded settings match saved settings
            service2.Settings.IsDarkMode.Should().BeTrue();
            service2.Settings.Generation.Shared.Steps.Value.Should().Be(50);
        }

        [Fact]
        public void RoundTrip_ScriptSettings_ShouldPersist()
        {
            // Arrange - Modify script settings
            _sut.Settings.Scripts.ControlNet.IsEnabled = true;
            _sut.Settings.Scripts.ControlNet.Weight.Value = 1.5f;
            _sut.Settings.Scripts.DynamicPrompts.IsEnabled = true;
            _sut.Settings.Scripts.DynamicPrompts.Combinatorial.IsEnabled = true;

            // Act
            _sut.SaveSettings();

            var mockConfig2 = new Mock<IConfiguration>();
            var io2 = new IOService(mockConfig2.Object);
            var service2 = new SettingsService(io2);

            // Assert
            service2.Settings.Scripts.ControlNet.IsEnabled.Should().BeTrue();
            service2.Settings.Scripts.ControlNet.Weight.Value.Should().Be(1.5f);
            service2.Settings.Scripts.DynamicPrompts.IsEnabled.Should().BeTrue();
            service2.Settings.Scripts.DynamicPrompts.Combinatorial.IsEnabled.Should().BeTrue();
        }

        #endregion
    }
}
