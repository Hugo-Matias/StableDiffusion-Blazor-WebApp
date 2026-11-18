using BlazorWebApp.Data;
using BlazorWebApp.Models;
using BlazorWebApp.Scheduler.Directives;
using BlazorWebApp.Scheduler.Models;
using BlazorWebApp.Scheduler.Persistence;
using BlazorWebApp.Scheduler.Targets;
using BlazorWebApp.Scheduler.Variations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlazorWebApp.Tests.Scheduler;

public class JobRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory;
    private readonly JobRepository _repo;

    public JobRepositoryTests()
    {
        _factory = new TestDbContextFactory();
        _repo = new JobRepository(_factory, NullLogger<JobRepository>.Instance);
    }

    public void Dispose() => _factory.Dispose();

    private static Job MakeJob(string name = "test", JobStatus status = JobStatus.Draft)
    {
        return new Job
        {
            Name = name,
            Status = status,
            WorkflowId = Guid.NewGuid(),
            OutputConfig = new JobOutputConfig { ProjectName = "P" },
            BaseParameters = new GenerationParameters(),
            Actions =
            {
                new JobAction
                {
                    Order = 0,
                    Label = "a1",
                    Directives = { new AppendPromptDirective { Text = "masterpiece", IsPrefix = true } },
                    Variations =
                    {
                        new RangeVariation
                        {
                            Target = new FragmentTarget { FragmentId = "f", ParamKey = "cfg" },
                            Start = 1, End = 3, Step = 1
                        }
                    }
                }
            }
        };
    }

    [Fact]
    public async Task CreateAsync_Persists_FullJob_Body_And_Denormalized_Columns()
    {
        var job = MakeJob("alpha", JobStatus.Draft);

        await _repo.CreateAsync(job);

        await using var ctx = await _factory.CreateDbContextAsync();
        var row = await ctx.Jobs.AsNoTracking().SingleAsync();
        row.JobId.Should().Be(job.Id);
        row.Name.Should().Be("alpha");
        row.Status.Should().Be(JobStatus.Draft);
        row.WorkflowId.Should().Be(job.WorkflowId);
        row.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        row.Body.Actions.Should().HaveCount(1);
        row.Body.Actions[0].Directives.Should().ContainSingle().Which.Should().BeOfType<AppendPromptDirective>();
    }

    [Fact]
    public async Task GetByIdAsync_Returns_Job_With_Polymorphic_Children()
    {
        var job = MakeJob();
        await _repo.CreateAsync(job);

        var loaded = await _repo.GetByIdAsync(job.Id);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(job.Id);
        loaded.Actions[0].Variations[0].Should().BeOfType<RangeVariation>();
    }

    [Fact]
    public async Task GetByIdAsync_Returns_Null_When_Missing()
    {
        var result = await _repo.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_Overwrites_Body_And_Denormalized_Columns()
    {
        var job = MakeJob("original", JobStatus.Draft);
        await _repo.CreateAsync(job);

        job.Name = "renamed";
        job.Status = JobStatus.Queued;
        job.Actions.Add(new JobAction { Order = 1, Label = "a2" });
        await _repo.UpdateAsync(job);

        await using var ctx = await _factory.CreateDbContextAsync();
        var row = await ctx.Jobs.AsNoTracking().SingleAsync();
        row.Name.Should().Be("renamed");
        row.Status.Should().Be(JobStatus.Queued);
        row.Body.Actions.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateAsync_Creates_Row_When_Missing()
    {
        var job = MakeJob("new");
        await _repo.UpdateAsync(job);

        var loaded = await _repo.GetByIdAsync(job.Id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("new");
    }

    [Fact]
    public async Task DeleteAsync_Removes_Row()
    {
        var job = MakeJob();
        await _repo.CreateAsync(job);

        await _repo.DeleteAsync(job.Id);

        var loaded = await _repo.GetByIdAsync(job.Id);
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_Missing_Is_NoOp()
    {
        var act = async () => await _repo.DeleteAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ListAsync_Orders_By_UpdatedAt_Descending()
    {
        var older = MakeJob("older");
        await _repo.CreateAsync(older);
        await Task.Delay(10);
        var newer = MakeJob("newer");
        await _repo.CreateAsync(newer);

        var list = await _repo.ListAsync();

        list.Should().HaveCount(2);
        list[0].Name.Should().Be("newer");
        list[1].Name.Should().Be("older");
    }

    [Fact]
    public async Task GetRunningOrPausedAsync_Filters_By_Status()
    {
        await _repo.CreateAsync(MakeJob("d", JobStatus.Draft));
        await _repo.CreateAsync(MakeJob("r", JobStatus.Running));
        await _repo.CreateAsync(MakeJob("p", JobStatus.Paused));
        await _repo.CreateAsync(MakeJob("c", JobStatus.Completed));

        var list = await _repo.GetRunningOrPausedAsync();

        list.Should().HaveCount(2);
        list.Select(j => j.Name).Should().BeEquivalentTo(new[] { "r", "p" });
    }

    [Fact]
    public async Task SaveRunStateAsync_Updates_RunState_And_Status_And_LastRunAt()
    {
        var job = MakeJob("run", JobStatus.Queued);
        await _repo.CreateAsync(job);

        var runState = new JobRunState
        {
            CurrentActionIndex = 0,
            CurrentIterationIndex = 2,
            TotalIterations = 5,
            CompletedImages = 2,
            StartedAt = DateTime.UtcNow
        };

        await _repo.SaveRunStateAsync(job.Id, runState, JobStatus.Running);

        var loaded = await _repo.GetByIdAsync(job.Id);
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be(JobStatus.Running);
        loaded.RunState.CurrentIterationIndex.Should().Be(2);
        loaded.RunState.CompletedImages.Should().Be(2);
        loaded.LastRunAt.Should().NotBeNull();

        await using var ctx = await _factory.CreateDbContextAsync();
        var row = await ctx.Jobs.AsNoTracking().SingleAsync();
        row.Status.Should().Be(JobStatus.Running);
    }

    [Fact]
    public async Task SaveRunStateAsync_Missing_Is_Logged_NoOp()
    {
        var act = async () => await _repo.SaveRunStateAsync(
            Guid.NewGuid(), new JobRunState(), JobStatus.Running);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Denormalized_Columns_Are_Indexable()
    {
        await _repo.CreateAsync(MakeJob("a", JobStatus.Running));
        await _repo.CreateAsync(MakeJob("b", JobStatus.Draft));

        await using var ctx = await _factory.CreateDbContextAsync();
        var runningRows = await ctx.Jobs
            .Where(j => j.Status == JobStatus.Running)
            .Select(j => new { j.JobId, j.Name, j.Status })
            .ToListAsync();

        runningRows.Should().ContainSingle().Which.Name.Should().Be("a");
    }

    /// <summary>
    /// Minimal <see cref="IDbContextFactory{AppDbContext}"/> backed by EF InMemory, sharing a
    /// single database instance across produced contexts via a unique DB name.
    /// </summary>
    private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>, IDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory()
        {
            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        public AppDbContext CreateDbContext() => new(_options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<AppDbContext>(new AppDbContext(_options));

        public void Dispose()
        {
            using var ctx = CreateDbContext();
            ctx.Database.EnsureDeleted();
        }
    }
}
