using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace BlazorWebApp.Tests.Services
{
    /// <summary>
    /// Tests for GalleryService focusing on image selection and event publication.
    /// Note: Folder/Project loading tests are limited due to DatabaseService not having an interface yet.
    /// Full database integration tests will be added in Phase 9 when IDatabaseService is extracted.
    /// </summary>
    public class GalleryServiceTests
    {
        private readonly Mock<IDatabaseService> _mockDb;
        private readonly Mock<IStateService> _mockState;
        private readonly Mock<IEventService> _mockEvents;
        private readonly GalleryService _service;

        public GalleryServiceTests()
        {
            _mockDb = new Mock<IDatabaseService>();
            _mockState = new Mock<IStateService>();
            _mockEvents = new Mock<IEventService>();

            // Setup default state
            _mockState.Setup(x => x.State).Returns(new AppState
            {
                Gallery = new AppStateGallery()
            });
            _mockState.Setup(x => x.SaveState()).Returns(Task.CompletedTask);

            _service = new GalleryService(_mockDb.Object, _mockState.Object, _mockEvents.Object);
        }

        #region Image Selection Tests

        [Fact]
        public void AddSelectedImage_AddsImageIdAndPublishesEvent()
        {
            // Arrange
            ImageSelectionChangedEventArgs? capturedEvent = null;
            _mockEvents.Setup(x => x.Publish(It.IsAny<ImageSelectionChangedEventArgs>()))
                .Callback<ImageSelectionChangedEventArgs>(e => capturedEvent = e);

            // Act
            _service.AddSelectedImage(1);

            // Assert
            _service.SelectedImageIds.Should().Contain(1);
            _mockEvents.Verify(x => x.Publish(It.IsAny<ImageSelectionChangedEventArgs>()), Times.Once);
            capturedEvent.Should().NotBeNull();
            capturedEvent!.SelectedCount.Should().Be(1);
        }

        [Fact]
        public void AddSelectedImage_WhenAlreadySelected_DoesNotAddAgain()
        {
            // Arrange
            _service.AddSelectedImage(1);
            _mockEvents.Invocations.Clear(); // Clear previous event

            // Act
            _service.AddSelectedImage(1); // Try to add again

            // Assert
            _service.SelectedImageIds.Should().ContainSingle();
            _mockEvents.Verify(x => x.Publish(It.IsAny<ImageSelectionChangedEventArgs>()), Times.Never);
        }

        [Fact]
        public void AddSelectedImage_MultipleDifferentIds_AddsAll()
        {
            // Arrange & Act
            _service.AddSelectedImage(1);
            _service.AddSelectedImage(2);
            _service.AddSelectedImage(3);

            // Assert
            _service.SelectedImageIds.Should().HaveCount(3);
            _service.SelectedImageIds.Should().Contain(new[] { 1, 2, 3 });
        }

        [Fact]
        public void RemoveSelectedImage_RemovesImageIdAndPublishesEvent()
        {
            // Arrange
            _service.AddSelectedImage(1);
            _mockEvents.Invocations.Clear(); // Clear add event

            ImageSelectionChangedEventArgs? capturedEvent = null;
            _mockEvents.Setup(x => x.Publish(It.IsAny<ImageSelectionChangedEventArgs>()))
                .Callback<ImageSelectionChangedEventArgs>(e => capturedEvent = e);

            // Act
            _service.RemoveSelectedImage(1);

            // Assert
            _service.SelectedImageIds.Should().BeEmpty();
            _mockEvents.Verify(x => x.Publish(It.IsAny<ImageSelectionChangedEventArgs>()), Times.Once);
            capturedEvent.Should().NotBeNull();
            capturedEvent!.SelectedCount.Should().Be(0);
        }

        [Fact]
        public void RemoveSelectedImage_WhenNotSelected_DoesNotPublishEvent()
        {
            // Act
            _service.RemoveSelectedImage(999); // Not in list

            // Assert
            _mockEvents.Verify(x => x.Publish(It.IsAny<ImageSelectionChangedEventArgs>()), Times.Never);
        }

        [Fact]
        public void RemoveSelectedImage_FromMultiple_RemovesOnlySpecified()
        {
            // Arrange
            _service.AddSelectedImage(1);
            _service.AddSelectedImage(2);
            _service.AddSelectedImage(3);
            _mockEvents.Invocations.Clear();

            // Act
            _service.RemoveSelectedImage(2);

            // Assert
            _service.SelectedImageIds.Should().HaveCount(2);
            _service.SelectedImageIds.Should().Contain(new[] { 1, 3 });
            _service.SelectedImageIds.Should().NotContain(2);
        }

        [Fact]
        public void ClearSelectedImages_ClearsAllAndPublishesEvent()
        {
            // Arrange
            _service.AddSelectedImage(1);
            _service.AddSelectedImage(2);
            _service.AddSelectedImage(3);
            _mockEvents.Invocations.Clear(); // Clear add events

            ImageSelectionChangedEventArgs? capturedEvent = null;
            _mockEvents.Setup(x => x.Publish(It.IsAny<ImageSelectionChangedEventArgs>()))
                .Callback<ImageSelectionChangedEventArgs>(e => capturedEvent = e);

            // Act
            _service.ClearSelectedImages();

            // Assert
            _service.SelectedImageIds.Should().BeEmpty();
            _mockEvents.Verify(x => x.Publish(It.IsAny<ImageSelectionChangedEventArgs>()), Times.Once);
            capturedEvent.Should().NotBeNull();
            capturedEvent!.SelectedCount.Should().Be(0);
        }

        [Fact]
        public void ClearSelectedImages_WhenAlreadyEmpty_DoesNotPublishEvent()
        {
            // Act
            _service.ClearSelectedImages(); // Already empty

            // Assert
            _mockEvents.Verify(x => x.Publish(It.IsAny<ImageSelectionChangedEventArgs>()), Times.Never);
        }

        [Fact]
        public void ReplaceSelectedImages_ReplacesListAndPublishesEvent()
        {
            // Arrange
            _service.AddSelectedImage(1);
            _service.AddSelectedImage(2);
            _mockEvents.Invocations.Clear(); // Clear add events

            var newIds = new List<int> { 5, 6, 7 };
            ImageSelectionChangedEventArgs? capturedEvent = null;
            _mockEvents.Setup(x => x.Publish(It.IsAny<ImageSelectionChangedEventArgs>()))
                .Callback<ImageSelectionChangedEventArgs>(e => capturedEvent = e);

            // Act
            _service.ReplaceSelectedImages(newIds);

            // Assert
            _service.SelectedImageIds.Should().BeEquivalentTo(newIds);
            _mockEvents.Verify(x => x.Publish(It.IsAny<ImageSelectionChangedEventArgs>()), Times.Once);
            capturedEvent.Should().NotBeNull();
            capturedEvent!.SelectedCount.Should().Be(3);
        }

        [Fact]
        public void ReplaceSelectedImages_WhenNullProvided_ReplacesWithEmptyList()
        {
            // Arrange
            _service.AddSelectedImage(1);
            _mockEvents.Invocations.Clear();

            // Act
            _service.ReplaceSelectedImages(null);

            // Assert
            _service.SelectedImageIds.Should().BeEmpty();
            _mockEvents.Verify(x => x.Publish(It.IsAny<ImageSelectionChangedEventArgs>()), Times.Once);
        }

        [Fact]
        public void ReplaceSelectedImages_PublishesEventWithCorrectCount()
        {
            // Arrange
            var newIds = new List<int> { 10, 20, 30, 40, 50 };
            ImageSelectionChangedEventArgs? capturedEvent = null;
            _mockEvents.Setup(x => x.Publish(It.IsAny<ImageSelectionChangedEventArgs>()))
                .Callback<ImageSelectionChangedEventArgs>(e => capturedEvent = e);

            // Act
            _service.ReplaceSelectedImages(newIds);

            // Assert
            capturedEvent.Should().NotBeNull();
            capturedEvent!.SelectedCount.Should().Be(5);
            capturedEvent.SelectedImageIds.Should().BeEquivalentTo(newIds);
        }

        #endregion

        #region Service Initialization Tests

        [Fact]
        public void Constructor_InitializesWithEmptySelectedImages()
        {
            // Assert
            _service.SelectedImageIds.Should().NotBeNull();
            _service.SelectedImageIds.Should().BeEmpty();
        }

        [Fact]
        public void Constructor_InitializesWithNullFoldersAndProjects()
        {
            // Assert
            _service.Folders.Should().BeNull();
            _service.Projects.Should().BeNull();
        }

        #endregion
    }
}
