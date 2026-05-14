using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;

namespace BlazorWebApp.Tests.Services;

public class CharacterReferenceSlotPlannerTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "character-slot-planner-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ResolveRunSlots_ShouldIncludeFrontViewBeforeDependentBodySlot()
    {
        var state = new AppStateCharacter();

        var result = CharacterReferenceSlotPlanner.ResolveRunSlots(
            state,
            ["left-profile"],
            includeDependencies: true);

        result.Select(slot => slot.Id).Should().Equal("front-view", "left-profile");
    }

    [Fact]
    public void ResolveRunSlots_ShouldIncludeNeutralBeforeDependentExpressionSlot()
    {
        var state = new AppStateCharacter();

        var result = CharacterReferenceSlotPlanner.ResolveRunSlots(
            state,
            ["happy"],
            includeDependencies: true);

        result.Select(slot => slot.Id).Should().Equal("neutral", "happy");
    }

    [Fact]
    public void ResolveRunSlots_WhenDependencyOutputExists_ShouldReuseItWithoutAddingDependencySlot()
    {
        Directory.CreateDirectory(_tempDirectory);
        var state = new AppStateCharacter();
        var frontOutputPath = Path.Combine(_tempDirectory, "front.png");
        File.WriteAllText(frontOutputPath, "front image");
        state.Slots.First(slot => slot.Id == "front-view").LastOutputPath = frontOutputPath;

        var result = CharacterReferenceSlotPlanner.ResolveRunSlots(
            state,
            ["left-profile"],
            includeDependencies: true);

        result.Select(slot => slot.Id).Should().Equal("left-profile");
    }

    [Fact]
    public void ResolveRunSlots_WhenDependenciesDisabled_ShouldReturnOnlyRequestedSlots()
    {
        var state = new AppStateCharacter();

        var result = CharacterReferenceSlotPlanner.ResolveRunSlots(
            state,
            ["left-profile"],
            includeDependencies: false);

        result.Select(slot => slot.Id).Should().Equal("left-profile");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}