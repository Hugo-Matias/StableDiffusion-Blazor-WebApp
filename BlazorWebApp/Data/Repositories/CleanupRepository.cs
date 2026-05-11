using BlazorWebApp.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Data.Repositories
{
    public class CleanupRepository : ICleanupRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public CleanupRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<CleanupImageIndex> UpsertImageIndexAsync(CleanupImageIndex index, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var now = DateTime.UtcNow;
            var existing = await context.CleanupImageIndexes
                .FirstOrDefaultAsync(row => row.ImageId == index.ImageId, cancellationToken);

            if (existing == null)
            {
                index.CreatedAtUtc = index.CreatedAtUtc == default ? now : index.CreatedAtUtc;
                index.UpdatedAtUtc = index.UpdatedAtUtc == default ? now : index.UpdatedAtUtc;
                context.CleanupImageIndexes.Add(index);
                await context.SaveChangesAsync(cancellationToken);
                return index;
            }

            index.Id = existing.Id;
            index.CreatedAtUtc = existing.CreatedAtUtc;
            index.UpdatedAtUtc = now;
            context.Entry(existing).CurrentValues.SetValues(index);
            await context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        public async Task<CleanupImageIndex?> GetImageIndexAsync(int imageId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.CleanupImageIndexes
                .AsNoTracking()
                .FirstOrDefaultAsync(index => index.ImageId == imageId, cancellationToken);
        }

        public async Task<List<CleanupImageIndex>> GetImageIndexesAsync(CleanupIndexFilter filter, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var query = context.CleanupImageIndexes.AsNoTracking();

            if (filter.ProjectId.HasValue)
            {
                query = query.Where(index => index.ProjectId == filter.ProjectId.Value);
            }

            if (filter.Status.HasValue)
            {
                query = query.Where(index => index.Status == filter.Status.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.PromptFingerprint))
            {
                query = query.Where(index => index.PromptFingerprint == filter.PromptFingerprint);
            }

            return await query
                .OrderByDescending(index => index.UpdatedAtUtc)
                .Skip(filter.Skip)
                .Take(filter.Take)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<CleanupImageIndex>> GetStaleImageIndexesAsync(DateTime staleBeforeUtc, int take, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.CleanupImageIndexes
                .AsNoTracking()
                .Where(index => index.Status == CleanupIndexStatus.Stale || index.IndexedAtUtc == null || index.IndexedAtUtc < staleBeforeUtc)
                .OrderBy(index => index.UpdatedAtUtc)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<CleanupImageIndex>> GetMissingFileIndexesAsync(int skip, int take, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.CleanupImageIndexes
                .AsNoTracking()
                .Where(index => index.Status == CleanupIndexStatus.MissingFile || !index.FileExists)
                .OrderByDescending(index => index.UpdatedAtUtc)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public async Task<CleanupGroupRun> CreateGroupRunAsync(CleanupGroupRun run, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var now = DateTime.UtcNow;
            run.CreatedAtUtc = run.CreatedAtUtc == default ? now : run.CreatedAtUtc;
            run.UpdatedAtUtc = run.UpdatedAtUtc == default ? now : run.UpdatedAtUtc;
            context.CleanupGroupRuns.Add(run);
            await context.SaveChangesAsync(cancellationToken);
            return run;
        }

        public async Task<CleanupGroupRun?> GetGroupRunAsync(int runId, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.CleanupGroupRuns
                .AsNoTracking()
                .FirstOrDefaultAsync(run => run.Id == runId, cancellationToken);
        }

        public async Task<List<CleanupGroupRun>> GetGroupRunsAsync(int skip, int take, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.CleanupGroupRuns
                .AsNoTracking()
                .OrderByDescending(run => run.CreatedAtUtc)
                .ThenByDescending(run => run.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public async Task AddGroupsAsync(int runId, IReadOnlyList<CleanupGroup> groups, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            foreach (var group in groups)
            {
                group.RunId = runId;
                group.CreatedAtUtc = group.CreatedAtUtc == default ? DateTime.UtcNow : group.CreatedAtUtc;
                foreach (var member in group.Members)
                {
                    member.CreatedAtUtc = member.CreatedAtUtc == default ? DateTime.UtcNow : member.CreatedAtUtc;
                }
            }

            context.CleanupGroups.AddRange(groups);
            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task<List<CleanupGroup>> GetGroupsAsync(int runId, int skip, int take, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.CleanupGroups
                .AsNoTracking()
                .Where(group => group.RunId == runId)
                .OrderByDescending(group => group.MemberCount)
                .ThenBy(group => group.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<CleanupGroupMember>> GetGroupMembersAsync(int groupId, int? take = null, CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            IQueryable<CleanupGroupMember> query = context.CleanupGroupMembers
                .AsNoTracking()
                .Where(member => member.GroupId == groupId)
                .OrderBy(member => member.SortOrder)
                .ThenBy(member => member.Id);

            if (take is > 0)
            {
                query = query.Take(take.Value);
            }

            return await query
                .ToListAsync(cancellationToken);
        }
    }
}