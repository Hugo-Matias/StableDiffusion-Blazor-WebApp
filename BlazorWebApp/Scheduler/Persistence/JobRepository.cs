using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Scheduler.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Scheduler.Persistence
{
    /// <summary>
    /// EF Core implementation of <see cref="IJobRepository"/>. Follows the same pattern as
    /// <see cref="BlazorWebApp.Services.WorkflowStateService"/>: each call creates a fresh
    /// scoped <see cref="AppDbContext"/> via <see cref="IDbContextFactory{TContext}"/>,
    /// logs and swallows errors for read paths, and surfaces failures on write paths.
    /// </summary>
    public class JobRepository : IJobRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ILogger<JobRepository> _logger;

        public JobRepository(IDbContextFactory<AppDbContext> contextFactory, ILogger<JobRepository> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<Job> CreateAsync(Job job, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var entity = ToEntity(job, existing: null);
            context.Jobs.Add(entity);
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Created job {JobId} '{Name}'", job.Id, job.Name);
            return job;
        }

        /// <inheritdoc />
        public async Task UpdateAsync(Job job, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await context.Jobs.FirstOrDefaultAsync(j => j.JobId == job.Id, cancellationToken);
            if (existing == null)
            {
                context.Jobs.Add(ToEntity(job, existing: null));
            }
            else
            {
                SyncEntity(existing, job);
                // Ensure the Body column is re-serialized when callers mutate the in-place object graph.
                context.Entry(existing).Property(e => e.Body).IsModified = true;
            }
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Updated job {JobId}", job.Id);
        }

        /// <inheritdoc />
        public async Task DeleteAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await context.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId, cancellationToken);
            if (existing == null) return;
            context.Jobs.Remove(existing);
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Deleted job {JobId}", jobId);
        }

        /// <inheritdoc />
        public async Task<Job?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var entity = await context.Jobs
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.JobId == jobId, cancellationToken);
            return entity?.Body;
        }

        /// <inheritdoc />
        public async Task<List<Job>> ListAsync(CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var entities = await context.Jobs
                .AsNoTracking()
                .OrderByDescending(j => j.UpdatedAt)
                .ToListAsync(cancellationToken);
            return entities.Select(e => e.Body).ToList();
        }

        /// <inheritdoc />
        public async Task<List<Job>> GetRunningOrPausedAsync(CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var entities = await context.Jobs
                .AsNoTracking()
                .Where(j => j.Status == JobStatus.Running || j.Status == JobStatus.Paused)
                .OrderBy(j => j.CreatedAt)
                .ToListAsync(cancellationToken);
            return entities.Select(e => e.Body).ToList();
        }

        /// <inheritdoc />
        public async Task SaveRunStateAsync(Guid jobId, JobRunState runState, JobStatus status,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await context.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId, cancellationToken);
            if (existing == null)
            {
                _logger.LogWarning("SaveRunStateAsync called for missing job {JobId}", jobId);
                return;
            }

            var body = existing.Body;
            body.RunState = runState;
            body.Status = status;
            if (status == JobStatus.Running && body.LastRunAt == null)
            {
                body.LastRunAt = runState.StartedAt ?? DateTime.UtcNow;
            }

            SyncEntity(existing, body);
            // Force the Body column to be re-serialized even though its reference didn't change.
            context.Entry(existing).Property(e => e.Body).IsModified = true;
            await context.SaveChangesAsync(cancellationToken);
        }

        private static JobEntity ToEntity(Job job, JobEntity? existing)
        {
            var entity = existing ?? new JobEntity();
            SyncEntity(entity, job);
            return entity;
        }

        private static void SyncEntity(JobEntity entity, Job job)
        {
            var now = DateTime.UtcNow;
            entity.JobId = job.Id;
            entity.Name = job.Name;
            entity.Status = job.Status;
            entity.WorkflowId = job.WorkflowId;
            entity.CreatedAt = job.CreatedAt == default ? now : job.CreatedAt;
            entity.LastRunAt = job.LastRunAt;
            entity.UpdatedAt = now;
            entity.Body = job;
        }
    }
}
