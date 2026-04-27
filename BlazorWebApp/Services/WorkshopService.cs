using BlazorWebApp.Data;
using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    public class WorkshopService : IWorkshopService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly OllamaService _ollama;
        private readonly IStateService _state;
        private readonly IImageService _images;
        private readonly IDatabaseService _db;
        private readonly IEventService _events;
        private readonly ILogger<WorkshopService> _logger;

        public WorkshopService(
            IDbContextFactory<AppDbContext> factory,
            OllamaService ollama,
            IStateService state,
            IImageService images,
            IDatabaseService db,
            IEventService events,
            ILogger<WorkshopService> logger)
        {
            _factory = factory;
            _ollama = ollama;
            _state = state;
            _images = images;
            _db = db;
            _events = events;
            _logger = logger;
        }

        #region Session CRUD

        public async Task<PromptWorkshopSession> CreateSessionAsync(string name, string rootPrompt)
        {
            using var context = await _factory.CreateDbContextAsync();
            var now = DateTime.UtcNow;
            var session = new PromptWorkshopSession
            {
                Name = name,
                CreatedAt = now,
                UpdatedAt = now,
                Nodes = new List<PromptWorkshopNode>
                {
                    new()
                    {
                        PromptText = rootPrompt,
                        Mode = "root",
                        GenerationNumber = 0,
                        CreatedAt = now,
                    }
                },
            };

            context.PromptWorkshopSessions.Add(session);
            await context.SaveChangesAsync();

            // Reload with full tree
            return await LoadSessionAsync(session.Id)!;
        }

        public async Task<List<PromptWorkshopSession>> ListSessionsAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.PromptWorkshopSessions
                .Include(s => s.Nodes)
                .OrderByDescending(s => s.UpdatedAt)
                .ToListAsync();
        }

        public async Task<PromptWorkshopSession?> LoadSessionAsync(int sessionId)
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.PromptWorkshopSessions
                .Include(s => s.Nodes)
                .FirstOrDefaultAsync(s => s.Id == sessionId);
        }

        public async Task RenameSessionAsync(int sessionId, string newName)
        {
            using var context = await _factory.CreateDbContextAsync();
            var session = await context.PromptWorkshopSessions.FindAsync(sessionId);
            if (session != null)
            {
                session.Name = newName;
                session.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
            }
        }

        public async Task DeleteSessionAsync(int sessionId)
        {
            using var context = await _factory.CreateDbContextAsync();
            var session = await context.PromptWorkshopSessions
                .Include(s => s.Nodes)
                .FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session != null)
            {
                context.PromptWorkshopSessions.Remove(session);
                await context.SaveChangesAsync();
            }
        }

        #endregion

        #region Node Operations

        public async Task<IReadOnlyList<PromptWorkshopNode>> GetAncestorChainAsync(int nodeId, int maxDepth)
        {
            using var context = await _factory.CreateDbContextAsync();
            var chain = new List<PromptWorkshopNode>();
            PromptWorkshopNode? current = await context.PromptWorkshopNodes.FindAsync(nodeId);

            while (current != null && chain.Count < maxDepth)
            {
                chain.Add(current);
                if (current.ParentId == null) break;
                current = await context.PromptWorkshopNodes.FindAsync(current.ParentId);
            }

            // Reverse to oldest-first order
            chain.Reverse();
            return chain.AsReadOnly();
        }

        public async Task SetCurrentNodeAsync(int sessionId, int nodeId)
        {
            using var context = await _factory.CreateDbContextAsync();
            var session = await context.PromptWorkshopSessions.FindAsync(sessionId);
            if (session != null)
            {
                session.CurrentNodeId = nodeId;
                session.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
            }
        }

        public async Task UpdateNodePromptAsync(int nodeId, string promptText)
        {
            using var context = await _factory.CreateDbContextAsync();
            var node = await context.PromptWorkshopNodes.FindAsync(nodeId);
            if (node == null) return;

            node.PromptText = promptText ?? string.Empty;
            await context.SaveChangesAsync();
        }

        public async Task<int?> GeneratePreviewAsync(int nodeId, Guid workflowId, PreviewOrientation orientation, long seed, bool useEnhancements = false)
        {
            // Snapshot the live params at the very top, before any awaits, so the user can keep
            // tweaking the Generate page while this preview drains through the queue.
            var prepared = _state.GenerationParameters.Clone();

            // Announce the queued status as soon as we have a node id and snapshot, so the UI
            // can render a pending placeholder before the server even sees the request.
            _events.Publish(new WorkshopPreviewGeneratedEventArgs(nodeId, 0, WorkshopPreviewStatus.Queued));

            // Resolve node
            using var context = await _factory.CreateDbContextAsync();
            var node = await context.PromptWorkshopNodes.FindAsync(nodeId);
            if (node == null)
            {
                _logger.LogWarning("GeneratePreviewAsync: node {NodeId} not found", nodeId);
                _events.Publish(new WorkshopPreviewGeneratedEventArgs(nodeId, 0, WorkshopPreviewStatus.Failed, "Node not found"));
                return null;
            }

            // Resolve workflow
            var workflow = _state.State.Generation.Workflows?.FirstOrDefault(w => w.Id == workflowId);
            if (workflow == null)
            {
                _logger.LogWarning("GeneratePreviewAsync: workflow {WorkflowId} not found in state", workflowId);
                _events.Publish(new WorkshopPreviewGeneratedEventArgs(nodeId, 0, WorkshopPreviewStatus.Failed, "Workflow not found"));
                return null;
            }

            // Apply per-call overrides on the snapshot
            prepared.WorkflowId = workflowId;

            var promptsFragment = prepared.GetFragment(FragmentKeys.Fragments.Prompts);
            if (promptsFragment != null)
                promptsFragment.SetValue(FragmentKeys.Params.Positive, node.PromptText ?? string.Empty);

            var (width, height) = ResolvePreviewResolution(workflow, orientation);
            var latentFragment = prepared.GetFragment(FragmentKeys.Fragments.Latent)
                ?? prepared.GetFragment(FragmentKeys.Fragments.EmptyLatent);
            if (latentFragment != null)
            {
                latentFragment.SetValue(FragmentKeys.Params.Width, width);
                latentFragment.SetValue(FragmentKeys.Params.Height, height);
                latentFragment.SetValue(FragmentKeys.Params.BatchSize, 1);
            }

            var samplerFragment = prepared.GetFragment(FragmentKeys.Fragments.MainSampler)
                ?? prepared.GetFragment(FragmentKeys.Fragments.SamplerAdvanced);
            if (samplerFragment != null)
                samplerFragment.SetValue(FragmentKeys.Params.Seed, seed);

            // Deactivate enhancement fragments unless explicitly opted-in. Workflow templates skip
            // any fragment whose IsActive == false, so this trims detailer/upscaler/refiner nodes
            // out of the preview workflow without touching the user's live state.
            if (!useEnhancements)
            {
                foreach (var key in EnhancementFragmentKeys)
                {
                    var fragment = prepared.GetFragment(key);
                    if (fragment != null) fragment.IsActive = false;
                }
            }

            // Generate via the hidden-image path (no Results-tab refresh)
            var saved = await _images.GenerateHiddenImageAsync(prepared, workflow);
            if (saved == null)
            {
                _events.Publish(new WorkshopPreviewGeneratedEventArgs(nodeId, 0, WorkshopPreviewStatus.Failed, "Generation failed"));
                return null;
            }

            // Best-effort cleanup of the previous preview (file + row)
            var previousId = node.PreviewImageId;
            if (previousId.HasValue && previousId.Value != saved.Id)
            {
                try
                {
                    var previous = await context.Images.FindAsync(previousId.Value);
                    if (previous != null)
                    {
                        if (!string.IsNullOrWhiteSpace(previous.Path) && File.Exists(previous.Path))
                        {
                            try { File.Delete(previous.Path); }
                            catch (Exception ex) { _logger.LogDebug(ex, "Could not delete previous preview file {Path}", previous.Path); }
                        }
                        context.Images.Remove(previous);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to clean up previous preview image {ImageId}", previousId.Value);
                }
            }

            // Re-fetch node on this context to update PreviewImageId
            var trackedNode = await context.PromptWorkshopNodes.FindAsync(nodeId);
            if (trackedNode != null)
            {
                trackedNode.PreviewImageId = saved.Id;
                await context.SaveChangesAsync();
            }

            _events.Publish(new WorkshopPreviewGeneratedEventArgs(nodeId, saved.Id, WorkshopPreviewStatus.Completed));
            return saved.Id;
        }

        /// <summary>
        /// Enhancement fragments deactivated on preview snapshots when <c>useEnhancements</c> is false.
        /// Mirrors the set documented in PHASE_8.6 Step 6.
        /// </summary>
        private static readonly string[] EnhancementFragmentKeys = new[]
        {
            FragmentKeys.Fragments.Detailer,
            FragmentKeys.Fragments.DetailerCore,
            FragmentKeys.Fragments.LoaderDetailer,
            FragmentKeys.Fragments.RefinerSampler,
            FragmentKeys.Fragments.Upscale,
            FragmentKeys.Fragments.UpscaleSeedVR2,
            FragmentKeys.Fragments.SeedVR2,
            FragmentKeys.Fragments.SeedVarianceEnhancer,
        };

        /// <summary>
        /// Picks a sane preview resolution per workflow base. Defaults to 1024 for SDXL/Flux/etc,
        /// 768 for SD1.5. Snapped to the nearest multiple of 64.
        /// </summary>
        private static (int width, int height) ResolvePreviewResolution(Workflow workflow, PreviewOrientation orientation)
        {
            // Long edge in pixels
            var longEdge = workflow.Base switch
            {
                ModelBase.StableDiffusion => 768, // SD1.5 family lives here
                _ => 1024,
            };
            var shortEdge = (int)Math.Round(longEdge * 0.75);

            // Snap both to multiples of 64
            longEdge = SnapTo64(longEdge);
            shortEdge = SnapTo64(shortEdge);

            return orientation == PreviewOrientation.Portrait
                ? (shortEdge, longEdge)
                : (longEdge, shortEdge);
        }

        private static int SnapTo64(int v) => Math.Max(64, (v / 64) * 64);

        #endregion

        #region Chat Mode

        public async Task<PromptWorkshopNode> AddChatChildAsync(int sessionId, int parentId, string instruction, string modelName, int ancestorDepth, ChatVerbosity verbosity = ChatVerbosity.Match, float temperature = 0.7f)
        {
            using var context = await _factory.CreateDbContextAsync();

            // Build ancestor chain for chat context
            var ancestors = await GetAncestorChainAsync(parentId, ancestorDepth);
            var parent = ancestors.LastOrDefault()
                ?? await context.PromptWorkshopNodes.FindAsync(parentId);
            if (parent == null)
                throw new ArgumentException($"Parent node {parentId} not found.");

            // Build chat messages: rich verbosity-aware system prompt + alternating history.
            var history = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = ChatEditPromptBuilder.BuildSystemPrompt(verbosity) }
            };

            foreach (var node in ancestors)
            {
                if (!string.IsNullOrWhiteSpace(node.Instruction))
                    history.Add(new() { Role = "user", Content = node.Instruction });
                history.Add(new() { Role = "assistant", Content = node.PromptText });
            }

            history.Add(new() { Role = "user", Content = instruction });

            // Call Ollama with verbosity-tuned temperature.
            var ollamaOptions = new OllamaOptions { Temperature = temperature };
            var response = await _ollama.SendChatMessage(modelName, history, ollamaOptions);
            var resultPrompt = response?.Message?.Content ?? parent.PromptText;

            // Create new child node
            var now = DateTime.UtcNow;
            var child = new PromptWorkshopNode
            {
                SessionId = sessionId,
                ParentId = parentId,
                GenerationNumber = parent.GenerationNumber + 1,
                PromptText = resultPrompt,
                Mode = "chat",
                Instruction = instruction,
                ModelUsed = modelName,
                CreatedAt = now,
            };

            context.PromptWorkshopNodes.Add(child);

            // Update session timestamp
            var session = await context.PromptWorkshopSessions.FindAsync(sessionId);
            if (session != null)
                session.UpdatedAt = now;

            await context.SaveChangesAsync();
            return child;
        }

        #endregion

        #region Evolve Mode

        public async Task<List<PromptWorkshopNode>> SpawnVariationsAsync(int sessionId, int parentId, EvolveControls controls)
        {
            using var context = await _factory.CreateDbContextAsync();
            var parent = await context.PromptWorkshopNodes.FindAsync(parentId);
            if (parent == null)
                throw new ArgumentException($"Parent node {parentId} not found.");

            var count = Math.Max(1, controls.Count);

            // Build evolve messages from the resolved system prompt template
            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content = controls.BuildSystemPrompt(count) },
                new() { Role = "user", Content =
                    $"Base prompt: {parent.PromptText}\n\nReturn {count} numbered variations, one per line." }
            };

            // Pass temperature through OllamaOptions
            var ollamaOptions = new OllamaOptions { Temperature = controls.Temperature };

            // Call Ollama
            var response = await _ollama.SendChatMessage(controls.Model, messages, ollamaOptions);
            var rawContent = response?.Message?.Content ?? string.Empty;

            // Parse numbered list
            var variations = ParseNumberedList(rawContent, expected: count);

            // Create N child nodes
            var now = DateTime.UtcNow;
            var children = new List<PromptWorkshopNode>();
            var summary = controls.BuildInstructionSummary();

            for (int i = 0; i < variations.Count; i++)
            {
                var child = new PromptWorkshopNode
                {
                    SessionId = sessionId,
                    ParentId = parentId,
                    GenerationNumber = parent.GenerationNumber + 1,
                    PromptText = variations[i],
                    Mode = "evolve",
                    Instruction = $"{summary} ({i + 1}/{variations.Count})",
                    ModelUsed = controls.Model,
                    CreatedAt = now,
                };
                context.PromptWorkshopNodes.Add(child);
                children.Add(child);
            }

            // Update session timestamp
            var session = await context.PromptWorkshopSessions.FindAsync(sessionId);
            if (session != null)
                session.UpdatedAt = now;

            await context.SaveChangesAsync();
            return children;
        }

        #endregion

        #region Helpers

        private static List<string> ParseNumberedList(string raw, int expected)
        {
            var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var rx = new Regex(@"^\d+[\.\)\:]\s*");
            var cleaned = lines
                .Select(l => rx.Replace(l, string.Empty).Trim())
                .Where(s => s.Length > 0)
                .ToList();

            return cleaned.Count >= expected ? cleaned.Take(expected).ToList() : cleaned;
        }

        #endregion
    }
}
