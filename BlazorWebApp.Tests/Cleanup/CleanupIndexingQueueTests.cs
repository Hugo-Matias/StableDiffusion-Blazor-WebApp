using BlazorWebApp.Events;
using BlazorWebApp.Services;
using BlazorWebApp.Services.Cleanup;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BlazorWebApp.Tests.Cleanup;

public class CleanupIndexingQueueTests
{
    [Fact]
    public async Task StartAsync_SubscribesToImageSavedEventsAndIndexesImage()
    {
        var events = new EventService();
        var indexing = new CapturingIndexingService();
        using var queue = new CleanupIndexingQueue(
            events,
            indexing,
            Options.Create(new CleanupIndexingQueueOptions { IndexSavedImages = true }),
            NullLogger<CleanupIndexingQueue>.Instance);

        await queue.StartAsync(CancellationToken.None);
        try
        {
            var indexed = indexing.NextIndexedAsync();

            events.Publish(new ImageRecordSavedEventArgs
            {
                ImageId = 42,
                ProjectId = 7,
                ImagePath = "generated.png"
            });

            await WaitForAsync(indexed);

            indexing.IndexedImageIds.Should().Equal(42);
            events.GetSubscriberCount<ImageRecordSavedEventArgs>().Should().Be(1);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }

        events.GetSubscriberCount<ImageRecordSavedEventArgs>().Should().Be(0);
    }

    [Fact]
    public async Task StartAsync_DoesNotEnqueueSavedImagesWhenSettingIsDisabled()
    {
        var events = new EventService();
        var indexing = new CapturingIndexingService();
        using var queue = new CleanupIndexingQueue(
            events,
            indexing,
            Options.Create(new CleanupIndexingQueueOptions { IndexSavedImages = false }),
            NullLogger<CleanupIndexingQueue>.Instance);

        await queue.StartAsync(CancellationToken.None);
        try
        {
            events.Publish(new ImageRecordSavedEventArgs
            {
                ImageId = 42,
                ProjectId = 7,
                ImagePath = "generated.png"
            });

            queue.PendingCount.Should().Be(0);
            indexing.IndexedImageIds.Should().BeEmpty();
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    private static async Task WaitForAsync(Task task)
    {
        var timeout = Task.Delay(TimeSpan.FromSeconds(3));
        var completed = await Task.WhenAny(task, timeout);
        completed.Should().Be(task);
        await task;
    }

    private sealed class CapturingIndexingService : ICleanupIndexingService
    {
        private readonly TaskCompletionSource _indexed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<int> IndexedImageIds { get; } = new();

        public Task NextIndexedAsync() => _indexed.Task;

        public Task<CleanupIndexingResult> IndexImageAsync(
            int imageId,
            bool force = false,
            CancellationToken cancellationToken = default)
        {
            IndexedImageIds.Add(imageId);
            _indexed.TrySetResult();
            return Task.FromResult(new CleanupIndexingResult
            {
                LastImageId = imageId,
                TotalCandidates = 1,
                Indexed = 1
            });
        }

        public Task<CleanupIndexingResult> IndexImagesAsync(
            CleanupIndexingOptions options,
            IProgress<CleanupIndexingProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CleanupIndexingResult());
    }
}