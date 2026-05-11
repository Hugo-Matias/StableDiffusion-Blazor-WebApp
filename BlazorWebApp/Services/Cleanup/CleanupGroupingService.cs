using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Numerics;
using System.Text.Json;

namespace BlazorWebApp.Services.Cleanup
{
    public class CleanupGroupingService : ICleanupGroupingService
    {
        private const int PersistChunkSize = 100;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ICleanupRepository _cleanupRepository;
        private readonly ILogger<CleanupGroupingService> _logger;

        public CleanupGroupingService(
            IDbContextFactory<AppDbContext> contextFactory,
            ICleanupRepository cleanupRepository,
            ILogger<CleanupGroupingService> logger)
        {
            _contextFactory = contextFactory;
            _cleanupRepository = cleanupRepository;
            _logger = logger;
        }

        public async Task<CleanupGroupingResult> GenerateGroupsAsync(CleanupGroupingOptions options, CancellationToken cancellationToken = default)
        {
            var startedAt = DateTime.UtcNow;
            var run = await _cleanupRepository.CreateGroupRunAsync(new CleanupGroupRun
            {
                Name = string.IsNullOrWhiteSpace(options.Name) ? BuildDefaultRunName(options.Strategy, startedAt) : options.Name,
                Strategy = options.Strategy,
                Status = CleanupGroupRunStatus.Running,
                ProjectId = options.ProjectId,
                ScopeKey = options.ProjectId.HasValue ? $"project:{options.ProjectId.Value}" : "all",
                ConfigurationJson = JsonSerializer.Serialize(options),
                StartedAtUtc = startedAt
            }, cancellationToken);

            try
            {
                var records = await LoadIndexedRecordsAsync(options.ProjectId, cancellationToken);
                var groups = options.Strategy switch
                {
                    CleanupGroupingStrategy.ExactDuplicate => BuildExactDuplicateGroups(records, options),
                    CleanupGroupingStrategy.PromptFingerprint => BuildPromptFingerprintGroups(records, options),
                    CleanupGroupingStrategy.NearDuplicate => BuildPerceptualHashGroups(records, options),
                    CleanupGroupingStrategy.PromptFuzzy => BuildPromptFuzzyGroups(records, options),
                    _ => throw new NotSupportedException($"Cleanup grouping strategy '{options.Strategy}' is not supported by deterministic grouping.")
                };

                groups = ApplyGroupLimit(groups, options.MaxGroups);
                await PersistGroupsAsync(run.Id, groups, cancellationToken);

                var summary = new CleanupGroupingSummary
                {
                    Strategy = options.Strategy.ToString(),
                    SourceRows = records.Count,
                    TotalGroups = groups.Count,
                    TotalMembers = groups.Sum(group => group.MemberCount),
                    ProjectId = options.ProjectId,
                    CompletedAtUtc = DateTime.UtcNow
                };

                await CompleteRunAsync(run.Id, CleanupGroupRunStatus.Completed, summary, cancellationToken: cancellationToken);
                return new CleanupGroupingResult
                {
                    RunId = run.Id,
                    Status = CleanupGroupRunStatus.Completed,
                    TotalGroups = summary.TotalGroups,
                    TotalMembers = summary.TotalMembers
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to generate cleanup groups for run {RunId}", run.Id);
                await CompleteRunAsync(run.Id, CleanupGroupRunStatus.Error, null, ex.Message, cancellationToken);
                return new CleanupGroupingResult
                {
                    RunId = run.Id,
                    Status = CleanupGroupRunStatus.Error,
                    ErrorMessage = ex.Message
                };
            }
        }

        private async Task<List<CleanupIndexRecord>> LoadIndexedRecordsAsync(int? projectId, CancellationToken cancellationToken)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var query = context.CleanupImageIndexes
                .AsNoTracking()
                .Where(index => index.Status == CleanupIndexStatus.Indexed && index.FileExists);

            if (projectId.HasValue)
            {
                query = query.Where(index => index.ProjectId == projectId.Value);
            }

            return await query
                .OrderBy(index => index.ImageId)
                .Select(index => new CleanupIndexRecord(
                    index.ImageId,
                    index.ProjectId,
                    index.ModeId,
                    index.ResourceId,
                    index.WorkflowId,
                    index.ExactHash,
                    index.PerceptualHash,
                    index.PromptFingerprint,
                    index.PromptTokenSignature,
                    index.PromptNormalized,
                    index.FileSizeBytes))
                .ToListAsync(cancellationToken);
        }

