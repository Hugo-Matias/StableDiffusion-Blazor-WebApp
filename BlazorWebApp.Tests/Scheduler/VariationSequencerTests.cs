using BlazorWebApp.Scheduler.Engine;
using BlazorWebApp.Scheduler.Models;
using BlazorWebApp.Scheduler.Targets;
using BlazorWebApp.Scheduler.Variations;
using FluentAssertions;
using Moq;

namespace BlazorWebApp.Tests.Scheduler;

public class VariationSequencerTests
{
    private static ParameterTarget T() => new FragmentTarget { FragmentId = "f", ParamKey = "p" };

    private static Mock<IVariationMaterializer> Materializer(Dictionary<Variation, IReadOnlyList<object?>> map)
    {
        var mock = new Mock<IVariationMaterializer>();
        mock.Setup(m => m.MaterializeAsync(It.IsAny<Variation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Variation v, CancellationToken _) => map[v]);
        return mock;
    }

    [Fact]
    public async Task NoVariations_ProducesSingleEmptySet()
    {
        var action = new JobAction { Order = 0 };
        var mock = Materializer(new());
        var seq = new VariationSequencer(mock.Object);

        var plan = await seq.BuildPlanAsync(action);
        plan.TotalCount.Should().Be(1);
        plan.EffectiveCount.Should().Be(1);

        var sets = seq.Enumerate(plan).ToList();
        sets.Should().HaveCount(1);
        sets[0].Values.Should().BeEmpty();
        sets[0].Index.Should().Be(0);
    }

    [Fact]
    public async Task TwoVariations_ProducesCartesianProduct()
    {
        var v1 = new ListVariation { Target = T(), Values = new() { 1, 2, 3 } };
        var v2 = new ListVariation { Target = T(), Values = new() { "a", "b" } };
        var action = new JobAction { Order = 0 };
        action.Variations.Add(v1);
        action.Variations.Add(v2);

        var mock = Materializer(new()
        {
            [v1] = new object?[] { 1, 2, 3 },
            [v2] = new object?[] { "a", "b" }
        });
        var seq = new VariationSequencer(mock.Object);

        var plan = await seq.BuildPlanAsync(action);
        plan.TotalCount.Should().Be(6);
        plan.EffectiveCount.Should().Be(6);
        plan.WasTruncated.Should().BeFalse();

        var sets = seq.Enumerate(plan).ToList();
        sets.Should().HaveCount(6);

        // Sequential order: leftmost variation is outer loop.
        // (1,a),(1,b),(2,a),(2,b),(3,a),(3,b)
        sets[0].Values.Select(iv => iv.Value).Should().Equal(1, "a");
        sets[1].Values.Select(iv => iv.Value).Should().Equal(1, "b");
        sets[2].Values.Select(iv => iv.Value).Should().Equal(2, "a");
        sets[5].Values.Select(iv => iv.Value).Should().Equal(3, "b");
    }

    [Fact]
    public async Task Limit_TruncatesSequential()
    {
        var v1 = new ListVariation { Target = T(), Values = new() { 1, 2, 3 } };
        var v2 = new ListVariation { Target = T(), Values = new() { "a", "b" } };
        var action = new JobAction { Order = 0, Limit = 4 };
        action.Variations.Add(v1);
        action.Variations.Add(v2);

        var mock = Materializer(new()
        {
            [v1] = new object?[] { 1, 2, 3 },
            [v2] = new object?[] { "a", "b" }
        });
        var seq = new VariationSequencer(mock.Object);

        var plan = await seq.BuildPlanAsync(action);
        plan.TotalCount.Should().Be(6);
        plan.EffectiveCount.Should().Be(4);
        plan.WasTruncated.Should().BeTrue();

        var sets = seq.Enumerate(plan).ToList();
        sets.Should().HaveCount(4);
        sets.Last().Values.Select(iv => iv.Value).Should().Equal(2, "b");
    }

