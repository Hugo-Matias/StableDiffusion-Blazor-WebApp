using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BlazorWebApp.Data;
using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Events;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BlazorWebApp.Tests.Services;

public class WorkshopWizardServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly TestableOllamaService _ollama = new();
    private readonly WizardIntroCatalog _intro;
    private readonly EventService _events = new();
    private readonly WorkshopWizardService _service;

    public WorkshopWizardServiceTests()
    {
        // Spin up the catalog over a temp file so the test does not depend on the project Data dir.
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "wizard_intro.json"), SampleIntroJson);

        _intro = new WizardIntroCatalog(NullLogger<WizardIntroCatalog>.Instance);
        _service = new WorkshopWizardService(
            _factory,
            _ollama,
            _intro,
            _events,
            NullLogger<WorkshopWizardService>.Instance);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task LoadOrCreateAsync_CreatesNewRow_WithIntroStage()
    {
        var row = await _service.LoadOrCreateAsync(sessionId: null);
        row.Body.Stage.Should().Be(WizardStage.Intro);
        row.Body.IntroSectionIndex.Should().Be(0);
        row.SessionId.Should().BeNull();

        // Idempotent
        var row2 = await _service.LoadOrCreateAsync(sessionId: null);
        row2.Id.Should().Be(row.Id);
    }

    [Fact]
    public async Task AdvanceIntroAsync_AppendsTurn_StitchesDraft_AdvancesIndex()
    {
        await _service.LoadOrCreateAsync(null);
        _ollama.QueueResponse(@"{""question"":""Where is the subject?"",""options"":[{""label"":""On a cliff""},{""label"":""In a forest""},{""label"":""In a city""}]}");

        var row = await _service.AdvanceIntroAsync(null, new WizardOption { Label = "A solitary lighthouse keeper" }, "test-model");

        row.Body.History.Should().HaveCount(1);
        row.Body.History[0].Source.Should().Be("intro:subject");
        row.Body.CurrentDraft.Should().Be("A solitary lighthouse keeper");
        row.Body.IntroSectionIndex.Should().Be(1);
        row.Body.Stage.Should().Be(WizardStage.Intro);
        row.Body.PendingOptions.Should().HaveCount(3);
    }

    [Fact]
    public async Task AdvanceIntroAsync_AfterLastSection_TransitionsToIteration()
    {
        await _service.LoadOrCreateAsync(null);
        // 2 sections in test catalog -> after the second intro turn we should be in Iteration.
        _ollama.QueueResponse(@"{""question"":""q"",""options"":[{""label"":""a""},{""label"":""b""},{""label"":""c""}]}");
        await _service.AdvanceIntroAsync(null, new WizardOption { Label = "Subject A" }, "m");

        _ollama.QueueResponse(@"{""draft"":""new draft"",""question"":""next?"",""options"":[{""label"":""x""},{""label"":""y""},{""label"":""z""}]}");
        var row = await _service.AdvanceIntroAsync(null, new WizardOption { Label = "Scenery B" }, "m");

        row.Body.Stage.Should().Be(WizardStage.Iteration);
        row.Body.CurrentDraft.Should().Be("Subject A, Scenery B");
        row.Body.PendingOptions.Should().HaveCount(3);
    }

    [Fact]
    public async Task ApplyVerbAsync_ReplacesDraft_AndRecordsTurn()
    {
        await SeedIterationAsync();

        _ollama.QueueResponse(@"{""draft"":""improved draft"",""question"":""more?"",""options"":[{""label"":""a""},{""label"":""b""},{""label"":""c""}]}");
        var row = await _service.ApplyVerbAsync(null, "improve", "m");

        row.Body.CurrentDraft.Should().Be("improved draft");
        row.Body.LastActionVerb.Should().Be("improve");
        row.Body.History.Last().Source.Should().Be("verb:improve");
    }

    [Fact]
    public async Task ApplyVerbAsync_RejectsUnknownVerb()
    {
        await SeedIterationAsync();
        var act = () => _service.ApplyVerbAsync(null, "explode", "m");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RequestMoreAsync_HonorsExcludeLabels()
    {
        await SeedIterationAsync();
        // After SeedIterationAsync, LastShownLabels has the previous round's labels.
        var prior = (await _service.LoadOrCreateAsync(null)).Body.LastShownLabels.ToList();

        _ollama.QueueResponse(@"{""draft"":""same"",""question"":""q"",""options"":[{""label"":""new1""},{""label"":""new2""},{""label"":""new3""}]}");
        await _service.RequestMoreAsync(null, "m");

        var sent = _ollama.LastUserPrompt;
        foreach (var label in prior)
            sent.Should().Contain(label, $"More... must list previously shown label '{label}' in excludeLabels");

        var row = await _service.LoadOrCreateAsync(null);
        row.Body.LastShownLabels.Should().Contain("new1");
        // Merge semantics: prior labels remain in the exclusion set.
        foreach (var label in prior)
            row.Body.LastShownLabels.Should().Contain(label);
    }

    [Fact]
    public async Task UndoAsync_RestoresPriorDraft_AndPending()
    {
        await SeedIterationAsync();
        var beforeUndo = await _service.LoadOrCreateAsync(null);
        var draftAfterSeed = beforeUndo.Body.CurrentDraft;
        var turnsAfterSeed = beforeUndo.Body.TurnCount;

        _ollama.QueueResponse(@"{""draft"":""mutated"",""question"":""q"",""options"":[{""label"":""o1""},{""label"":""o2""},{""label"":""o3""}]}");
        await _service.ApplyVerbAsync(null, "improve", "m");

        var row = await _service.UndoAsync(null);
        row.Body.CurrentDraft.Should().Be(draftAfterSeed);
        row.Body.TurnCount.Should().Be(turnsAfterSeed);
        row.Body.History.Should().HaveCount(turnsAfterSeed);
    }

    [Fact]
    public async Task UndoAsync_DoesNotCallLLM()
    {
        await SeedIterationAsync();
        _ollama.QueueResponse(@"{""draft"":""mutated"",""question"":""q"",""options"":[{""label"":""o1""},{""label"":""o2""},{""label"":""o3""}]}");
        await _service.ApplyVerbAsync(null, "improve", "m");
        var callsBefore = _ollama.CallCount;

        await _service.UndoAsync(null);

        _ollama.CallCount.Should().Be(callsBefore, "Undo must be deterministic with no LLM call");
    }

    [Fact]
    public async Task CommitAsync_SetsCommittedStage_AndPublishesEvent()
    {
        await SeedIterationAsync();
        WizardCommittedEventArgs? captured = null;
        _events.Subscribe<WizardCommittedEventArgs>(e => captured = e);

        var row = await _service.CommitAsync(null);
        row.Body.Stage.Should().Be(WizardStage.Committed);
        captured.Should().NotBeNull();
        captured!.Draft.Should().Be(row.Body.CurrentDraft);
    }

    [Fact]
    public async Task ResetAsync_WipesBody_PreservesRow_AndPublishesEvent()
    {
        await SeedIterationAsync();
        WizardResetEventArgs? captured = null;
        _events.Subscribe<WizardResetEventArgs>(e => captured = e);

        var row = await _service.ResetAsync(null);
        row.Body.Stage.Should().Be(WizardStage.Intro);
        row.Body.History.Should().BeEmpty();
        row.Body.CurrentDraft.Should().BeEmpty();
        captured.Should().NotBeNull();

        // Same row id - we did not delete it.
        var reload = await _service.LoadOrCreateAsync(null);
        reload.Id.Should().Be(row.Id);
    }

    [Fact]
    public async Task HardCap_PreventsAdditionalAdvances()
    {
        await SeedIterationAsync();
        using (var ctx = await _factory.CreateDbContextAsync())
        {
            var row = await ctx.WorkshopWizardSessions.FirstAsync();
            row.Body.TurnCount = WorkshopWizardService.HardCap;
            ctx.Entry(row).Property(e => e.Body).IsModified = true;
            await ctx.SaveChangesAsync();
        }

        _ollama.QueueResponse(@"{""draft"":""x"",""question"":""q"",""options"":[{""label"":""a""},{""label"":""b""},{""label"":""c""}]}");
        var act = () => _service.ApplyVerbAsync(null, "improve", "m");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void BuildContextWindow_ReturnsAtMostTwoTurns()
    {
        var body = new WizardBody();
        for (int i = 0; i < 5; i++)
            body.History.Add(new WizardTurn { Source = $"iterate", Choice = $"c{i}" });

        var window = WorkshopWizardServiceAccessor.WindowFor(body);
        window.Should().HaveCount(WorkshopWizardService.ContextWindowTurns);
        window.Last().Choice.Should().Be("c4");
    }

    // ---------------- helpers ----------------

    private async Task SeedIterationAsync()
    {
        await _service.LoadOrCreateAsync(null);
        _ollama.QueueResponse(@"{""question"":""q1"",""options"":[{""label"":""subA""},{""label"":""subB""},{""label"":""subC""}]}");
        await _service.AdvanceIntroAsync(null, new WizardOption { Label = "Subject A" }, "m");
        _ollama.QueueResponse(@"{""draft"":""Subject A, Scenery B"",""question"":""next?"",""options"":[{""label"":""opt1""},{""label"":""opt2""},{""label"":""opt3""}]}");
        await _service.AdvanceIntroAsync(null, new WizardOption { Label = "Scenery B" }, "m");
    }

    private const string SampleIntroJson = """
        {
          "version": 1,
          "sections": [
            { "id": "subject", "title": "Subject", "guidance": "pick a subject", "examples": ["a fox"] },
            { "id": "scenery", "title": "Scenery", "guidance": "pick a setting", "examples": ["a forest"] }
          ]
        }
        """;
}

internal static class WorkshopWizardServiceAccessor
{
    // Wraps the internal helper so the test stays decoupled from reflection plumbing.
    public static List<WizardTurn> WindowFor(WizardBody body)
    {
        var mi = typeof(WorkshopWizardService).GetMethod(
            "BuildContextWindow",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (List<WizardTurn>)mi!.Invoke(null, new object[] { body })!;
    }
}

internal sealed class TestableOllamaService : OllamaService
{
    private readonly Queue<string> _responses = new();

    public TestableOllamaService()
        : base(NullLogger<OllamaService>.Instance, new TestConfig(), new NoopProgress())
    {
    }

    public int CallCount { get; private set; }
    public string LastUserPrompt { get; private set; } = string.Empty;

    public void QueueResponse(string json) => _responses.Enqueue(json);

    public override Task<OllamaChatResponse?> SendChatMessage(
        string modelName,
        List<OllamaChatMessage> messages,
        OllamaOptions? options = null,
        string? keepAlive = "15m",
        bool stream = false,
        string? format = null)
    {
        CallCount++;
        LastUserPrompt = messages.LastOrDefault(m => m.Role == "user")?.Content ?? string.Empty;
        var content = _responses.Count > 0 ? _responses.Dequeue() : "{}";
        return Task.FromResult<OllamaChatResponse?>(new OllamaChatResponse
        {
            Message = new OllamaChatMessage { Role = "assistant", Content = content },
            Done = true,
        });
    }

    private sealed class TestConfig : Microsoft.Extensions.Configuration.IConfiguration
    {
        public string? this[string key] { get => null; set { } }
        public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() =>
            Array.Empty<Microsoft.Extensions.Configuration.IConfigurationSection>();
        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() =>
            throw new NotImplementedException();
        public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) =>
            throw new NotImplementedException();
    }

    private sealed class NoopProgress : IProgressService
    {
        public List<BaseProgress> Progresses { get; set; } = new();
        public int CurrentProgress { get; set; }
        public bool IsConverging { get; set; }
        public int QueueRemaining { get; set; }
        public event Action? OnUpdate { add { } remove { } }
        public void Add(BaseProgress progress) { }
        public void Update(Guid id, float value) { }
        public void Remove(Guid id) { }
        public void NotifyProgressChanged() { }
    }
}

internal sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>, IDisposable
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
