using BlazorWebApp.Events;
using BlazorWebApp.Models;
using BlazorWebApp.Scheduler.Engine;
using BlazorWebApp.Scheduler.Models;
using BlazorWebApp.Scheduler.Persistence;
using BlazorWebApp.Services;

namespace BlazorWebApp.Scheduler
{
    /// <summary>
    /// Default <see cref="ISchedulerService"/> implementation.
    /// Serializes all public lifecycle calls with an internal lock so a single instance can safely be
    /// consumed from Blazor components and background callers alike.
    /// </summary>
    public class SchedulerService : ISchedulerService
    {
        private readonly IJobRepository _jobs;
        private readonly IVariationSequencer _sequencer;
        private readonly IDirectiveExecutor _directives;
        private readonly IParameterApplier _applier;
        private readonly IJobGenerationRunner _runner;
        private readonly IWorkflowService _workflows;
        private readonly IEventService _events;
        private readonly ILogger<SchedulerService> _logger;

        private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

        // Signals owned by the currently running job. Null when idle.
        private CancellationTokenSource? _stopCts;
        private CancellationTokenSource? _skipCts;
        private TaskCompletionSource? _pauseResumeSignal;
        private volatile bool _pauseRequested;
        private Guid? _runningJobId;
        private JobStatus? _runningStatus;

        public SchedulerService(
            IJobRepository jobs,
            IVariationSequencer sequencer,
            IDirectiveExecutor directives,
            IParameterApplier applier,
            IJobGenerationRunner runner,
            IWorkflowService workflows,
            IEventService events,
            ILogger<SchedulerService> logger)
        {
            _jobs = jobs;
            _sequencer = sequencer;
            _directives = directives;
            _applier = applier;
            _runner = runner;
            _workflows = workflows;
            _events = events;
            _logger = logger;
        }

        /// <inheritdoc />
        public Guid? RunningJobId => _runningJobId;

        /// <inheritdoc />
        public JobStatus? RunningJobStatus => _runningStatus;

        /// <inheritdoc />
        public Task RunAsync(Guid jobId, CancellationToken cancellationToken = default)
            => ExecuteJobAsync(jobId, resume: false, cancellationToken);

        /// <inheritdoc />
        public Task ResumeAsync(Guid jobId, CancellationToken cancellationToken = default)
            => ExecuteJobAsync(jobId, resume: true, cancellationToken);

