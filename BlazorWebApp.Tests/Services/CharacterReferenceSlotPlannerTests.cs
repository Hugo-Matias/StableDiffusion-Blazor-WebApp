using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;

namespace BlazorWebApp.Tests.Services;

public class CharacterReferenceSlotPlannerTests
{
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
    public void ResolveRunSlots_WhenDependenciesDisabled_ShouldReturnOnlyRequestedSlots()
    {
        var state = new AppStateCharacter();

        var result = CharacterReferenceSlotPlanner.ResolveRunSlots(
            state,
            ["left-profile"],
            includeDependencies: false);

        result.Select(slot => slot.Id).Should().Equal("left-profile");
    }
}