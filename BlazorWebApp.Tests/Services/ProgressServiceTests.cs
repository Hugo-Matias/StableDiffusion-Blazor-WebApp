using BlazorWebApp.Events;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using Microsoft.Extensions.Logging;
using Moq;
using MudBlazor;

namespace BlazorWebApp.Tests.Services;

public class ProgressServiceTests
{
    private readonly Mock<ILogger<ProgressService>> _mockLogger;
    private readonly Mock<IEventService> _mockEventService;
    private readonly ProgressService _service;

    public ProgressServiceTests()
    {
        _mockLogger = new Mock<ILogger<ProgressService>>();
        _mockEventService = new Mock<IEventService>();
        _service = new ProgressService(_mockLogger.Object, _mockEventService.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeEmptyProgressesList()
    {
        // Assert
        Assert.NotNull(_service.Progresses);
        Assert.Empty(_service.Progresses);
    }

    [Fact]
    public void Constructor_ShouldInitializeIsConvergingToFalse()
    {
        // Assert
        Assert.False(_service.IsConverging);
    }

    [Fact]
    public void Constructor_ShouldInitializeCurrentProgressToZero()
    {
        // Assert
        Assert.Equal(0, _service.CurrentProgress);
    }

    #endregion

    #region Add Tests

    [Fact]
    public void Add_ShouldAddProgressToCollection()
    {
        // Arrange
        var progress = new BaseProgress { Id = Guid.NewGuid(), Label = "Test", MaxValue = 100 };

        // Act
        _service.Add(progress);

        // Assert
        Assert.Single(_service.Progresses);
        Assert.Contains(progress, _service.Progresses);
    }

    [Fact]
    public void Add_ShouldFireOnUpdateEvent()
    {
        // Arrange
        var progress = new BaseProgress { Id = Guid.NewGuid(), Label = "Test", MaxValue = 100 };
        var eventFired = false;
        _service.OnUpdate += () => eventFired = true;

        // Act
        _service.Add(progress);

        // Assert
        Assert.True(eventFired);
    }

    [Fact]
    public void Add_ShouldAllowMultipleProgressTrackers()
    {
        // Arrange
        var progress1 = new BaseProgress { Id = Guid.NewGuid(), Label = "Test1", MaxValue = 100 };
        var progress2 = new BaseProgress { Id = Guid.NewGuid(), Label = "Test2", MaxValue = 50 };

        // Act
        _service.Add(progress1);
        _service.Add(progress2);

        // Assert
        Assert.Equal(2, _service.Progresses.Count);
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_ShouldUpdateProgressValue()
    {
        // Arrange
        var id = Guid.NewGuid();
        var progress = new BaseProgress { Id = id, Label = "Test", MaxValue = 100, Value = 0 };
        _service.Add(progress);

        // Act
        _service.Update(id, 50);

        // Assert
        Assert.Equal(50, _service.Progresses.First().Value);
    }

    [Fact]
    public void Update_ShouldFireOnUpdateEvent()
    {
        // Arrange
        var id = Guid.NewGuid();
        var progress = new BaseProgress { Id = id, Label = "Test", MaxValue = 100, Value = 0 };
        _service.Add(progress);
        var eventFireCount = 0;
        _service.OnUpdate += () => eventFireCount++;

        // Act
        _service.Update(id, 50);

        // Assert
        Assert.Equal(1, eventFireCount);
    }

    [Fact]
    public void Update_WithValueExceedingMax_ShouldRemoveProgress()
    {
        // Arrange
        var id = Guid.NewGuid();
        var progress = new BaseProgress { Id = id, Label = "Test", MaxValue = 100, Value = 0 };
        _service.Add(progress);

        // Act
        _service.Update(id, 101);

        // Assert
        Assert.Empty(_service.Progresses);
    }

    [Fact]
    public void Update_WithNegativeValue_ShouldRemoveProgress()
    {
        // Arrange
        var id = Guid.NewGuid();
        var progress = new BaseProgress { Id = id, Label = "Test", MaxValue = 100, Value = 50 };
        _service.Add(progress);

        // Act
        _service.Update(id, -1);

        // Assert
        Assert.Empty(_service.Progresses);
    }

    [Fact]
    public void Update_WithNonExistentId_ShouldNotThrow()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert - should not throw
        _service.Update(nonExistentId, 50);
    }

    #endregion

    #region Remove Tests

    [Fact]
    public void Remove_ShouldRemoveProgressFromCollection()
    {
        // Arrange
        var id = Guid.NewGuid();
        var progress = new BaseProgress { Id = id, Label = "Test", MaxValue = 100 };
        _service.Add(progress);

        // Act
        _service.Remove(id);

        // Assert
        Assert.Empty(_service.Progresses);
    }

    [Fact]
    public void Remove_ShouldFireOnUpdateEvent()
    {
        // Arrange
        var id = Guid.NewGuid();
        var progress = new BaseProgress { Id = id, Label = "Test", MaxValue = 100 };
        _service.Add(progress);
        var eventFireCount = 0;
        _service.OnUpdate += () => eventFireCount++;

        // Act
        _service.Remove(id);

        // Assert
        Assert.Equal(1, eventFireCount);
    }

    [Fact]
    public void Remove_WithNonExistentId_ShouldNotThrow()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert - should not throw
        _service.Remove(nonExistentId);
    }

    [Fact]
    public void Remove_ShouldOnlyRemoveSpecifiedProgress()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var progress1 = new BaseProgress { Id = id1, Label = "Test1", MaxValue = 100 };
        var progress2 = new BaseProgress { Id = id2, Label = "Test2", MaxValue = 50 };
        _service.Add(progress1);
        _service.Add(progress2);

        // Act
        _service.Remove(id1);

        // Assert
        Assert.Single(_service.Progresses);
        Assert.Equal(id2, _service.Progresses.First().Id);
    }

