using BlazorWebApp.Models.CharacterCreator;
using BlazorWebApp.Services;
using FluentAssertions;

namespace BlazorWebApp.Tests.Services;

public class CharacterPromptCompilerServiceTests
{
    private readonly CharacterPromptCompilerService _compiler = new();

    [Fact]
    public void Compile_ShouldRenderIdentityWardrobeExpressionAndApplicationFragments()
    {
        var body = CreateCharacterBody();
        var catalog = CreateCatalog();

        var result = _compiler.Compile(body, catalog, new CharacterPromptCompileOptions
        {
            ApplicationContext = new CharacterPromptApplicationContext
            {
                Pose = "running pose",
                Camera = "low-angle camera",
                Scene = "dusk forest",
                EditIntent = "preserve identity while changing outfit"
            }
        });

        result.ProfileId.Should().Be("identity_plus_wardrobe");
        result.PositivePrompt.Should().Contain("Kira, a human scout, nimble pathfinder");
        result.PositivePrompt.Should().Contain("athletic build");
        result.PositivePrompt.Should().Contain("long silver hair");
        result.PositivePrompt.Should().Contain("emerald eyes");
        result.PositivePrompt.Should().Contain("weathered travel cloak");
        result.PositivePrompt.Should().Contain("cheek scar");
        result.PositivePrompt.Should().Contain("soft smile");
        result.PositivePrompt.Should().Contain("running pose, low-angle camera, dusk forest, preserve identity while changing outfit");
        result.Fragments.Should().Contain(fragment => fragment.Kind == CharacterPromptFragmentKind.Identity && fragment.Text.Contains("athletic build"));
        result.Fragments.Should().Contain(fragment => fragment.Kind == CharacterPromptFragmentKind.Wardrobe && fragment.Text.Contains("weathered travel cloak"));
        result.Fragments.Should().Contain(fragment => fragment.Kind == CharacterPromptFragmentKind.Expression && fragment.Text.Contains("soft smile"));
        result.Fragments.Should().Contain(fragment => fragment.Kind == CharacterPromptFragmentKind.Application && fragment.Text.Contains("dusk forest"));
    }

    [Fact]
    public void Compile_ShouldRespectConciseBudget()
    {
        var body = CreateCharacterBody();
        body.Regions["body"].SelectedTraits["body.height"] = new CharacterTraitValue { TraitId = "body.height", Value = "tall" };
        body.Regions["torso"] = CreateRegion("torso", ("torso.shape", "narrow waist"));
        body.Regions["arms"] = CreateRegion("arms", ("arms.shape", "toned arms"));
        body.Regions["hands"] = CreateRegion("hands", ("hands.shape", "slender hands"));
        body.Regions["legs"] = CreateRegion("legs", ("legs.shape", "long legs"));
        body.Regions["feet"] = CreateRegion("feet", ("feet.shape", "light step"));

        var result = _compiler.Compile(body, CreateCatalog(), new CharacterPromptCompileOptions
        {
            DetailBudgetOverride = CharacterPromptDetailBudget.Concise
        });

        result.DetailBudget.Should().Be(CharacterPromptDetailBudget.Concise);
        result.PositivePrompt.Should().Contain("athletic build");
        result.PositivePrompt.Should().Contain("long silver hair");
        result.PositivePrompt.Should().NotContain("light step");
    }

    [Fact]
    public void Compile_ShouldResolveConflictsByKeepingLockedTrait()
    {
        var body = CharacterBody.CreateDefault("Kira");
        body.Regions["hair"] = new CharacterRegionState
        {
            RegionId = "hair",
            SelectedTraits =
            {
                ["hair.length"] = new CharacterTraitValue { TraitId = "hair.length", Value = "short" },
                ["hair.alternate_length"] = new CharacterTraitValue { TraitId = "hair.alternate_length", Value = "long", Locked = true }
            }
        };
        var catalog = CreateCatalog();
        catalog.ConflictRules.Add(new CharacterConflictRuleDefinition
        {
            Id = "hair-length-exclusive",
            Items = ["short", "long"]
        });

        var result = _compiler.Compile(body, catalog);

        result.PositivePrompt.Should().Contain("long silver hair");
        result.PositivePrompt.Should().NotContain("short hair");
        result.Warnings.Should().Contain(warning => warning.Contains("hair-length-exclusive"));
    }

    [Fact]
    public void Compile_ShouldRenderNegativeGuardProfile()
    {
        var body = CreateCharacterBody();

        var result = _compiler.Compile(body, CreateCatalog(), new CharacterPromptCompileOptions
        {
            ProfileId = "negative_guard",
            ApplicationContext = new CharacterPromptApplicationContext
            {
                AdditionalNegative = "duplicate character"
            }
        });

        result.PositivePrompt.Should().BeEmpty();
        result.NegativePrompt.Should().Contain("inconsistent identity");
        result.NegativePrompt.Should().Contain("different face");
        result.NegativePrompt.Should().Contain("duplicate character");
        result.Fragments.Should().Contain(fragment => fragment.Kind == CharacterPromptFragmentKind.NegativeGuard);
    }

    [Fact]
    public void ComposePrompt_ShouldSupportReplacePrependAndAppend()
    {
        _compiler.ComposePrompt("a castle", "Kira, long silver hair", CharacterPromptCompositionMode.Replace)
            .Should().Be("Kira, long silver hair");
        _compiler.ComposePrompt("a castle", "Kira, long silver hair", CharacterPromptCompositionMode.Prepend)
            .Should().Be("Kira, long silver hair, a castle");
        _compiler.ComposePrompt("a castle", "Kira, long silver hair", CharacterPromptCompositionMode.Append)
            .Should().Be("a castle, Kira, long silver hair");
    }

