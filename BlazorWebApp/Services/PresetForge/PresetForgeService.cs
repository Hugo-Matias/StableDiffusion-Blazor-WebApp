using BlazorWebApp.Data;
using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Services.PresetForge.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Services.PresetForge;

public sealed class PresetForgeService : IPresetForgeService
{
    private readonly PresetForgePromptComposer _composer;
    private readonly OllamaService _ollama;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public PresetForgeService(
        PresetForgePromptComposer composer,
        OllamaService ollama,
        IDbContextFactory<AppDbContext> contextFactory)
    {
        _composer = composer;
        _ollama = ollama;
        _contextFactory = contextFactory;
    }

    public async Task<PresetForgeDraft> RunAsync(PresetForgeRequest request, string modelName)
    {
        ValidateRequest(request, modelName);

        var response = await _ollama.SendChatMessage(
            modelName,
            _composer.Compose(request),
            new OllamaOptions
            {
                Temperature = request.BroadCoverage ? 0.8f : 0.65f,
                TopP = 0.9f
            },
            format: "json");

        var raw = response?.Message?.Content ?? string.Empty;
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existingPrompts = await context.Prompts.AsNoTracking().ToListAsync();

        return new PresetForgeDraft
        {
            Operation = request.Operation,
            Kind = request.Kind,
            RawResponse = raw,
            Entries = PresetForgeDraftParser.Parse(raw, request, existingPrompts),
            UpdatedAt = DateTime.UtcNow
        };
    }

    public async Task<IReadOnlyList<int>> SaveDraftAsync(PresetForgeDraft draft)
    {
        var accepted = draft.Entries
            .Where(entry => entry.Accepted && entry.IsValid)
            .ToList();
        if (accepted.Count == 0)
            throw new InvalidOperationException("No valid accepted presets are available to save.");

        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();

        var prompts = accepted.Select(entry => new Prompt
        {
            Title = entry.Name.Trim(),
            Positive = entry.Prompt.Trim(),
            Negative = string.IsNullOrWhiteSpace(entry.NegativePrompt) ? null : entry.NegativePrompt.Trim(),
            Category = entry.Category.Trim(),
            Tags = entry.Tags.ToList(),
            Loras = []
        }).ToList();

        context.Prompts.AddRange(prompts);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return prompts.Select(prompt => prompt.Id).ToList();
    }

    private static void ValidateRequest(PresetForgeRequest request, string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            throw new InvalidOperationException("Select a text LLM model before running Preset Forge.");
        if (request.Count is < 1 or > 24)
            throw new ArgumentOutOfRangeException(nameof(request.Count), "Preset count must be between 1 and 24.");
        if (request.Operation == PresetForgeOperation.Generate && string.IsNullOrWhiteSpace(request.Theme))
            throw new ArgumentException("A pack theme is required.", nameof(request));
        if (request.Operation == PresetForgeOperation.Expand && request.ReferencePresets.Count == 0)
            throw new ArgumentException("Expand requires at least one reference preset.", nameof(request));
        if (request.Kind == PresetForgeKind.EditInstruction && request.PreservationPolicy == PresetPreservationPolicy.None)
            throw new ArgumentException("Edit instructions require a preservation policy.", nameof(request));
    }
}