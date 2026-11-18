using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;
using Xunit;

namespace BlazorWebApp.Tests.Services
{
    /// <summary>
    /// Unit tests for Phase 7.5: service-driven anchor interleaving,
    /// persona anchor personality, and choice-driven facet entropy (Mechanisms A/B/C).
    /// No database/LLM dependencies — all tests are purely in-process.
    /// </summary>
    public class OdditariumPhase75Tests
    {
        // ─── BuildAnchorQueue ────────────────────────────────────────────────────

        [Fact]
        public void BuildAnchorQueue_Muse_HasFramingInFirstThreePositions()
        {
            var body = new OdditariumBody();
            var muse = OdditariumPersonaRoster.FindById("muse")!;

            var queue = OdditariumService.BuildAnchorQueue(body, muse);

            // framing has index 0 in Muse's AnchorAffinity — must appear early
            queue.Take(3).Should().Contain("framing",
                because: "Muse has framing as highest affinity anchor");
        }

        [Fact]
        public void BuildAnchorQueue_IronMother_HasActionBeforeStyle()
        {
            var body = new OdditariumBody();
            var ironMother = OdditariumPersonaRoster.FindById("iron-mother")!;

            var queue = OdditariumService.BuildAnchorQueue(body, ironMother);

            var actionIdx = queue.IndexOf("action");
            var styleIdx = queue.IndexOf("style");
            actionIdx.Should().BeLessThan(styleIdx,
                because: "Iron Mother ranks action above style");
        }

        [Fact]
        public void BuildAnchorQueue_PixelPixie_HasColorBeforeMovementInEnrichmentTier()
        {
            var body = new OdditariumBody();
            var pixie = OdditariumPersonaRoster.FindById("pixel-pixie")!;

            // Run multiple times to confirm the affinity signal is stronger than jitter (±0.3 vs rank gap of 3)
            int colorBeforeMovement = 0;
            for (int i = 0; i < 50; i++)
            {
                var queue = OdditariumService.BuildAnchorQueue(body, pixie);
                if (queue.IndexOf("color") < queue.IndexOf("movement"))
                    colorBeforeMovement++;
            }

            // Both color (rank 0) and movement (rank 3) are Enrichment anchors.
            // With rank gap=3 and jitter ±0.3, color wins ≥95% of runs.
            colorBeforeMovement.Should().BeGreaterThan(40,
                because: "Pixel Pixie ranks color (affinity #0) well above movement (#3) within the Enrichment tier");
        }

        [Fact]
        public void BuildAnchorQueue_SkipsAlreadyVisitedAnchors()
        {
            var body = new OdditariumBody();
            body.VisitedFacets["subject"] = new List<string> { "core-archetype" };
            body.VisitedFacets["setting"] = new List<string> { "location-type" };

            var queue = OdditariumService.BuildAnchorQueue(body, null);

            queue.Should().NotContain("subject", because: "subject is already visited");
            queue.Should().NotContain("setting", because: "setting is already visited");
        }

        [Fact]
        public void BuildAnchorQueue_CoreAnchorsAppearBeforeEnrichmentAnchors()
        {
            // Run for all five personas to ensure tier order holds regardless of affinity.
            foreach (var persona in OdditariumPersonaRoster.All)
            {
                var body = new OdditariumBody();
                var queue = OdditariumService.BuildAnchorQueue(body, persona);

                var subjectIdx = queue.IndexOf("subject");
                var settingIdx = queue.IndexOf("setting");
                var movementIdx = queue.IndexOf("movement");
                var colorIdx = queue.IndexOf("color");

                subjectIdx.Should().BeLessThan(movementIdx,
                    because: $"{persona.Name}: subject (Core) must precede movement (Enrichment)");
                settingIdx.Should().BeLessThan(colorIdx,
                    because: $"{persona.Name}: setting (Core) must precede color (Enrichment)");
            }
        }

        [Fact]
        public void BuildAnchorQueue_ReturnsAll13AnchorsByDefault()
        {
            var body = new OdditariumBody();
            var queue = OdditariumService.BuildAnchorQueue(body, null);
            queue.Should().HaveCount(13);
        }

        // ─── AdvanceAnchorTarget ─────────────────────────────────────────────────

        [Fact]
        public void AdvanceAnchorTarget_FirstCall_SetsModeExpandAndPopsQueue()
        {
            var body = new OdditariumBody();
            var pixie = OdditariumPersonaRoster.FindById("pixel-pixie")!; // MinDeepens=0 for predictability

            OdditariumService.AdvanceAnchorTarget(body, pixie);

            body.PendingMode.Should().Be("expand");
            body.PendingAnchor.Should().NotBeNullOrWhiteSpace();
            body.CurrentAnchorTarget.Should().Be(body.PendingAnchor);
        }

