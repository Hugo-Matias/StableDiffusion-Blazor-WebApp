using System.Collections.Concurrent;
using System.Threading.Channels;
using BlazorWebApp.Events;
using Microsoft.Extensions.Options;

namespace BlazorWebApp.Services.Cleanup
{
    public class CleanupIndexingQueue : ICleanupIndexingQueue, IHostedService, IDisposable
    {
        private readonly IEventService _events;
        private readonly ICleanupIndexingService _indexingService;
        private readonly IOptions<CleanupIndexingQueueOptions> _options;
        private readonly ILogger<CleanupIndexingQueue> _logger;
        private readonly Channel<CleanupIndexingQueueItem> _queue = Channel.CreateUnbounded<CleanupIndexingQueueItem>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        private readonly ConcurrentDictionary<int, CleanupIndexingQueueItem> _pending = new();
        private CancellationTokenSource? _workerCancellation;
        private Task? _worker;

        public CleanupIndexingQueue(
            IEventService events,
            ICleanupIndexingService indexingService,
            IOptions<CleanupIndexingQueueOptions> options,
            ILogger<CleanupIndexingQueue> logger)
        {
            _events = events;
            _indexingService = indexingService;
            _options = options;
            _logger = logger;
        }

        public int PendingCount => _pending.Count;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _events.Subscribe<ImageRecordSavedEventArgs>(OnImageRecordSaved);
            _workerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _worker = Task.Run(() => ProcessQueueAsync(_workerCancellation.Token), CancellationToken.None);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _events.Unsubscribe<ImageRecordSavedEventArgs>(OnImageRecordSaved);
            _queue.Writer.TryComplete();

            if (_workerCancellation != null)
            {
                await _workerCancellation.CancelAsync();
            }

            if (_worker != null)
            {
                try
                {
                    await _worker.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (_workerCancellation?.IsCancellationRequested == true)
                {
                }
            }
        }

        public void EnqueueImage(
            int imageId,
            CleanupIndexingQueueReason reason = CleanupIndexingQueueReason.Manual)
        {
            if (imageId <= 0)
            {
                return;
            }

            var item = new CleanupIndexingQueueItem(imageId, reason, DateTime.UtcNow);
            if (_pending.TryAdd(imageId, item))
            {
                _queue.Writer.TryWrite(item);
            }
        }

        public void Dispose()
        {
            _workerCancellation?.Dispose();
        }

        private void OnImageRecordSaved(ImageRecordSavedEventArgs args)
        {
            if (!_options.Value.IndexSavedImages)
            {
                return;
            }

            EnqueueImage(
                args.ImageId,
                args.IsUpdate ? CleanupIndexingQueueReason.ImageUpdated : CleanupIndexingQueueReason.ImageSaved);
        }

        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var item in _queue.Reader.ReadAllAsync(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!_pending.TryRemove(item.ImageId, out _))
                    {
                        continue;
                    }

                    try
                    {
                        await _indexingService.IndexImageAsync(item.ImageId, cancellationToken: cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to process cleanup indexing queue item for image {ImageId}", item.ImageId);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        private sealed record CleanupIndexingQueueItem(
            int ImageId,
            CleanupIndexingQueueReason Reason,
            DateTime QueuedAtUtc);
    }
}