    private static CharacterBody CreateCharacterBody()
    {
        var body = CharacterBody.CreateDefault("Kira");
        body.Identity.Species = "human";
        body.Identity.Archetype = "scout";
        body.Identity.Summary = "nimble pathfinder";
        body.Regions["body"] = CreateRegion("body", ("body.build", "athletic"));
        body.Regions["hair"] = CreateRegion("hair", ("hair.length", "long"));
        body.Regions["eyes"] = CreateRegion("eyes", ("eyes.color", "emerald eyes"));
        body.Regions["marks"] = CreateRegion("marks", ("marks.scar", "cheek scar"));
        body.Regions["expression"] = CreateRegion("expression", ("expression.default", "soft_smile"));
        body.Wardrobes.Add(new CharacterWardrobePreset
        {
            Id = "travel",
            Label = "Travel",
            TraitValues =
            [
                new CharacterTraitAssignment { RegionId = "clothing", TraitId = "clothing.outer", Value = "weathered travel cloak" }
            ]
        });

        return body;
    }

    private static CharacterRegionState CreateRegion(string regionId, params (string TraitId, string Value)[] traits)
    {
        var region = new CharacterRegionState { RegionId = regionId };
        foreach (var trait in traits)
        {
            region.SelectedTraits[trait.TraitId] = new CharacterTraitValue
            {
                TraitId = trait.TraitId,
                Value = trait.Value
            };
        }

        return region;
    }

    private static CharacterCreatorCatalog CreateCatalog()
    {
        return new CharacterCreatorCatalog
        {
            Regions =
            [
                new CharacterRegionDefinition { Id = "body", Label = "Body", SortOrder = 10, PromptPriority = 10 },
                new CharacterRegionDefinition { Id = "torso", Label = "Torso", SortOrder = 20, PromptPriority = 20 },
                new CharacterRegionDefinition { Id = "arms", Label = "Arms", SortOrder = 30, PromptPriority = 30 },
                new CharacterRegionDefinition { Id = "hands", Label = "Hands", SortOrder = 40, PromptPriority = 40 },
                new CharacterRegionDefinition { Id = "legs", Label = "Legs", SortOrder = 50, PromptPriority = 50 },
                new CharacterRegionDefinition { Id = "feet", Label = "Feet", SortOrder = 60, PromptPriority = 60 },
                new CharacterRegionDefinition { Id = "hair", Label = "Hair", ParentId = "head", SortOrder = 70, PromptPriority = 15 },
                new CharacterRegionDefinition { Id = "eyes", Label = "Eyes", ParentId = "head", SortOrder = 80, PromptPriority = 16 },
                new CharacterRegionDefinition { Id = "marks", Label = "Marks", SortOrder = 90, PromptPriority = 90 },
                new CharacterRegionDefinition { Id = "expression", Label = "Expression", Group = "expression", SortOrder = 100, PromptPriority = 100 },
                new CharacterRegionDefinition { Id = "clothing", Label = "Clothing", Group = "wardrobe", SortOrder = 110, PromptPriority = 110 }
            ],
            TraitDefinitions =
            [
                new CharacterTraitDefinition
                {
                    Id = "body.build",
                    RegionId = "body",
                    Label = "Build",
                    Options = [new CharacterTraitOptionDefinition { Id = "athletic", Label = "Athletic", Prompt = "athletic build" }]
                },
                new CharacterTraitDefinition { Id = "body.height", RegionId = "body", Label = "Height" },
                new CharacterTraitDefinition { Id = "torso.shape", RegionId = "torso", Label = "Torso" },
                new CharacterTraitDefinition { Id = "arms.shape", RegionId = "arms", Label = "Arms" },
                new CharacterTraitDefinition { Id = "hands.shape", RegionId = "hands", Label = "Hands" },
                new CharacterTraitDefinition { Id = "legs.shape", RegionId = "legs", Label = "Legs" },
                new CharacterTraitDefinition { Id = "feet.shape", RegionId = "feet", Label = "Feet" },
                CreateHairLengthDefinition("hair.length"),
                CreateHairLengthDefinition("hair.alternate_length"),
                new CharacterTraitDefinition { Id = "eyes.color", RegionId = "eyes", Label = "Eye Color" },
                new CharacterTraitDefinition { Id = "marks.scar", RegionId = "marks", Label = "Scar" },
                new CharacterTraitDefinition
                {
                    Id = "expression.default",
                    RegionId = "expression",
                    Label = "Default Expression",
                    Scope = "expression",
                    Options = [new CharacterTraitOptionDefinition { Id = "soft_smile", Label = "Soft Smile", Prompt = "soft smile" }]
                },
                new CharacterTraitDefinition { Id = "clothing.outer", RegionId = "clothing", Label = "Outerwear", Scope = "wardrobe" }
            ],
            PromptRules =
            [
                new CharacterPromptRuleDefinition
                {
                    ProfileId = "identity_plus_wardrobe",
                    Label = "Identity + Wardrobe",
                    DetailBudget = "balanced",
                    RegionOrder = ["identity", "body", "head", "wardrobe", "marks", "expression", "application"],
                    Template = "{identity}. {body}. {head}. {wardrobe}. {marks}. {expression}. {application}",
                    NegativeTemplate = "{negative_guard}"
                }
            ]
        };
    }

    private static CharacterTraitDefinition CreateHairLengthDefinition(string id)
    {
        return new CharacterTraitDefinition
        {
            Id = id,
            RegionId = "hair",
            Label = "Hair Length",
            Options =
            [
                new CharacterTraitOptionDefinition { Id = "short", Label = "Short", Prompt = "short hair" },
                new CharacterTraitOptionDefinition { Id = "long", Label = "Long", Prompt = "long silver hair" }
            ]
        };
    }
}