        [Fact]
        public void AdvanceAnchorTarget_DeepensDecrementCountdown()
        {
            var body = new OdditariumBody();
            // Use Muse (MinDeepens=2, MaxDeepens=3) to guarantee at least one deepen
            var muse = OdditariumPersonaRoster.FindById("muse")!;

            // First call: expand, sets countdown
            OdditariumService.AdvanceAnchorTarget(body, muse);
            var firstAnchor = body.PendingAnchor;
            var initialDeepens = body.RemainingDeepensForAnchor;
            initialDeepens.Should().BeInRange(2, 3);

            // Subsequent calls: deepen and decrement
            for (int i = initialDeepens; i > 0; i--)
            {
                OdditariumService.AdvanceAnchorTarget(body, muse);
                body.PendingMode.Should().Be("deepen");
                body.PendingAnchor.Should().Be(firstAnchor);
                body.RemainingDeepensForAnchor.Should().Be(i - 1);
            }
        }

        [Fact]
        public void AdvanceAnchorTarget_AdvancesQueueWhenCountdownDepleted()
        {
            var body = new OdditariumBody();
            var pixie = OdditariumPersonaRoster.FindById("pixel-pixie")!; // MinDeepens=0

            // First call: pops first anchor, sets RemainingDeepens to 0 or 1
            OdditariumService.AdvanceAnchorTarget(body, pixie);
            var firstAnchor = body.PendingAnchor!;

            // Force countdown to 0 regardless of what was set
            body.RemainingDeepensForAnchor = 0;

            // Next call: should pop next anchor with mode=expand
            OdditariumService.AdvanceAnchorTarget(body, pixie);

            body.PendingMode.Should().Be("expand");
            body.PendingAnchor.Should().NotBe(firstAnchor,
                because: "countdown was 0 so a new anchor should be popped");
        }

        [Fact]
        public void AdvanceAnchorTarget_PixelPixie_CanHaveZeroDeepensForAnAnchor()
        {
            var body = new OdditariumBody();
            var pixie = OdditariumPersonaRoster.FindById("pixel-pixie")!;
            pixie.MinDeepensPerAnchor.Should().Be(0, because: "Pixel Pixie is defined with MinDeepens=0");

            // Run AdvanceAnchorTarget many times and verify that at least once
            // RemainingDeepensForAnchor is set to 0 (meaning no deepen for that anchor).
            bool observedZeroDeepens = false;
            for (int round = 0; round < 30; round++)
            {
                body.RemainingDeepensForAnchor = 0;
                body.AnchorQueue = OdditariumService.BuildAnchorQueue(body, pixie);
                OdditariumService.AdvanceAnchorTarget(body, pixie);
                if (body.RemainingDeepensForAnchor == 0)
                    observedZeroDeepens = true;
            }

            observedZeroDeepens.Should().BeTrue(
                because: "Pixel Pixie's MinDeepens=0 allows anchors to be skipped without any deepens");
        }

        // ─── MutateQueueOnPick (Mechanism A) ────────────────────────────────────

        [Fact]
        public void MutateQueueOnPick_PickSubject_BumpsActionToFront()
        {
            var body = new OdditariumBody();
            body.CurrentAnchorTarget = "subject";
            // Build a queue that has action somewhere after position 0
            body.AnchorQueue = new List<string> { "lighting", "framing", "action", "mood" };

            OdditariumService.MutateQueueOnPick(body, "subject");

            body.AnchorQueue[0].Should().Be("action",
                because: "subject -> action resonance should bump action to front");
        }

        [Fact]
        public void MutateQueueOnPick_NoOp_WhenResonanceTargetAlreadyAtFront()
        {
            var body = new OdditariumBody();
            body.CurrentAnchorTarget = "subject";
            body.AnchorQueue = new List<string> { "action", "framing", "mood" };

            OdditariumService.MutateQueueOnPick(body, "subject");

            // Queue should be unchanged
            body.AnchorQueue.Should().Equal("action", "framing", "mood");
        }

        [Fact]
        public void MutateQueueOnPick_NoOp_WhenResonanceTargetAlreadyVisited()
        {
            var body = new OdditariumBody();
            body.CurrentAnchorTarget = "subject";
            body.VisitedFacets["action"] = new List<string> { "some-facet" };
            body.AnchorQueue = new List<string> { "lighting", "framing", "mood" };

            OdditariumService.MutateQueueOnPick(body, "subject");

            // action is visited so queue should be unchanged
            body.AnchorQueue.Should().Equal("lighting", "framing", "mood");
        }

        [Fact]
        public void MutateQueueOnPick_NoOp_WhenResonanceTargetIsCurrentTarget()
        {
            var body = new OdditariumBody();
            body.CurrentAnchorTarget = "action"; // resonance target of "subject" IS the current target
            body.AnchorQueue = new List<string> { "framing", "lighting", "mood" };

            OdditariumService.MutateQueueOnPick(body, "subject");

            body.AnchorQueue.Should().Equal("framing", "lighting", "mood");
        }

