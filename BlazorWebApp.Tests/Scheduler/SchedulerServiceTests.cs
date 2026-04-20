using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Events;
using BlazorWebApp.Models;
using BlazorWebApp.Scheduler;
using BlazorWebApp.Scheduler.Directives;
using BlazorWebApp.Scheduler.Engine;
using BlazorWebApp.Scheduler.Models;
using BlazorWebApp.Scheduler.Persistence;
using BlazorWebApp.Scheduler.Targets;
using BlazorWebApp.Scheduler.Variations;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BlazorWebApp.Tests.Scheduler;

public class SchedulerServiceTests
{
    // ---------- Helpers ----------

    private static Workflow NewWorkflow() =>
        new() { Id = Guid.NewGuid(), Title = "test-wf" };

    private sealed class FakeJobRepository : IJobRepository
    {
        public readonly Dictionary<Guid, Job> Store = new();
        public readonly List<(JobRunState state, JobStatus status)> RunStateHistory = new();

        public Task<Job> CreateAsync(Job job, CancellationToken ct = default) { Store[job.Id] = job; return Task.FromResult(job); }
        public Task UpdateAsync(Job job, CancellationToken ct = default) { Store[job.Id] = job; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Store.Remove(id); return Task.CompletedTask; }
        public Task<Job?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Store.TryGetValue(id, out var j) ? j : null);
        public Task<List<Job>> ListAsync(CancellationToken ct = default) => Task.FromResult(Store.Values.ToList());
        public Task<List<Job>> GetRunningOrPausedAsync(CancellationToken ct = default)
            => Task.FromResult(Store.Values.Where(j => j.Status is JobStatus.Running or JobStatus.Paused).ToList());
        public Task SaveRunStateAsync(Guid id, JobRunState s, JobStatus status, CancellationToken ct = default)
        {
            // Clone the state so history snapshots are stable even as the live object is mutated.
            RunStateHistory.Add((new JobRunState
            {
                CurrentActionIndex = s.CurrentActionIndex,
                CurrentIterationIndex = s.CurrentIterationIndex,
                TotalIterations = s.TotalIterations,
                CompletedImages = s.CompletedImages,
                FailedImages = s.FailedImages,
                StartedAt = s.StartedAt,
                UpdatedAt = s.UpdatedAt,
            }, status));
            if (Store.TryGetValue(id, out var job))
            {
                job.RunState = s;
                job.Status = status;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class CountingRunner : IJobGenerationRunner
    {
        public int Calls;
        public List<GenerationParameters> Parameters = new();
        public List<JobOutputConfig> Outputs = new();
        public Func<GenerationParameters, Workflow, JobOutputConfig, CancellationToken, Task<ImagesDto>>? Handler;

        public Task<ImagesDto> RunAsync(GenerationParameters p, Workflow w, JobOutputConfig o, CancellationToken ct)
        {
            Calls++;
            Parameters.Add(p.Clone());
            Outputs.Add(new JobOutputConfig { ProjectName = o.ProjectName, FolderName = o.FolderName });
            return Handler?.Invoke(p, w, o, ct) ?? Task.FromResult(new ImagesDto { Images = new() });
        }
    }

    private static (SchedulerService svc, FakeJobRepository repo, CountingRunner runner, List<EventArgs> events)
        BuildService(Workflow workflow)
    {
        var repo = new FakeJobRepository();
        var runner = new CountingRunner();
        var events = new List<EventArgs>();

        var eventService = new Mock<IEventService>();
        eventService.Setup(e => e.Publish(It.IsAny<EventArgs>()))
            .Callback<EventArgs>(events.Add);
        // Moq's generic method dispatch for Publish<T>: capture everything through non-generic override.
        eventService.Setup(e => e.Publish(It.IsAny<JobStartedEventArgs>())).Callback<JobStartedEventArgs>(e => events.Add(e));
        eventService.Setup(e => e.Publish(It.IsAny<JobProgressChangedEventArgs>())).Callback<JobProgressChangedEventArgs>(e => events.Add(e));
        eventService.Setup(e => e.Publish(It.IsAny<JobActionChangedEventArgs>())).Callback<JobActionChangedEventArgs>(e => events.Add(e));
        eventService.Setup(e => e.Publish(It.IsAny<JobImageGeneratedEventArgs>())).Callback<JobImageGeneratedEventArgs>(e => events.Add(e));
        eventService.Setup(e => e.Publish(It.IsAny<JobCompletedEventArgs>())).Callback<JobCompletedEventArgs>(e => events.Add(e));

        var workflows = new Mock<IWorkflowService>();
        workflows.Setup(w => w.GetWorkflowById(workflow.Id)).Returns(workflow);

        var applier = new ParameterApplier(NullLogger<ParameterApplier>.Instance);
        var directives = new DirectiveExecutor(applier, NullLogger<DirectiveExecutor>.Instance);
        var wildcards = new Mock<IWildcardService>();
        var materializer = new VariationMaterializer(wildcards.Object, null!, NullLogger<VariationMaterializer>.Instance);
        var sequencer = new VariationSequencer(materializer);

        var svc = new SchedulerService(
            repo, sequencer, directives, applier, runner, workflows.Object,
            eventService.Object, NullLogger<SchedulerService>.Instance);

        return (svc, repo, runner, events);
    }

    private static Job NewJob(Workflow wf, params JobAction[] actions)
    {
        var j = new Job { Name = "Test", WorkflowId = wf.Id };
        foreach (var a in actions) j.Actions.Add(a);
        return j;
    }

    // ---------- Tests ----------

    [Fact]
    public async Task Run_JobWithNoVariations_ProducesOneGeneration()
    {
        var wf = NewWorkflow();
        var (svc, repo, runner, events) = BuildService(wf);
        var job = NewJob(wf, new JobAction { Order = 0 });
        await repo.CreateAsync(job);

        await svc.RunAsync(job.Id);

        runner.Calls.Should().Be(1);
        repo.Store[job.Id].Status.Should().Be(JobStatus.Completed);
        events.OfType<JobStartedEventArgs>().Should().ContainSingle();
        events.OfType<JobCompletedEventArgs>().Should().ContainSingle()
            .Which.FinalStatus.Should().Be(JobStatus.Completed);
    }

    [Fact]
    public async Task Run_WithVariations_AppliesValuesPerIteration()
    {
        var wf = NewWorkflow();
        var (svc, repo, runner, _) = BuildService(wf);
        var action = new JobAction { Order = 0 };
        action.Variations.Add(new ListVariation
        {
            Target = new FragmentTarget { FragmentId = "main_sampler", ParamKey = "steps" },
            Values = new() { 10L, 20L, 30L }
        });
        var job = NewJob(wf, action);
        await repo.CreateAsync(job);

        await svc.RunAsync(job.Id);

        runner.Calls.Should().Be(3);
        runner.Parameters.Select(p => p.GetFragment("main_sampler")?.Values["steps"])
            .Should().Equal(10L, 20L, 30L);
    }

    [Fact]
    public async Task Run_AppliesDirectivesBeforeVariations()
    {
        var wf = NewWorkflow();
        var (svc, repo, runner, _) = BuildService(wf);
        var action = new JobAction { Order = 0 };
        action.Directives.Add(new SetValueDirective
        {
            Target = new FragmentTarget { FragmentId = "main_sampler", ParamKey = "cfg" },
            Value = 7.5
        });
        action.Variations.Add(new ListVariation
        {
            Target = new FragmentTarget { FragmentId = "main_sampler", ParamKey = "steps" },
            Values = new() { 10L }
        });
        var job = NewJob(wf, action);
        await repo.CreateAsync(job);

        await svc.RunAsync(job.Id);

        runner.Parameters[0].GetFragment("main_sampler")!.Values["cfg"].Should().Be(7.5);
        runner.Parameters[0].GetFragment("main_sampler")!.Values["steps"].Should().Be(10L);
    }

    [Fact]
    public async Task Run_TwoActions_SumIterations()
    {
        var wf = NewWorkflow();
        var (svc, repo, runner, events) = BuildService(wf);
        var a1 = new JobAction { Order = 0 };
        a1.Variations.Add(new ListVariation
        {
            Target = new FragmentTarget { FragmentId = "main_sampler", ParamKey = "steps" },
            Values = new() { 10L, 20L }
        });
        var a2 = new JobAction { Order = 1 };
        a2.Variations.Add(new ListVariation
        {
            Target = new FragmentTarget { FragmentId = "main_sampler", ParamKey = "steps" },
            Values = new() { 30L }
        });
        var job = NewJob(wf, a1, a2);
        await repo.CreateAsync(job);

        await svc.RunAsync(job.Id);

        runner.Calls.Should().Be(3);
        events.OfType<JobActionChangedEventArgs>().Should().HaveCount(2);
        events.OfType<JobStartedEventArgs>().Single().TotalIterations.Should().Be(3);
    }

    [Fact]
    public async Task Run_OutputOverride_FlowsToRunner()
    {
        var wf = NewWorkflow();
        var (svc, repo, runner, _) = BuildService(wf);
        var action = new JobAction { Order = 0, OutputOverride = new JobOutputConfig { ProjectName = "Override" } };
        var job = NewJob(wf, action);
        job.OutputConfig.ProjectName = "Default";
        await repo.CreateAsync(job);

        await svc.RunAsync(job.Id);

        runner.Outputs[0].ProjectName.Should().Be("Override");
    }

    [Fact]
    public async Task Run_MissingWorkflow_JobFails()
    {
        var wf = NewWorkflow();
        var (svc, repo, runner, events) = BuildService(wf);
        var job = new Job { Name = "X", WorkflowId = null };
        job.Actions.Add(new JobAction { Order = 0 });
        await repo.CreateAsync(job);

        await svc.RunAsync(job.Id);

        runner.Calls.Should().Be(0);
        repo.Store[job.Id].Status.Should().Be(JobStatus.Failed);
        events.OfType<JobCompletedEventArgs>().Single().FinalStatus.Should().Be(JobStatus.Failed);
    }

    [Fact]
    public async Task Stop_DuringGeneration_CancelsJob()
    {
        var wf = NewWorkflow();
        var (svc, repo, runner, events) = BuildService(wf);
        var action = new JobAction { Order = 0 };
        action.Variations.Add(new ListVariation
        {
            Target = new FragmentTarget { FragmentId = "main_sampler", ParamKey = "steps" },
            Values = new() { 10L, 20L, 30L, 40L }
        });
        var job = NewJob(wf, action);
        await repo.CreateAsync(job);

        runner.Handler = async (_, _, _, ct) =>
        {
            if (runner.Calls >= 2) await svc.StopAsync();
            ct.ThrowIfCancellationRequested();
            return new ImagesDto { Images = new() };
        };

        await svc.RunAsync(job.Id);

        runner.Calls.Should().BeLessThan(4);
        repo.Store[job.Id].Status.Should().Be(JobStatus.Cancelled);
        events.OfType<JobCompletedEventArgs>().Single().FinalStatus.Should().Be(JobStatus.Cancelled);
    }

    [Fact]
    public async Task Pause_Then_Resume_ContinuesFromSavedIndex()
    {
        var wf = NewWorkflow();
        var (svc, repo, runner, events) = BuildService(wf);
        var action = new JobAction { Order = 0 };
        action.Variations.Add(new ListVariation
        {
            Target = new FragmentTarget { FragmentId = "main_sampler", ParamKey = "steps" },
            Values = new() { 10L, 20L, 30L, 40L }
        });
        var job = NewJob(wf, action);
        await repo.CreateAsync(job);

        runner.Handler = (_, _, _, _) =>
        {
            if (runner.Calls == 2) svc.PauseAsync();
            return Task.FromResult(new ImagesDto { Images = new() });
        };

        await svc.RunAsync(job.Id);

        repo.Store[job.Id].Status.Should().Be(JobStatus.Paused);
        int callsAfterPause = runner.Calls;
        callsAfterPause.Should().BeInRange(2, 3);

        // Resume: configure runner to complete
        runner.Handler = null;
        await svc.ResumeAsync(job.Id);

        runner.Calls.Should().Be(4);
        repo.Store[job.Id].Status.Should().Be(JobStatus.Completed);
    }

    [Fact]
    public async Task SkipCurrent_SkipsInFlightIterationAndContinues()
    {
        var wf = NewWorkflow();
        var (svc, repo, runner, _) = BuildService(wf);
        var action = new JobAction { Order = 0 };
        action.Variations.Add(new ListVariation
        {
            Target = new FragmentTarget { FragmentId = "main_sampler", ParamKey = "steps" },
            Values = new() { 10L, 20L, 30L }
        });
        var job = NewJob(wf, action);
        await repo.CreateAsync(job);

        runner.Handler = async (_, _, _, ct) =>
        {
            if (runner.Calls == 2)
            {
                await svc.SkipCurrentAsync();
                ct.ThrowIfCancellationRequested();
            }
            return new ImagesDto { Images = new() };
        };

        await svc.RunAsync(job.Id);

        runner.Calls.Should().Be(3);
        repo.Store[job.Id].Status.Should().Be(JobStatus.Completed);
        repo.Store[job.Id].RunState.FailedImages.Should().Be(1);
        repo.Store[job.Id].RunState.CompletedImages.Should().Be(2);
    }

    [Fact]
    public async Task Run_RunnerThrows_CountsAsFailureAndContinues()
    {
        var wf = NewWorkflow();
        var (svc, repo, runner, _) = BuildService(wf);
        var action = new JobAction { Order = 0 };
        action.Variations.Add(new ListVariation
        {
            Target = new FragmentTarget { FragmentId = "main_sampler", ParamKey = "steps" },
            Values = new() { 10L, 20L }
        });
        var job = NewJob(wf, action);
        await repo.CreateAsync(job);

        runner.Handler = (_, _, _, _) =>
        {
            if (runner.Calls == 1) throw new InvalidOperationException("boom");
            return Task.FromResult(new ImagesDto { Images = new() });
        };

        await svc.RunAsync(job.Id);

        runner.Calls.Should().Be(2);
        repo.Store[job.Id].RunState.FailedImages.Should().Be(1);
        repo.Store[job.Id].RunState.CompletedImages.Should().Be(1);
        repo.Store[job.Id].Status.Should().Be(JobStatus.Completed);
    }

    [Fact]
    public async Task Run_TracksGeneratedImageIds_InRunState()
    {
        var wf = NewWorkflow();
        var (svc, repo, runner, _) = BuildService(wf);
        var action = new JobAction { Order = 0 };
        action.Variations.Add(new ListVariation
        {
            Target = new FragmentTarget { FragmentId = "main_sampler", ParamKey = "steps" },
            Values = new() { 10L, 20L, 30L }
        });
        var job = NewJob(wf, action);
        await repo.CreateAsync(job);

        var nextId = 101;
        runner.Handler = (_, _, _, _) =>
        {
            var dto = new ImagesDto
            {
                Images = new()
                {
                    new BlazorWebApp.Data.Entities.Image { Id = nextId++, Path = $"/img/{nextId}.png" },
                    new BlazorWebApp.Data.Entities.Image { Id = nextId++, Path = $"/img/{nextId}.png" },
                }
            };
            return Task.FromResult(dto);
        };

        await svc.RunAsync(job.Id);

        // 3 iterations x 2 images each = 6 tracked IDs
        repo.Store[job.Id].RunState.GeneratedImageIds.Should().HaveCount(6);
        repo.Store[job.Id].RunState.GeneratedImageIds.Should().OnlyHaveUniqueItems();
        repo.Store[job.Id].RunState.GeneratedImageIds.Should().OnlyContain(id => id >= 101);
    }

    [Fact]
    public async Task Run_SkipsImageIdsWithZeroOrNegativeId()
    {
        var wf = NewWorkflow();
        var (svc, repo, runner, _) = BuildService(wf);
        var job = NewJob(wf, new JobAction { Order = 0 });
        await repo.CreateAsync(job);

        runner.Handler = (_, _, _, _) => Task.FromResult(new ImagesDto
        {
            Images = new()
            {
                new BlazorWebApp.Data.Entities.Image { Id = 0, Path = "/x.png" },
                new BlazorWebApp.Data.Entities.Image { Id = -1, Path = "/y.png" },
                new BlazorWebApp.Data.Entities.Image { Id = 42, Path = "/z.png" },
            }
        });

        await svc.RunAsync(job.Id);

        repo.Store[job.Id].RunState.GeneratedImageIds.Should().ContainSingle().Which.Should().Be(42);
    }
}