    [Fact]
    public async Task Limit_AboveTotal_UsesFullProduct()
    {
        var v = new ListVariation { Target = T(), Values = new() { 1, 2 } };
        var action = new JobAction { Order = 0, Limit = 99 };
        action.Variations.Add(v);

        var mock = Materializer(new() { [v] = new object?[] { 1, 2 } });
        var seq = new VariationSequencer(mock.Object);

        var plan = await seq.BuildPlanAsync(action);
        plan.EffectiveCount.Should().Be(2);
        plan.WasTruncated.Should().BeFalse();
    }

    [Fact]
    public async Task Random_SameSeed_IsReproducible()
    {
        var v1 = new ListVariation { Target = T(), Values = new() { 1, 2, 3 } };
        var v2 = new ListVariation { Target = T(), Values = new() { "a", "b" } };

        IReadOnlyList<object?> Run(JobAction a)
        {
            var mock = Materializer(new()
            {
                [(ListVariation)a.Variations[0]] = new object?[] { 1, 2, 3 },
                [(ListVariation)a.Variations[1]] = new object?[] { "a", "b" }
            });
            var seq = new VariationSequencer(mock.Object);
            var plan = seq.BuildPlanAsync(a).GetAwaiter().GetResult();
            return seq.Enumerate(plan).Select(s => (object?)string.Join(",", s.Values.Select(iv => iv.Value))).ToList();
        }

        var a1 = new JobAction { Order = 0, PermutationOrder = PermutationOrder.Random, RandomPermutationSeed = 7 };
        a1.Variations.Add(v1); a1.Variations.Add(v2);
        var a2 = new JobAction { Order = 0, PermutationOrder = PermutationOrder.Random, RandomPermutationSeed = 7 };
        a2.Variations.Add(v1); a2.Variations.Add(v2);

        Run(a1).Should().Equal(Run(a2));
    }

    [Fact]
    public async Task Random_WithLimit_TrimsToEffective()
    {
        var v1 = new ListVariation { Target = T(), Values = new() { 1, 2, 3 } };
        var v2 = new ListVariation { Target = T(), Values = new() { "a", "b" } };
        var action = new JobAction
        {
            Order = 0,
            Limit = 3,
            PermutationOrder = PermutationOrder.Random,
            RandomPermutationSeed = 12
        };
        action.Variations.Add(v1);
        action.Variations.Add(v2);

        var mock = Materializer(new()
        {
            [v1] = new object?[] { 1, 2, 3 },
            [v2] = new object?[] { "a", "b" }
        });
        var seq = new VariationSequencer(mock.Object);

        var plan = await seq.BuildPlanAsync(action);
        var sets = seq.Enumerate(plan).ToList();

        sets.Should().HaveCount(3);
        // All 6 combinations should be unique among the 3 drawn (no replacement).
        sets.Select(s => string.Join(",", s.Values.Select(iv => iv.Value)))
            .Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task EmptyMaterializedValues_SubstitutedWithNullSlot()
    {
        // A wildcard with zero entries still contributes one "null" slot so the plan has non-zero total.
        var wildcard = new WildcardVariation { Target = new PromptTarget(), CollectionName = "empty" };
        var other = new ListVariation { Target = T(), Values = new() { 1, 2 } };
        var action = new JobAction { Order = 0 };
        action.Variations.Add(wildcard);
        action.Variations.Add(other);

        var mock = Materializer(new()
        {
            [wildcard] = Array.Empty<object?>(),
            [other] = new object?[] { 1, 2 }
        });
        var seq = new VariationSequencer(mock.Object);

        var plan = await seq.BuildPlanAsync(action);
        plan.TotalCount.Should().Be(2); // 1 * 2
        plan.Values[0].Should().Equal(new object?[] { null });

        var sets = seq.Enumerate(plan).ToList();
        sets.Should().HaveCount(2);
        sets[0].Values[0].Value.Should().BeNull();
    }
}