        private static List<CleanupGroup> BuildExactDuplicateGroups(IReadOnlyList<CleanupIndexRecord> records, CleanupGroupingOptions options)
        {
            return BuildKeyedGroups(
                records.Where(record => !string.IsNullOrWhiteSpace(record.ExactHash)),
                record => record.ExactHash!,
                options,
                "exact",
                "same exact file hash",
                static _ => 1.0,
                static _ => 0.0);
        }

        private static List<CleanupGroup> BuildPromptFingerprintGroups(IReadOnlyList<CleanupIndexRecord> records, CleanupGroupingOptions options)
        {
            return BuildKeyedGroups(
                records.Where(record => !string.IsNullOrWhiteSpace(record.PromptFingerprint)),
                record => record.PromptFingerprint!,
                options,
                "prompt",
                "same prompt fingerprint",
                static _ => 1.0,
                static _ => 0.0);
        }

        private static List<CleanupGroup> BuildKeyedGroups(
            IEnumerable<CleanupIndexRecord> records,
            Func<CleanupIndexRecord, string> keySelector,
            CleanupGroupingOptions options,
            string keyPrefix,
            string reason,
            Func<CleanupIndexRecord, double?> similaritySelector,
            Func<CleanupIndexRecord, double?> distanceSelector)
        {
            var minimumGroupSize = Math.Max(2, options.MinimumGroupSize);
            return records
                .GroupBy(keySelector)
                .Where(group => group.Count() >= minimumGroupSize)
                .Select(group => BuildGroup(
                    options.Strategy,
                    $"{keyPrefix}:{group.Key}",
                    reason,
                    group.OrderBy(record => record.ImageId).ToList(),
                    similaritySelector,
                    distanceSelector))
                .OrderByDescending(group => group.MemberCount)
                .ThenBy(group => group.GroupKey, StringComparer.Ordinal)
                .ToList();
        }

        private static List<CleanupGroup> BuildPerceptualHashGroups(IReadOnlyList<CleanupIndexRecord> records, CleanupGroupingOptions options)
        {
            var candidates = records
                .Select(record => new PerceptualHashRecord(record, TryParseHash(record.PerceptualHash)))
                .Where(record => record.Hash.HasValue)
                .ToList();

            if (candidates.Count == 0)
            {
                return new List<CleanupGroup>();
            }

            var union = new UnionFind(candidates.Count);
            var tree = new PerceptualHashTree();
            for (var i = 0; i < candidates.Count; i++)
            {
                var hash = candidates[i].Hash!.Value;
                foreach (var match in tree.FindWithin(hash, options.PerceptualHashMaxDistance))
                {
                    union.Union(i, match.Index);
                }

                tree.Add(hash, i);
            }

            var minimumGroupSize = Math.Max(2, options.MinimumGroupSize);
            return Enumerable.Range(0, candidates.Count)
                .GroupBy(union.Find)
                .Select(group => group.Select(index => candidates[index]).OrderBy(item => item.Record.ImageId).ToList())
                .Where(group => group.Count >= minimumGroupSize)
                .Select(group =>
                {
                    var representativeHash = group[0].Hash!.Value;
                    return BuildGroup(
                        options.Strategy,
                        $"phash:{group[0].Record.ImageId}:{group.Count}",
                        $"perceptual hash distance <= {options.PerceptualHashMaxDistance}",
                        group.Select(item => item.Record).ToList(),
                        record => 1.0 - (double)HammingDistance(TryParseHash(record.PerceptualHash)!.Value, representativeHash) / 64.0,
                        record => HammingDistance(TryParseHash(record.PerceptualHash)!.Value, representativeHash));
                })
                .OrderByDescending(group => group.MemberCount)
                .ThenBy(group => group.GroupKey, StringComparer.Ordinal)
                .ToList();
        }

        private static List<CleanupGroup> BuildPromptFuzzyGroups(IReadOnlyList<CleanupIndexRecord> records, CleanupGroupingOptions options)
        {
            var candidates = records
                .Select(record => new PromptTokenRecord(record, ParseTokenSignature(record.PromptTokenSignature)))
                .Where(record => record.Tokens.Count > 0)
                .ToList();

            if (candidates.Count == 0)
            {
                return new List<CleanupGroup>();
            }

            var union = new UnionFind(candidates.Count);
            var inverted = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidateMatches = new HashSet<int>();
                foreach (var token in candidates[i].Tokens)
                {
                    if (!inverted.TryGetValue(token, out var indexes))
                    {
                        continue;
                    }

                    foreach (var index in indexes)
                    {
                        candidateMatches.Add(index);
                    }
                }

                foreach (var match in candidateMatches)
                {
                    if (Jaccard(candidates[i].Tokens, candidates[match].Tokens) >= options.PromptFuzzyMinSimilarity)
                    {
                        union.Union(i, match);
                    }
                }

                foreach (var token in candidates[i].Tokens)
                {
                    if (!inverted.TryGetValue(token, out var indexes))
                    {
                        indexes = new List<int>();
                        inverted[token] = indexes;
                    }

                    indexes.Add(i);
                }
            }

