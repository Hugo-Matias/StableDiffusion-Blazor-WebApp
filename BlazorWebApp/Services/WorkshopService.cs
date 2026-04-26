using BlazorWebApp.Data;
using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
    public class WorkshopService : IWorkshopService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly OllamaService _ollama;
        private readonly ILogger<WorkshopService> _logger;

        public WorkshopService(IDbContextFactory<AppDbContext> factory, OllamaService ollama, ILogger<WorkshopService> logger)
        {
            _factory = factory;
            _ollama = ollama;
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

        #endregion

        #region Chat Mode

        public async Task<PromptWorkshopNode> AddChatChildAsync(int sessionId, int parentId, string instruction, string modelName, int ancestorDepth)
        {
            using var context = await _factory.CreateDbContextAsync();

            // Build ancestor chain for chat context
            var ancestors = await GetAncestorChainAsync(parentId, ancestorDepth);
            var parent = ancestors.LastOrDefault()
                ?? await context.PromptWorkshopNodes.FindAsync(parentId);
            if (parent == null)
                throw new ArgumentException($"Parent node {parentId} not found.");

            // Build chat messages from template placeholders
            var history = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content =
                    "You are a prompt-editing assistant inside a creative workshop. The conversation history is alternating " +
                    "user instructions and assistant-produced prompts. Apply the latest user instruction to the most recent " +
                    "assistant prompt while preserving every unrelated concept. Return only the updated prompt - no commentary." }
            };

            foreach (var node in ancestors)
            {
                if (!string.IsNullOrWhiteSpace(node.Instruction))
                    history.Add(new() { Role = "user", Content = node.Instruction });
                history.Add(new() { Role = "assistant", Content = node.PromptText });
            }

            history.Add(new() { Role = "user", Content = instruction });

            // Call Ollama
            var response = await _ollama.SendChatMessage(modelName, history);
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

        public async Task<List<PromptWorkshopNode>> SpawnVariationsAsync(int sessionId, int parentId, int count, string modelName)
        {
            using var context = await _factory.CreateDbContextAsync();
            var parent = await context.PromptWorkshopNodes.FindAsync(parentId);
            if (parent == null)
                throw new ArgumentException($"Parent node {parentId} not found.");

            // Build evolve messages with template placeholders
            var messages = new List<OllamaChatMessage>
            {
                new() { Role = "system", Content =
                    "You generate creative variations of an image-model prompt. Each variation must differ from the base in at " +
                    "least one meaningful way (subject, setting, style, lighting, or composition) while remaining coherent. " +
                    "Return exactly {count} variations as a numbered list. No preamble. No extra commentary." },
                new() { Role = "user", Content =
                    $"Base prompt: {parent.PromptText}\n\nReturn {count} numbered variations, one per line." }
            };

            // Call Ollama
            var response = await _ollama.SendChatMessage(modelName, messages);
            var rawContent = response?.Message?.Content ?? string.Empty;

            // Parse numbered list
            var variations = ParseNumberedList(rawContent, expected: count);

            // Create N child nodes
            var now = DateTime.UtcNow;
            var children = new List<PromptWorkshopNode>();

            for (int i = 0; i < variations.Count; i++)
            {
                var child = new PromptWorkshopNode
                {
                    SessionId = sessionId,
                    ParentId = parentId,
                    GenerationNumber = parent.GenerationNumber + 1,
                    PromptText = variations[i],
                    Mode = "evolve",
                    Instruction = $"Variation {i + 1} of {variations.Count}",
                    ModelUsed = modelName,
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
