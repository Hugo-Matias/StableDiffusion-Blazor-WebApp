using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Tests.Cleanup;

public class CleanupRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory;
    private readonly CleanupRepository _repository;

    public CleanupRepositoryTests()
    {
        _factory = new TestDbContextFactory();
        _repository = new CleanupRepository(_factory);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task UpsertImageIndexAsync_CreatesAndUpdates_ByImageId()
    {
        var created = await _repository.UpsertImageIndexAsync(new CleanupImageIndex
        {
            ImageId = 10,
            ProjectId = 2,
            ModeId = 1,
            ImagePath = "first.png",
            FileExists = true,
            FileSizeBytes = 1024,
            PromptFingerprint = "portrait"
        });

        var updated = await _repository.UpsertImageIndexAsync(new CleanupImageIndex
        {
            ImageId = 10,
            ProjectId = 2,
            ModeId = 1,
            ImagePath = "first-renamed.png",
            FileExists = false,
            Status = CleanupIndexStatus.MissingFile,
            PromptFingerprint = "portrait"
        });

        updated.Id.Should().Be(created.Id);

        var loaded = await _repository.GetImageIndexAsync(10);
        loaded.Should().NotBeNull();
        loaded!.ImagePath.Should().Be("first-renamed.png");
        loaded.FileExists.Should().BeFalse();
        loaded.Status.Should().Be(CleanupIndexStatus.MissingFile);
        loaded.CreatedAtUtc.Should().Be(created.CreatedAtUtc);
        loaded.UpdatedAtUtc.Should().BeOnOrAfter(created.UpdatedAtUtc);
    }

    [Fact]
    public async Task GetStaleAndMissingQueries_ReturnExpectedRows()
    {
        var oldIndexedAt = DateTime.UtcNow.AddDays(-4);

        await _repository.UpsertImageIndexAsync(new CleanupImageIndex
        {
            ImageId = 1,
            ProjectId = 1,
            ModeId = 1,
            ImagePath = "old.png",
            FileExists = true,
            Status = CleanupIndexStatus.Indexed,
            IndexedAtUtc = oldIndexedAt,
            UpdatedAtUtc = oldIndexedAt
        });

        await _repository.UpsertImageIndexAsync(new CleanupImageIndex
        {
            ImageId = 2,
            ProjectId = 1,
            ModeId = 1,
            ImagePath = "missing.png",
            FileExists = false,
            Status = CleanupIndexStatus.MissingFile
        });

        var stale = await _repository.GetStaleImageIndexesAsync(DateTime.UtcNow.AddDays(-1), 10);
        var missing = await _repository.GetMissingFileIndexesAsync(0, 10);

        stale.Select(row => row.ImageId).Should().Contain(new[] { 1, 2 });
        missing.Should().ContainSingle(row => row.ImageId == 2);
    }

    [Fact]
    public async Task AddGroupsAsync_PersistsGroupsAndMembers_ForRun()
    {
        var run = await _repository.CreateGroupRunAsync(new CleanupGroupRun
        {
            Name = "Prompt duplicates",
            Strategy = CleanupGroupingStrategy.PromptFingerprint,
            Status = CleanupGroupRunStatus.Completed,
            ProjectId = 3
        });

        await _repository.AddGroupsAsync(run.Id, new[]
        {
            new CleanupGroup
            {
                GroupKey = "prompt:abc",
                Strategy = CleanupGroupingStrategy.PromptFingerprint,
                Reason = "same prompt fingerprint",
                RepresentativeImageId = 101,
                MemberCount = 2,
                Members =
                {
                    new CleanupGroupMember
                    {
                        ImageId = 101,
                        Role = CleanupGroupMemberRole.Representative,
                        SuggestedAction = CleanupSuggestedAction.Keep,
                        SortOrder = 0
                    },
                    new CleanupGroupMember
                    {
                        ImageId = 102,
                        Role = CleanupGroupMemberRole.CleanupCandidate,
                        SuggestedAction = CleanupSuggestedAction.Review,
                        SortOrder = 1
                    }
                }
            }
        });

        var groups = await _repository.GetGroupsAsync(run.Id, 0, 10);
        groups.Should().ContainSingle();
        groups[0].RunId.Should().Be(run.Id);
        groups[0].GroupKey.Should().Be("prompt:abc");

        var members = await _repository.GetGroupMembersAsync(groups[0].Id);
        members.Should().HaveCount(2);
        members.Select(member => member.ImageId).Should().Equal(101, 102);
        members[0].Role.Should().Be(CleanupGroupMemberRole.Representative);

        var previewMembers = await _repository.GetGroupMembersAsync(groups[0].Id, 1);
        previewMembers.Should().ContainSingle();
        previewMembers[0].ImageId.Should().Be(101);
    }

    private sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>, IDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory()
        {
            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        }

        public AppDbContext CreateDbContext() => new(_options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());

        public void Dispose()
        {
            using var context = CreateDbContext();
            context.Database.EnsureDeleted();
        }
    }
}