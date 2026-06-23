using System.Collections.Concurrent;
using System.Threading.Channels;
using BlazorWebApp.Data.Repositories;
using BlazorWebApp.Events;

namespace BlazorWebApp.Services.Cleanup
{
    public class CleanupMaintenanceService : ICleanupMaintenanceService, IHostedService, IDisposable
    {
        private readonly IEventService _events;
        private readonly ICleanupRepository _repository;
        private readonly ILogger<CleanupMaintenanceService> _logger;
        private readonly Channel<IReadOnlyList<int>> _queue = Channel.CreateUnbounded<IReadOnlyList<int>>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        private readonly ConcurrentDictionary<int, byte> _pendingGroupIds = new();
        private CancellationTokenSource? _workerCancellation;
        private Task? _worker;

        public CleanupMaintenanceService(
            IEventService events,
            ICleanupRepository repository,
            ILogger<CleanupMaintenanceService> logger)
        {
            _events = events;
            _repository = repository;
            _logger = logger;
        }

        public int PendingCount => _pendingGroupIds.Count;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _events.Subscribe<ImageRecordDeletedEventArgs>(OnImageRecordDeleted);
            _workerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _worker = Task.Run(() => ProcessQueueAsync(_workerCancellation.Token), CancellationToken.None);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _events.Unsubscribe<ImageRecordDeletedEventArgs>(OnImageRecordDeleted);
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

        public void Dispose()
        {
            _workerCancellation?.Dispose();
        }

        private void OnImageRecordDeleted(ImageRecordDeletedEventArgs args)
        {
            var groupIds = args.CleanupGroupIds.Where(id => id > 0).Distinct().ToList();
            if (groupIds.Count == 0)
            {
                return;
            }

            var queued = new List<int>();
            foreach (var groupId in groupIds)
            {
                if (_pendingGroupIds.TryAdd(groupId, 0))
                {
                    queued.Add(groupId);
                }
            }

            if (queued.Count > 0)
            {
                _queue.Writer.TryWrite(queued);
            }
        }

        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var groupIds in _queue.Reader.ReadAllAsync(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var groupId in groupIds)
                    {
                        _pendingGroupIds.TryRemove(groupId, out _);
                    }

                    try
                    {
                        var result = await _repository.ReconcileGroupsAsync(groupIds, cancellationToken);
                        if (result.UpdatedGroups > 0 || result.RemovedGroups > 0 || result.UpdatedRuns > 0)
                        {
                            _logger.LogInformation(
                                "Cleanup maintenance reconciled {UpdatedGroups} groups, removed {RemovedGroups} empty groups, and updated {UpdatedRuns} runs.",
                                result.UpdatedGroups,
                                result.RemovedGroups,
                                result.UpdatedRuns);
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to reconcile cleanup groups after image deletion.");
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }
    }
}