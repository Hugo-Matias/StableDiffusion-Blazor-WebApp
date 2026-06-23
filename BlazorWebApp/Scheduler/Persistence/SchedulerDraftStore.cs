using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Scheduler.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Scheduler.Persistence
{
    /// <summary>
    /// EF Core implementation of <see cref="ISchedulerDraftStore"/> backed by the
    /// single-row <see cref="SchedulerDraft"/> table.
    /// </summary>
    public class SchedulerDraftStore : ISchedulerDraftStore
    {
        private const int SingletonId = 1;

        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ILogger<SchedulerDraftStore> _logger;

        public SchedulerDraftStore(IDbContextFactory<AppDbContext> contextFactory, ILogger<SchedulerDraftStore> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public async Task<SchedulerDraftRecord?> LoadAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
                var row = await context.SchedulerDrafts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == SingletonId, cancellationToken);
                if (row is null) return null;
                return new SchedulerDraftRecord(row.Body, row.EditingJobId, row.UpdatedAt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load Scheduler draft.");
                return null;
            }
        }

        public async Task SaveAsync(Job draft, Guid? editingJobId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var row = await context.SchedulerDrafts.FirstOrDefaultAsync(d => d.Id == SingletonId, cancellationToken);
            var now = DateTime.UtcNow;
            if (row is null)
            {
                row = new SchedulerDraft
                {
                    Id = SingletonId,
                    Body = draft,
                    EditingJobId = editingJobId,
                    UpdatedAt = now,
                };
                context.SchedulerDrafts.Add(row);
            }
            else
            {
                row.Body = draft;
                row.EditingJobId = editingJobId;
                row.UpdatedAt = now;
                // Force re-serialization of the JSON-backed Body column.
                context.Entry(row).Property(e => e.Body).IsModified = true;
            }
            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
                var row = await context.SchedulerDrafts.FirstOrDefaultAsync(d => d.Id == SingletonId, cancellationToken);
                if (row is null) return;
                context.SchedulerDrafts.Remove(row);
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear Scheduler draft.");
            }
        }
    }
}