            var minimumGroupSize = Math.Max(2, options.MinimumGroupSize);
            return Enumerable.Range(0, candidates.Count)
                .GroupBy(union.Find)
                .Select(group => group.Select(index => candidates[index]).OrderBy(item => item.Record.ImageId).ToList())
                .Where(group => group.Count >= minimumGroupSize)
                .Select(group =>
                {
                    var representativeTokens = group[0].Tokens;
                    return BuildGroup(
                        options.Strategy,
                        $"prompt-fuzzy:{group[0].Record.ImageId}:{group.Count}",
                        $"prompt token similarity >= {options.PromptFuzzyMinSimilarity:0.##}",
                        group.Select(item => item.Record).ToList(),
                        record => Jaccard(ParseTokenSignature(record.PromptTokenSignature), representativeTokens),
                        record => 1.0 - Jaccard(ParseTokenSignature(record.PromptTokenSignature), representativeTokens));
                })
                .OrderByDescending(group => group.MemberCount)
                .ThenBy(group => group.GroupKey, StringComparer.Ordinal)
                .ToList();
        }

        private static CleanupGroup BuildGroup(
            CleanupGroupingStrategy strategy,
            string groupKey,
            string reason,
            IReadOnlyList<CleanupIndexRecord> records,
            Func<CleanupIndexRecord, double?> similaritySelector,
            Func<CleanupIndexRecord, double?> distanceSelector)
        {
            var representative = records.OrderBy(record => record.ImageId).First();
            var members = records
                .OrderBy(record => record.ImageId)
                .Select((record, index) =>
                {
                    var isRepresentative = record.ImageId == representative.ImageId;
                    return new CleanupGroupMember
                    {
                        ImageId = record.ImageId,
                        Role = isRepresentative ? CleanupGroupMemberRole.Representative : CleanupGroupMemberRole.CleanupCandidate,
                        SuggestedAction = isRepresentative ? CleanupSuggestedAction.Keep : CleanupSuggestedAction.Review,
                        SimilarityScore = similaritySelector(record),
                        Distance = distanceSelector(record),
                        EstimatedBytes = record.FileSizeBytes,
                        SortOrder = index,
                        Reason = reason
                    };
                })
                .ToList();

            var similarities = members.Select(member => member.SimilarityScore).Where(value => value.HasValue).Select(value => value!.Value).ToList();
            return new CleanupGroup
            {
                GroupKey = groupKey,
                Strategy = strategy,
                Reason = reason,
                RepresentativeImageId = representative.ImageId,
                MemberCount = members.Count,
                EstimatedBytes = members.Sum(member => member.EstimatedBytes ?? 0),
                MinSimilarity = similarities.Count == 0 ? null : similarities.Min(),
                MaxSimilarity = similarities.Count == 0 ? null : similarities.Max(),
                Confidence = similarities.Count == 0 ? null : similarities.Average(),
                Members = members
            };
        }

        private async Task PersistGroupsAsync(int runId, IReadOnlyList<CleanupGroup> groups, CancellationToken cancellationToken)
        {
            foreach (var chunk in groups.Chunk(PersistChunkSize))
            {
                await _cleanupRepository.AddGroupsAsync(runId, chunk, cancellationToken);
            }
        }