    #endregion

    #region CurrentProgress Tests

    [Fact]
    public void CurrentProgress_Set_ShouldPublishProgressChangedEvent()
    {
        // Arrange & Act
        _service.CurrentProgress = 50;

        // Assert
        _mockEventService.Verify(e => e.Publish(It.Is<ProgressChangedEventArgs>(p => p.Progress == 50)), Times.Once);
    }

    [Fact]
    public void CurrentProgress_Set_ShouldUpdateValue()
    {
        // Act
        _service.CurrentProgress = 75;

        // Assert
        Assert.Equal(75, _service.CurrentProgress);
    }

    #endregion

    #region IsConverging Tests

    [Fact]
    public void IsConverging_SetToTrue_ShouldPublishConvergingChangedEvent()
    {
        // Arrange & Act
        _service.IsConverging = true;

        // Assert
        _mockEventService.Verify(e => e.Publish(It.Is<ConvergingChangedEventArgs>(c => c.IsConverging == true)), Times.Once);
    }

    [Fact]
    public void IsConverging_SetToFalse_ShouldPublishConvergingChangedEvent()
    {
        // Arrange
        _service.IsConverging = true;

        // Act
        _service.IsConverging = false;

        // Assert
        _mockEventService.Verify(e => e.Publish(It.Is<ConvergingChangedEventArgs>(c => c.IsConverging == false)), Times.Once);
    }

    [Fact]
    public void IsConverging_Set_ShouldUpdateValue()
    {
        // Act
        _service.IsConverging = true;

        // Assert
        Assert.True(_service.IsConverging);
    }

    #endregion

    #region NotifyProgressChanged Tests

    [Fact]
    public void NotifyProgressChanged_ShouldPublishProgressChangedEvent()
    {
        // Arrange
        _service.CurrentProgress = 25;
        _mockEventService.Invocations.Clear();

        // Act
        _service.NotifyProgressChanged();

        // Assert
        _mockEventService.Verify(e => e.Publish(It.Is<ProgressChangedEventArgs>(p => p.Progress == 25)), Times.Once);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void FullLifecycle_AddUpdateRemove_ShouldWorkCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var progress = new BaseProgress 
        { 
            Id = id, 
            Label = "Download", 
            MaxValue = 100, 
            Value = 0,
            BarColor = Color.Primary
        };

        // Act - Add
        _service.Add(progress);
        Assert.Single(_service.Progresses);

        // Act - Update multiple times
        _service.Update(id, 25);
        Assert.Equal(25, _service.Progresses.First().Value);

        _service.Update(id, 50);
        Assert.Equal(50, _service.Progresses.First().Value);

        _service.Update(id, 75);
        Assert.Equal(75, _service.Progresses.First().Value);

        // Act - Complete (exceeds max)
        _service.Update(id, 100);
        Assert.Equal(100, _service.Progresses.First().Value);

        // Act - Remove
        _service.Remove(id);
        Assert.Empty(_service.Progresses);
    }

    [Fact]
    public void MultipleTrackers_ShouldBeIndependent()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var progress1 = new BaseProgress { Id = id1, Label = "Download 1", MaxValue = 100, Value = 0 };
        var progress2 = new BaseProgress { Id = id2, Label = "Download 2", MaxValue = 50, Value = 0 };

        // Act
        _service.Add(progress1);
        _service.Add(progress2);
        _service.Update(id1, 30);
        _service.Update(id2, 40);

        // Assert
        Assert.Equal(30, _service.Progresses.First(p => p.Id == id1).Value);
        Assert.Equal(40, _service.Progresses.First(p => p.Id == id2).Value);
    }

    #endregion
}
