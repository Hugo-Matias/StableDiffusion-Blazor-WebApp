using BlazorWebApp.Events;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace BlazorWebApp.Tests.Services
{
    /// <summary>
    /// Tests for SessionService focusing on canvas state, input images, image editor, and session videos.
    /// SessionService is scoped (per browser tab) and manages transient UI state.
    /// </summary>
    public class SessionServiceTests
    {
        private readonly Mock<IEventService> _mockEvents;
        private readonly SessionService _service;

        public SessionServiceTests()
        {
            _mockEvents = new Mock<IEventService>();
            _service = new SessionService(_mockEvents.Object);
        }

        #region Canvas State Tests

        [Fact]
        public void CanvasImageData_WhenSet_PublishesEvent()
        {
            // Arrange
            var imageData = "data:image/png;base64,test";
            CanvasImageDataChangedEventArgs? capturedEvent = null;
            _mockEvents.Setup(x => x.Publish(It.IsAny<CanvasImageDataChangedEventArgs>()))
                .Callback<CanvasImageDataChangedEventArgs>(e => capturedEvent = e);

            // Act
            _service.CanvasImageData = imageData;

            // Assert
            _service.CanvasImageData.Should().Be(imageData);
            _mockEvents.Verify(x => x.Publish(It.IsAny<CanvasImageDataChangedEventArgs>()), Times.Once);
            capturedEvent.Should().NotBeNull();
            capturedEvent!.ImageData.Should().Be(imageData);
        }

        [Fact]
        public void CanvasMaskData_CanBeSetAndRetrieved()
        {
            // Arrange
            var maskData = "data:image/png;base64,mask";

            // Act
            _service.CanvasMaskData = maskData;

            // Assert
            _service.CanvasMaskData.Should().Be(maskData);
        }

        [Fact]
        public void UpscaleImageData_CanBeSetAndRetrieved()
        {
            // Arrange
            var imageData = "data:image/png;base64,upscale";

            // Act
            _service.UpscaleImageData = imageData;

            // Assert
            _service.UpscaleImageData.Should().Be(imageData);
        }

        [Fact]
        public void CanvasStates_InitializesEmpty()
        {
            // Assert
            _service.CanvasStates.Should().NotBeNull();
            _service.CanvasStates.Should().BeEmpty();
        }

        [Fact]
        public void CanvasStates_CanAddUndoStates()
        {
            // Arrange
            var state1 = "state1";
            var state2 = "state2";

            // Act
            _service.CanvasStates.Add(state1);
            _service.CanvasStates.Add(state2);

            // Assert
            _service.CanvasStates.Should().HaveCount(2);
            _service.CanvasStates.Should().ContainInOrder(state1, state2);
        }

        #endregion

        #region Input Images Tests

        [Fact]
        public void Img2ImgInputImage_WhenSet_PublishesEvent()
        {
            // Arrange
            var imageData = "data:image/png;base64,img2img";
            Img2ImgInputImageChangedEventArgs? capturedEvent = null;
            _mockEvents.Setup(x => x.Publish(It.IsAny<Img2ImgInputImageChangedEventArgs>()))
                .Callback<Img2ImgInputImageChangedEventArgs>(e => capturedEvent = e);

            // Act
            _service.Img2ImgInputImage = imageData;

            // Assert
            _service.Img2ImgInputImage.Should().Be(imageData);
            _mockEvents.Verify(x => x.Publish(It.IsAny<Img2ImgInputImageChangedEventArgs>()), Times.Once);
            capturedEvent.Should().NotBeNull();
            capturedEvent!.ImageData.Should().Be(imageData);
        }

        [Fact]
        public void Img2VidInputImage_WhenSet_PublishesEvent()
        {
            // Arrange
            var imageData = "data:image/png;base64,img2vid";
            Img2VidInputImageChangedEventArgs? capturedEvent = null;
            _mockEvents.Setup(x => x.Publish(It.IsAny<Img2VidInputImageChangedEventArgs>()))
                .Callback<Img2VidInputImageChangedEventArgs>(e => capturedEvent = e);

            // Act
            _service.Img2VidInputImage = imageData;

            // Assert
            _service.Img2VidInputImage.Should().Be(imageData);
            _mockEvents.Verify(x => x.Publish(It.IsAny<Img2VidInputImageChangedEventArgs>()), Times.Once);
            capturedEvent.Should().NotBeNull();
            capturedEvent!.ImageData.Should().Be(imageData);
        }

        [Fact]
        public void SetImg2ImgInputImage_WithResetFalse_DoesNotResetEditorState()
        {
            // Arrange
            var originalState = new ImageEditorState { /* configure initial state */ };
            _service.ImageEditorState = originalState;
            var newImageData = "data:image/png;base64,new";

            // Act
            _service.SetImg2ImgInputImage(newImageData, resetEditorState: false);

            // Assert
            _service.Img2ImgInputImage.Should().Be(newImageData);
            _service.ImageEditorState.Should().BeSameAs(originalState);
        }

        [Fact]
        public void SetImg2ImgInputImage_WithResetTrue_ResetsEditorState()
        {
            // Arrange
            var originalState = new ImageEditorState { /* configure initial state */ };
            _service.ImageEditorState = originalState;
            var newImageData = "data:image/png;base64,new";

            // Act
            _service.SetImg2ImgInputImage(newImageData, resetEditorState: true);

            // Assert
            _service.Img2ImgInputImage.Should().Be(newImageData);
            _service.ImageEditorState.Should().NotBeSameAs(originalState);
        }

        [Fact]
        public void SetImg2ImgInputImage_WithResetTrue_SameImage_DoesNotResetEditorState()
        {
            // Arrange
            var initialImage = "data:image/png;base64,same";
            _service.SetImg2ImgInputImage(initialImage, resetEditorState: false);
            var originalState = new ImageEditorState { /* configure state */ };
            _service.ImageEditorState = originalState;

            // Act
            _service.SetImg2ImgInputImage(initialImage, resetEditorState: true);

            // Assert - should not reset because image is the same
            _service.ImageEditorState.Should().BeSameAs(originalState);
        }

        #endregion

        #region Image Editor Tests

        [Fact]
        public void ImageEditorState_InitializesWithNewInstance()
        {
            // Assert
            _service.ImageEditorState.Should().NotBeNull();
        }

        [Fact]
        public void ImageEditorState_WhenSet_PublishesEvent()
        {
            // Arrange
            var newState = new ImageEditorState();
            ImageEditorStateChangedEventArgs? capturedEvent = null;
            _mockEvents.Setup(x => x.Publish(It.IsAny<ImageEditorStateChangedEventArgs>()))
                .Callback<ImageEditorStateChangedEventArgs>(e => capturedEvent = e);

            // Act
            _service.ImageEditorState = newState;

            // Assert
            _service.ImageEditorState.Should().BeSameAs(newState);
            _mockEvents.Verify(x => x.Publish(It.IsAny<ImageEditorStateChangedEventArgs>()), Times.Once);
            capturedEvent.Should().NotBeNull();
            capturedEvent!.EditorState.Should().BeSameAs(newState);
        }

        [Fact]
        public void ResetImageEditorState_CreatesNewStateAndPublishesEvent()
        {
            // Arrange
            var originalState = new ImageEditorState();
            _service.ImageEditorState = originalState;
            _mockEvents.Invocations.Clear();

            ImageEditorStateChangedEventArgs? capturedEvent = null;
            _mockEvents.Setup(x => x.Publish(It.IsAny<ImageEditorStateChangedEventArgs>()))
                .Callback<ImageEditorStateChangedEventArgs>(e => capturedEvent = e);

            // Act
            _service.ResetImageEditorState();

            // Assert
            _service.ImageEditorState.Should().NotBeSameAs(originalState);
            _mockEvents.Verify(x => x.Publish(It.IsAny<ImageEditorStateChangedEventArgs>()), Times.Once);
            capturedEvent.Should().NotBeNull();
        }

        #endregion

        #region Session Videos Tests

        [Fact]
        public void SessionGeneratedVideos_InitializesEmpty()
        {
            // Assert
            _service.SessionGeneratedVideos.Should().NotBeNull();
            _service.SessionGeneratedVideos.Videos.Should().BeEmpty();
        }

        [Fact]
        public void AddSessionVideo_AddsVideoAndPublishesEvent()
        {
            // Arrange
            var video = new GeneratedVideo { Filename = "test.mp4" };
            SessionVideosChangedEventArgs? capturedEvent = null;
            _mockEvents.Setup(x => x.Publish(It.IsAny<SessionVideosChangedEventArgs>()))
                .Callback<SessionVideosChangedEventArgs>(e => capturedEvent = e);

            // Act
            _service.AddSessionVideo(video);

            // Assert
            _service.SessionGeneratedVideos.Videos.Should().Contain(video);
            _service.SessionGeneratedVideos.Videos.Should().HaveCount(1);
            _mockEvents.Verify(x => x.Publish(It.IsAny<SessionVideosChangedEventArgs>()), Times.Once);
            capturedEvent.Should().NotBeNull();
            capturedEvent!.VideoCount.Should().Be(1);
            capturedEvent.Action.Should().Be(SessionVideoAction.Added);
        }

        [Fact]
        public void AddSessionVideo_InsertsAtBeginning()
        {
            // Arrange
            var video1 = new GeneratedVideo { Filename = "video1.mp4" };
            var video2 = new GeneratedVideo { Filename = "video2.mp4" };

            // Act
            _service.AddSessionVideo(video1);
            _service.AddSessionVideo(video2);

            // Assert - video2 should be first (most recent)
            _service.SessionGeneratedVideos.Videos.Should().HaveCount(2);
            _service.SessionGeneratedVideos.Videos[0].Should().BeSameAs(video2);
            _service.SessionGeneratedVideos.Videos[1].Should().BeSameAs(video1);
        }

        [Fact]
        public void AddSessionVideos_AddsMultipleVideosAndPublishesEvent()
        {
            // Arrange
            var videos = new[]
            {
                new GeneratedVideo { Filename = "video1.mp4" },
                new GeneratedVideo { Filename = "video2.mp4" },
                new GeneratedVideo { Filename = "video3.mp4" }
            };
            SessionVideosChangedEventArgs? capturedEvent = null;
            _mockEvents.Setup(x => x.Publish(It.IsAny<SessionVideosChangedEventArgs>()))
                .Callback<SessionVideosChangedEventArgs>(e => capturedEvent = e);

            // Act
            _service.AddSessionVideos(videos);

            // Assert
            _service.SessionGeneratedVideos.Videos.Should().HaveCount(3);
            _mockEvents.Verify(x => x.Publish(It.IsAny<SessionVideosChangedEventArgs>()), Times.Once);
            capturedEvent.Should().NotBeNull();
            capturedEvent!.VideoCount.Should().Be(3);
            capturedEvent.Action.Should().Be(SessionVideoAction.Added);
        }

        [Fact]
        public void RemoveSessionVideo_RemovesVideoAndPublishesEvent()
        {
            // Arrange
            var video = new GeneratedVideo { Filename = "test.mp4" };
            _service.AddSessionVideo(video);
            _mockEvents.Invocations.Clear();

            SessionVideosChangedEventArgs? capturedEvent = null;
            _mockEvents.Setup(x => x.Publish(It.IsAny<SessionVideosChangedEventArgs>()))
                .Callback<SessionVideosChangedEventArgs>(e => capturedEvent = e);

            // Act
            _service.RemoveSessionVideo(video);

            // Assert
            _service.SessionGeneratedVideos.Videos.Should().NotContain(video);
            _service.SessionGeneratedVideos.Videos.Should().BeEmpty();
            _mockEvents.Verify(x => x.Publish(It.IsAny<SessionVideosChangedEventArgs>()), Times.Once);
            capturedEvent.Should().NotBeNull();
            capturedEvent!.VideoCount.Should().Be(0);
            capturedEvent.Action.Should().Be(SessionVideoAction.Removed);
        }

        [Fact]
        public void ClearSessionVideos_RemovesAllVideosAndPublishesEvent()
        {
            // Arrange
            var videos = new[]
            {
                new GeneratedVideo { Filename = "video1.mp4" },
                new GeneratedVideo { Filename = "video2.mp4" }
            };
            _service.AddSessionVideos(videos);
            _mockEvents.Invocations.Clear();

            SessionVideosChangedEventArgs? capturedEvent = null;
            _mockEvents.Setup(x => x.Publish(It.IsAny<SessionVideosChangedEventArgs>()))
                .Callback<SessionVideosChangedEventArgs>(e => capturedEvent = e);

            // Act
            _service.ClearSessionVideos();

            // Assert
            _service.SessionGeneratedVideos.Videos.Should().BeEmpty();
            _mockEvents.Verify(x => x.Publish(It.IsAny<SessionVideosChangedEventArgs>()), Times.Once);
            capturedEvent.Should().NotBeNull();
            capturedEvent!.VideoCount.Should().Be(0);
            capturedEvent.Action.Should().Be(SessionVideoAction.Cleared);
        }

        #endregion

        #region Service Initialization Tests

        [Fact]
        public void Constructor_InitializesAllPropertiesCorrectly()
        {
            // Assert
            _service.CanvasImageData.Should().BeEmpty();
            _service.CanvasMaskData.Should().BeNull();
            _service.UpscaleImageData.Should().BeEmpty();
            _service.CanvasStates.Should().NotBeNull().And.BeEmpty();
            _service.Img2ImgInputImage.Should().BeEmpty();
            _service.Img2VidInputImage.Should().BeEmpty();
            _service.ImageEditorState.Should().NotBeNull();
            _service.SessionGeneratedVideos.Should().NotBeNull();
            _service.SessionGeneratedVideos.Videos.Should().BeEmpty();
        }

        #endregion
    }
}