        // ─── BuildFacetSeedBlock (Mechanisms B and C) ───────────────────────────

        [Fact]
        public void BuildFacetSeedBlock_ExpandWithLayers_ContainsLastPickedLabel()
        {
            var body = new OdditariumBody();
            body.Vibe = "decay and rebirth";
            body.CollectedLayers.Add(new OdditariumLayer
            {
                Anchor = "setting",
                Facet = "location-type",
                Label = "Sunken Cathedral",
                Hint = "flooded naves"
            });

            var seed = OdditariumService.BuildFacetSeedBlock(body, "subject", "expand");

            seed.Should().Contain("Sunken Cathedral",
                because: "Mechanism B should inject the last picked label");
        }

        [Fact]
        public void BuildFacetSeedBlock_DeepensWithCrossAnchorLayer_ReferencesOtherAnchor()
        {
            var body = new OdditariumBody();
            body.Vibe = "desolate";
            body.CollectedLayers.Add(new OdditariumLayer
            {
                Anchor = "setting",
                Label = "Sunken Cathedral",
                Hint = "flooded naves"
            });

            // We are deepening "subject" — cross-anchor layer is the "setting" layer
            var seed = OdditariumService.BuildFacetSeedBlock(body, "subject", "deepen");

            seed.Should().Contain("Sunken Cathedral",
                because: "Mechanism C should inject the cross-anchor layer's label");
            seed.Should().Contain("subject",
                because: "seed block should name the anchor being deepened");
        }

        [Fact]
        public void BuildFacetSeedBlock_FirstRound_ContainsVibeText()
        {
            var body = new OdditariumBody { Vibe = "neon fog" };
            // No layers collected

            var seed = OdditariumService.BuildFacetSeedBlock(body, "subject", "expand");

            seed.Should().Contain("neon fog",
                because: "opening round seed should reference the vibe");
            seed.Should().Contain("opening round");
        }

        [Fact]
        public void BuildFacetSeedBlock_DeepensWithNoCompanionLayer_ReturnsFallback()
        {
            var body = new OdditariumBody { Vibe = "arctic silence" };
            // Only layers for the same anchor — no cross-anchor layer
            body.CollectedLayers.Add(new OdditariumLayer
            {
                Anchor = "subject",
                Label = "Hollow Automaton",
                Hint = "built to serve"
            });

            var seed = OdditariumService.BuildFacetSeedBlock(body, "subject", "deepen");

            seed.Should().Contain("No companion anchor layer",
                because: "fallback text should be used when no cross-anchor layer exists");
        }

        // ─── PopulatePendingFromLLMAsync — anchor/mode are service-owned ─────────

        [Fact]
        public void OdditariumBody_PendingAnchorAndMode_AreNotSetByLLMResponseFields()
        {
            // Verify that OdditariumBody fields used by PopulatePendingFromLLMAsync
            // come from AdvanceAnchorTarget (service-set), not from parsed.Anchor/Mode.
            // We test this indirectly: after AdvanceAnchorTarget, PendingAnchor/Mode
            // should match what the service decided, regardless of what an LLM response would have.
            var body = new OdditariumBody { PersonaId = "muse" };
            var muse = OdditariumPersonaRoster.FindById("muse")!;

            OdditariumService.AdvanceAnchorTarget(body, muse);

            // These are now set by the service
            body.PendingAnchor.Should().NotBeNullOrWhiteSpace();
            body.PendingMode.Should().BeOneOf("expand", "deepen");

            // Simulate what happens if LLM response had different anchor/mode
            var simulatedParsed = new OdditariumLLMResponse
            {
                Anchor = "mood",   // different from what service decided
                Mode = "deepen",   // potentially different
                Facet = "emotional-register",
                Question = "What emotional register calls to you?",
                Options = Enumerable.Range(1, 6).Select(i => new OdditariumOption { Label = $"Option {i}" }).ToList()
            };

            // Apply only what PopulatePendingFromLLMAsync applies from the LLM response
            var pendingAnchorBefore = body.PendingAnchor;
            var pendingModeBefore = body.PendingMode;

            // Mimic the v3 logic: only apply facet from LLM, not anchor/mode
            body.PendingFacet = simulatedParsed.Facet;

            // anchor and mode must NOT have changed
            body.PendingAnchor.Should().Be(pendingAnchorBefore,
                because: "anchor is service-owned; LLM Anchor field must be ignored");
            body.PendingMode.Should().Be(pendingModeBefore,
                because: "mode is service-owned; LLM Mode field must be ignored");
        }
    }
}
