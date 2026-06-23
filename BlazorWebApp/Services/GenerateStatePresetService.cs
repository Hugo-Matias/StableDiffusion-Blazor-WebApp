using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Services
{
    public class GenerateStatePresetService : IGenerateStatePresetService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public GenerateStatePresetService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<GenerateStatePreset>> GetPresetsAsync(Guid workflowId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.GenerateStatePresets
                .AsNoTracking()
                .Where(p => p.WorkflowId == workflowId)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        public async Task<GenerateStatePreset?> GetPresetAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.GenerateStatePresets
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<GenerateStatePreset> CreatePresetAsync(string name, GenerationParameters parameters)
        {
            var normalizedName = NormalizeName(name);
            var workflowId = RequireWorkflowId(parameters);
            var now = DateTime.UtcNow;

            var preset = new GenerateStatePreset
            {
                Name = normalizedName,
                WorkflowId = workflowId,
                CreatedAt = now,
                UpdatedAt = now,
                Body = CreateBody(parameters, workflowId)
            };

            await using var context = await _contextFactory.CreateDbContextAsync();
            context.GenerateStatePresets.Add(preset);
            await context.SaveChangesAsync();
            return preset;
        }

        public async Task<GenerateStatePreset?> RenamePresetAsync(int id, string name)
        {
            var normalizedName = NormalizeName(name);

            await using var context = await _contextFactory.CreateDbContextAsync();
            var preset = await context.GenerateStatePresets.FirstOrDefaultAsync(p => p.Id == id);
            if (preset == null) return null;

            preset.Name = normalizedName;
            preset.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            return preset;
        }

        public async Task<GenerateStatePreset?> UpdatePresetAsync(int id, string name, GenerationParameters parameters)
        {
            var normalizedName = NormalizeName(name);
            var workflowId = RequireWorkflowId(parameters);

            await using var context = await _contextFactory.CreateDbContextAsync();
            var preset = await context.GenerateStatePresets.FirstOrDefaultAsync(p => p.Id == id);
            if (preset == null) return null;

            preset.Name = normalizedName;
            preset.WorkflowId = workflowId;
            preset.Body = CreateBody(parameters, workflowId);
            preset.UpdatedAt = DateTime.UtcNow;
            context.Entry(preset).Property(p => p.Body).IsModified = true;
            await context.SaveChangesAsync();
            return preset;
        }

        public async Task DeletePresetAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var preset = await context.GenerateStatePresets.FirstOrDefaultAsync(p => p.Id == id);
            if (preset == null) return;

            context.GenerateStatePresets.Remove(preset);
            await context.SaveChangesAsync();
        }

        private static GenerateStatePresetBody CreateBody(GenerationParameters parameters, Guid workflowId)
        {
            var snapshot = parameters.Clone();
            snapshot.WorkflowId = workflowId;
            return new GenerateStatePresetBody
            {
                SchemaVersion = 1,
                Parameters = snapshot
            };
        }

        private static Guid RequireWorkflowId(GenerationParameters parameters)
        {
            if (parameters.WorkflowId is Guid workflowId)
            {
                return workflowId;
            }

            throw new InvalidOperationException("A workflow must be selected before saving a Generate state preset.");
        }

        private static string NormalizeName(string name)
        {
            var normalized = (name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException("Preset name is required.", nameof(name));
            }

            return normalized;
        }
    }
}