        private async Task CompleteRunAsync(
            int runId,
            CleanupGroupRunStatus status,
            CleanupGroupingSummary? summary,
            string? errorMessage = null,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var run = await context.CleanupGroupRuns.FirstAsync(row => row.Id == runId, cancellationToken);
            run.Status = status;
            run.CompletedAtUtc = DateTime.UtcNow;
            run.UpdatedAtUtc = DateTime.UtcNow;
            run.ErrorMessage = errorMessage;
            if (summary != null)
            {
                run.TotalGroups = summary.TotalGroups;
                run.TotalMembers = summary.TotalMembers;
                run.SummaryJson = JsonSerializer.Serialize(summary);
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        private static List<CleanupGroup> ApplyGroupLimit(List<CleanupGroup> groups, int? maxGroups)
        {
            return maxGroups.HasValue && maxGroups.Value > 0
                ? groups.Take(maxGroups.Value).ToList()
                : groups;
        }

        private static string BuildDefaultRunName(CleanupGroupingStrategy strategy, DateTime createdAtUtc)
        {
            return $"{strategy} cleanup run {createdAtUtc:yyyy-MM-dd HH:mm:ss} UTC";
        }

        private static ulong? TryParseHash(string? hash)
        {
            return ulong.TryParse(hash, System.Globalization.NumberStyles.HexNumber, null, out var value) ? value : null;
        }

        private static int HammingDistance(ulong left, ulong right)
        {
            return BitOperations.PopCount(left ^ right);
        }

        private static HashSet<string> ParseTokenSignature(string? tokenSignature)
        {
            return string.IsNullOrWhiteSpace(tokenSignature)
                ? new HashSet<string>(StringComparer.Ordinal)
                : tokenSignature.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        }

        private static double Jaccard(IReadOnlySet<string> left, IReadOnlySet<string> right)
        {
            if (left.Count == 0 && right.Count == 0)
            {
                return 1.0;
            }

            var intersection = left.Count(token => right.Contains(token));
            var union = left.Count + right.Count - intersection;
            return union == 0 ? 0.0 : (double)intersection / union;
        }

        private sealed record CleanupIndexRecord(
            int ImageId,
            int ProjectId,
            int ModeId,
            int? ResourceId,
            string? WorkflowId,
            string? ExactHash,
            string? PerceptualHash,
            string? PromptFingerprint,
            string? PromptTokenSignature,
            string? PromptNormalized,
            long? FileSizeBytes);

        private sealed record PerceptualHashRecord(CleanupIndexRecord Record, ulong? Hash);

        private sealed record PromptTokenRecord(CleanupIndexRecord Record, HashSet<string> Tokens);

        private sealed record CleanupGroupingSummary
        {
            public string Strategy { get; init; } = string.Empty;
            public int SourceRows { get; init; }
            public int TotalGroups { get; init; }
            public int TotalMembers { get; init; }
            public int? ProjectId { get; init; }
            public DateTime CompletedAtUtc { get; init; }
        }

        private sealed class UnionFind
        {
            private readonly int[] _parents;
            private readonly int[] _ranks;

            public UnionFind(int count)
            {
                _parents = Enumerable.Range(0, count).ToArray();
                _ranks = new int[count];
            }

            public int Find(int item)
            {
                if (_parents[item] != item)
                {
                    _parents[item] = Find(_parents[item]);
                }

                return _parents[item];
            }

            public void Union(int left, int right)
            {
                var leftRoot = Find(left);
                var rightRoot = Find(right);
                if (leftRoot == rightRoot)
                {
                    return;
                }

                if (_ranks[leftRoot] < _ranks[rightRoot])
                {
                    _parents[leftRoot] = rightRoot;
                }
                else if (_ranks[leftRoot] > _ranks[rightRoot])
                {
                    _parents[rightRoot] = leftRoot;
                }
                else
                {
                    _parents[rightRoot] = leftRoot;
                    _ranks[leftRoot]++;
                }
            }
        }

        private sealed class PerceptualHashTree
        {
            private PerceptualHashNode? _root;

            public void Add(ulong hash, int index)
            {
                if (_root == null)
                {
                    _root = new PerceptualHashNode(hash, index);
                    return;
                }

                _root.Add(hash, index);
            }

            public IEnumerable<PerceptualHashMatch> FindWithin(ulong hash, int maxDistance)
            {
                return _root == null ? Enumerable.Empty<PerceptualHashMatch>() : _root.FindWithin(hash, maxDistance);
            }
        }

        private sealed class PerceptualHashNode
        {
            private readonly Dictionary<int, PerceptualHashNode> _children = new();

            public PerceptualHashNode(ulong hash, int index)
            {
                Hash = hash;
                Index = index;
            }

            public ulong Hash { get; }
            public int Index { get; }

            public void Add(ulong hash, int index)
            {
                var distance = HammingDistance(Hash, hash);
                if (_children.TryGetValue(distance, out var child))
                {
                    child.Add(hash, index);
                    return;
                }

                _children[distance] = new PerceptualHashNode(hash, index);
            }

            public IEnumerable<PerceptualHashMatch> FindWithin(ulong hash, int maxDistance)
            {
                var distance = HammingDistance(Hash, hash);
                if (distance <= maxDistance)
                {
                    yield return new PerceptualHashMatch(Index, distance);
                }

                var min = Math.Max(0, distance - maxDistance);
                var max = distance + maxDistance;
                foreach (var child in _children.Where(child => child.Key >= min && child.Key <= max))
                {
                    foreach (var match in child.Value.FindWithin(hash, maxDistance))
                    {
                        yield return match;
                    }
                }
            }
        }

        private sealed record PerceptualHashMatch(int Index, int Distance);
    }
}