        /// <inheritdoc />
        public Task PauseAsync()
        {
            _pauseRequested = true;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync()
        {
            _stopCts?.Cancel();
            // Also release any pause wait so the loop can observe cancellation immediately.
            _pauseResumeSignal?.TrySetResult();
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task SkipCurrentAsync()
        {
            _skipCts?.Cancel();
            return Task.CompletedTask;
        }

        private async Task ExecuteJobAsync(Guid jobId, bool resume, CancellationToken cancellationToken)
        {
            var job = await _jobs.GetByIdAsync(jobId)
                ?? throw new InvalidOperationException($"Job {jobId} not found.");

            if (_runningJobId is not null)
                throw new InvalidOperationException($"Scheduler is already running job {_runningJobId}.");

            _runningJobId = jobId;
            _stopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _pauseRequested = false;
            _pauseResumeSignal = null;

            try
            {
                if (!resume)
                {
                    job.RunState = new JobRunState
                    {
                        CurrentActionIndex = 0,
                        CurrentIterationIndex = 0,
                        CompletedImages = 0,
                        FailedImages = 0,
                        StartedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    };
                }
                else if (job.RunState.StartedAt is null)
                {
                    job.RunState.StartedAt = DateTime.UtcNow;
                }

                await RunInternalAsync(job, _stopCts.Token);
            }
            finally
            {
                _stopCts?.Dispose();
                _stopCts = null;
                _skipCts = null;
                _pauseResumeSignal = null;
                _pauseRequested = false;
                _runningJobId = null;
                _runningStatus = null;
            }
        }

        private async Task RunInternalAsync(Job job, CancellationToken ct)
        {
            var workflow = job.WorkflowId.HasValue ? _workflows.GetWorkflowById(job.WorkflowId.Value) : null;
            if (workflow is null)
            {
                await FailAsync(job, "Job is not bound to a valid workflow.");
                return;
            }

            // Build all action plans up front so total iteration count is known for progress reporting.
            var plans = new List<VariationPlan>(job.Actions.Count);
            int totalIterations = 0;
            foreach (var action in job.Actions.OrderBy(a => a.Order))
            {
                ct.ThrowIfCancellationRequested();
                var plan = await _sequencer.BuildPlanAsync(action, ct);
                plans.Add(plan);
                var repeat = Math.Max(1, action.Repeat);
                totalIterations += plan.EffectiveCount * repeat;
            }
            job.RunState.TotalIterations = totalIterations;

            job.Status = JobStatus.Running;
            job.LastRunAt = DateTime.UtcNow;
            _runningStatus = job.Status;
            await _jobs.UpdateAsync(job);
            _events.Publish(new JobStartedEventArgs(job.Id, job.Name, totalIterations, DateTime.UtcNow));

            try
            {
                for (int actionIndex = job.RunState.CurrentActionIndex; actionIndex < job.Actions.Count; actionIndex++)
                {
                    ct.ThrowIfCancellationRequested();
                    await WaitIfPausedAsync(job, ct);

                    var action = job.Actions[actionIndex];
                    var plan = plans[actionIndex];
                    var repeat = Math.Max(1, action.Repeat);
                    var effectiveCount = plan.EffectiveCount * repeat;

                    _events.Publish(new JobActionChangedEventArgs(job.Id, actionIndex, effectiveCount, action.Label));

                    int startIteration = actionIndex == job.RunState.CurrentActionIndex
                        ? job.RunState.CurrentIterationIndex
                        : 0;

                    int iterationInPlan = 0;
                    for (int rep = 0; rep < repeat; rep++)
                    {
                        foreach (var iterationSet in _sequencer.Enumerate(plan))
                        {
                            if (iterationInPlan < startIteration) { iterationInPlan++; continue; }

                            ct.ThrowIfCancellationRequested();
                            await WaitIfPausedAsync(job, ct);

                            job.RunState.CurrentActionIndex = actionIndex;
                            job.RunState.CurrentIterationIndex = iterationInPlan;
                            job.RunState.UpdatedAt = DateTime.UtcNow;

                            await RunIterationAsync(job, actionIndex, action, iterationSet, workflow, ct);

                            iterationInPlan++;
                        }
                    }
                }

                job.Status = JobStatus.Completed;
                job.RunState.UpdatedAt = DateTime.UtcNow;
                _runningStatus = job.Status;
                await _jobs.SaveRunStateAsync(job.Id, job.RunState, job.Status);
                _events.Publish(new JobCompletedEventArgs(
                    job.Id, job.Status, job.RunState.CompletedImages, job.RunState.FailedImages,
                    totalIterations, null, DateTime.UtcNow));
            }
            catch (OperationCanceledException)
            {
                if (_pauseRequested)
                {
                    job.Status = JobStatus.Paused;
                    _runningStatus = job.Status;
                    await _jobs.SaveRunStateAsync(job.Id, job.RunState, job.Status);
                    _events.Publish(new JobCompletedEventArgs(
                        job.Id, job.Status, job.RunState.CompletedImages, job.RunState.FailedImages,
                        totalIterations, "Paused", DateTime.UtcNow));
                }
                else
                {
                    job.Status = JobStatus.Cancelled;
                    _runningStatus = job.Status;
                    await _jobs.SaveRunStateAsync(job.Id, job.RunState, job.Status);
                    _events.Publish(new JobCompletedEventArgs(
                        job.Id, job.Status, job.RunState.CompletedImages, job.RunState.FailedImages,
                        totalIterations, "Cancelled", DateTime.UtcNow));
                }
            }
            catch (Exception ex)
            {
                await FailAsync(job, ex.Message, totalIterations);
            }
        }

        private async Task RunIterationAsync(
            Job job, int actionIndex, JobAction action, IterationValueSet iteration,
            Workflow workflow, CancellationToken ct)
        {
            // 1. Clone base parameters
            var parameters = job.BaseParameters.Clone();

            // 2. Resolve output config: job default <- action override <- directives/targets
            var output = new JobOutputConfig
            {
                ProjectName = action.OutputOverride?.ProjectName ?? job.OutputConfig.ProjectName,
                FolderName = action.OutputOverride?.FolderName ?? job.OutputConfig.FolderName,
            };

            // 3. Apply enabled directives in declared order
            foreach (var directive in action.Directives)
                _directives.Apply(parameters, output, directive);

            // 4. Apply iteration variation values
            foreach (var iv in iteration.Values)
                _applier.Apply(parameters, output, iv.Variation.Target, iv.Value);

            // 5. Invoke the generation pipeline with a per-iteration skip CTS linked to the stop CTS.
            _skipCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            try
            {
                var images = await _runner.RunAsync(parameters, workflow, output, _skipCts.Token);
                job.RunState.CompletedImages++;
                if (images?.Images is { Count: > 0 } list)
                {
                    foreach (var img in list)
                    {
                        if (img != null && img.Id > 0)
                            job.RunState.GeneratedImageIds.Add(img.Id);
                    }
                }
                _events.Publish(new JobImageGeneratedEventArgs(job.Id, actionIndex, iteration.Index, images));
            }
            catch (OperationCanceledException) when (_skipCts?.IsCancellationRequested == true && !ct.IsCancellationRequested)
            {
                _logger.LogInformation("Scheduler: iteration {Iter} of action {Action} skipped.", iteration.Index, actionIndex);
                job.RunState.FailedImages++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduler iteration failed (action {Action}, iteration {Iter})", actionIndex, iteration.Index);
                job.RunState.FailedImages++;
            }
            finally
            {
                _skipCts?.Dispose();
                _skipCts = null;
            }

            // CurrentIterationIndex tracks "next iteration to run" so Resume picks up at the right spot.
            job.RunState.CurrentIterationIndex = iteration.Index + 1;
            job.RunState.UpdatedAt = DateTime.UtcNow;
            await _jobs.SaveRunStateAsync(job.Id, job.RunState, JobStatus.Running);
            _events.Publish(new JobProgressChangedEventArgs(
                job.Id, actionIndex, iteration.Index,
                job.RunState.CompletedImages, job.RunState.FailedImages,
                job.RunState.TotalIterations));
        }

        private async Task WaitIfPausedAsync(Job job, CancellationToken ct)
        {
            if (!_pauseRequested) return;

            // Persist paused status before throwing so resume has a valid snapshot.
            job.Status = JobStatus.Paused;
            _runningStatus = job.Status;
            await _jobs.SaveRunStateAsync(job.Id, job.RunState, job.Status);

            // A pause always bubbles up to ExecuteJobAsync's catch so the job terminates at a well-defined
            // Paused state. _pauseRequested is intentionally left true so the OperationCanceledException
            // handler can distinguish Paused from Canceled; ExecuteJobAsync's finally resets it.
            ct.ThrowIfCancellationRequested();
            throw new OperationCanceledException("Job paused.");
        }

        private async Task FailAsync(Job job, string error, int? totalIterations = null)
        {
            job.Status = JobStatus.Failed;
            _runningStatus = job.Status;
            await _jobs.SaveRunStateAsync(job.Id, job.RunState, job.Status);
            _events.Publish(new JobCompletedEventArgs(
                job.Id, job.Status, job.RunState.CompletedImages, job.RunState.FailedImages,
                totalIterations ?? job.RunState.TotalIterations, error, DateTime.UtcNow));
        }
    }
}
