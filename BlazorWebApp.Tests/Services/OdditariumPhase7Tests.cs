using BlazorWebApp.Models;
using BlazorWebApp.Services;
using FluentAssertions;
using Xunit;

namespace BlazorWebApp.Tests.Services
{
    /// <summary>
    /// Unit tests for Phase 7 Odditarium models, anchors, and validator.
    /// No database/LLM dependencies — all tests are purely in-process.
    /// </summary>
    public class OdditariumPhase7Tests
    {
        // ─── OdditariumAnchors ───────────────────────────────────────────────────

        [Fact]
        public void Anchors_All_HasExactly13Entries()
        {
            OdditariumAnchors.All.Should().HaveCount(13);
        }

        [Fact]
        public void Anchors_Core_HasSubjectAndSetting()
        {
            OdditariumAnchors.Core.Should().Equal("subject", "setting");
        }

        [Fact]
        public void Anchors_Structural_HasFourEntries()
        {
            OdditariumAnchors.Structural.Should().Equal("action", "lighting", "framing", "atmosphere");
        }

        [Fact]
        public void Anchors_Enrichment_HasSevenEntries()
        {
            OdditariumAnchors.Enrichment.Should().HaveCount(7);
            OdditariumAnchors.Enrichment.Should().Contain("mood", "style", "detail", "time", "scale", "color", "movement");
        }

        [Fact]
        public void Anchors_Round1Eligible_DoesNotContainEnrichment()
        {
            var enrichmentSet = new HashSet<string>(OdditariumAnchors.Enrichment, StringComparer.OrdinalIgnoreCase);
            OdditariumAnchors.Round1Eligible.Should().NotContain(a => enrichmentSet.Contains(a));
        }

        [Fact]
        public void Anchors_Round1Eligible_ContainsAllCoreAndStructural()
        {
            var r1 = new HashSet<string>(OdditariumAnchors.Round1Eligible, StringComparer.OrdinalIgnoreCase);
            foreach (var a in OdditariumAnchors.Core.Concat(OdditariumAnchors.Structural))
                r1.Should().Contain(a);
        }

        [Fact]
        public void Anchors_DebtTracked_EqualsRound1Eligible()
        {
            OdditariumAnchors.DebtTracked.Should().BeEquivalentTo(OdditariumAnchors.Round1Eligible);
        }

        [Fact]
        public void Anchors_IsKnown_ReturnsTrueForAllKnownAnchors()
        {
            foreach (var a in OdditariumAnchors.All)
                OdditariumAnchors.IsKnown(a).Should().BeTrue(because: $"'{a}' is in All");
        }

        [Fact]
        public void Anchors_IsKnown_ReturnsFalseForUnknown()
        {
            OdditariumAnchors.IsKnown("galaxy-brain").Should().BeFalse();
            OdditariumAnchors.IsKnown("").Should().BeFalse();
        }

        // ─── OdditariumResponseValidator.Sanitize ───────────────────────────────

        [Fact]
        public void Sanitize_NormalizesMode_ExpandDeepPivotPassThrough()
        {
            foreach (var mode in new[] { "expand", "deepen", "pivot" })
            {
                var result = OdditariumResponseValidator.Sanitize(new OdditariumLLMResponse { Mode = mode });
                result.Mode.Should().Be(mode, because: $"'{mode}' is a valid mode");
            }
        }

        [Fact]
        public void Sanitize_NullsUnrecognizedMode()
        {
            var result = OdditariumResponseValidator.Sanitize(new OdditariumLLMResponse { Mode = "vibrate" });
            result.Mode.Should().BeNull();
        }

        [Fact]
        public void Sanitize_CoercesUnknownAnchorToDetail()
        {
            var result = OdditariumResponseValidator.Sanitize(new OdditariumLLMResponse { Anchor = "galaxy-brain" });
            result.Anchor.Should().Be("detail");
        }

        [Fact]
        public void Sanitize_LeavesKnownAnchorUnchanged()
        {
            var result = OdditariumResponseValidator.Sanitize(new OdditariumLLMResponse { Anchor = "Subject" });
            result.Anchor.Should().Be("subject"); // lowercased
        }

        [Fact]
        public void Sanitize_KebabNormalizesFacet()
        {
            var result = OdditariumResponseValidator.Sanitize(new OdditariumLLMResponse { Facet = "Clothing And Armor!" });
            result.Facet.Should().Be("clothing-and-armor");
        }

        [Fact]
        public void Sanitize_DropsDuplicateOptionsByLabel()
        {
            var raw = new OdditariumLLMResponse
            {
                Question = "Pick one",
                Options = Enumerable.Repeat(new OdditariumOption { Label = "Figure", Hint = "x" }, 6).ToList()
            };
            var result = OdditariumResponseValidator.Sanitize(raw);
            result.Options.Should().HaveCount(1);
        }

        [Fact]
        public void Sanitize_ClampsTruncatesOptionsAt6()
        {
            var raw = new OdditariumLLMResponse
            {
                Question = "Pick one",
                Options = Enumerable.Range(1, 9)
                    .Select(i => new OdditariumOption { Label = $"Opt{i}" })
                    .ToList()
            };
            var result = OdditariumResponseValidator.Sanitize(raw);
            result.Options.Should().HaveCount(OdditariumResponseValidator.MaxOptions);
        }

        [Fact]
        public void Sanitize_PropagatesAnchorAndFacetToEveryOption()
        {
            var raw = new OdditariumLLMResponse
            {
                Anchor = "subject",
                Facet = "core archetype",
                Question = "What is it?",
                Options = new List<OdditariumOption>
                {
                    new() { Label = "Figure" },
                    new() { Label = "Creature" },
                    new() { Label = "Vehicle" },
                }
            };
            var result = OdditariumResponseValidator.Sanitize(raw);
            result.Options.Should().AllSatisfy(o =>
            {
                o.Anchor.Should().Be("subject");
                o.Facet.Should().Be("core-archetype"); // space-to-dash normalized
            });
        }

        [Fact]
        public void Sanitize_NormalizesExpansionsKeysToLowercase()
        {
            var raw = new OdditariumLLMResponse
            {
                Expansions = new Dictionary<string, string>
                {
                    ["Subject"] = "A tall figure in plate armor",
                    ["SETTING"] = "A misty mountain pass",
                }
            };
            var result = OdditariumResponseValidator.Sanitize(raw);
            result.Expansions.Should().ContainKey("subject");
            result.Expansions.Should().ContainKey("setting");
        }

        // ─── OdditariumResponseValidator.TryParse ──────────────────────────────

        [Fact]
        public void TryParse_ReturnsFalseForNullInput()
        {
            OdditariumResponseValidator.TryParse(null, out _).Should().BeFalse();
        }

        [Fact]
        public void TryParse_ReturnsFalseForEmptyInput()
        {
            OdditariumResponseValidator.TryParse("", out _).Should().BeFalse();
        }

        [Fact]
        public void TryParse_ReturnsFalseWhenTooFewOptions()
        {
            var json = """
                { "question": "What?", "mode": "expand", "anchor": "subject", "facet": "core-archetype",
                  "options": [ { "label": "Figure" }, { "label": "Relic" } ] }
                """;
            OdditariumResponseValidator.TryParse(json, out _).Should().BeFalse();
        }

        [Fact]
        public void TryParse_ReturnsTrueForValidResponse()
        {
            var json = """
                { "question": "What is the focal presence?", "mode": "expand", "anchor": "subject", "facet": "core-archetype",
                  "options": [
                    { "label": "Figure",     "hint": "A humanoid presence" },
                    { "label": "Creature",   "hint": "Something non-human" },
                    { "label": "Structure",  "hint": "Built or grown" },
                    { "label": "Vehicle",    "hint": "Something that moves" },
                    { "label": "Relic",      "hint": "An object with history" },
                    { "label": "Phenomenon", "hint": "A natural event" }
                  ] }
                """;
            var ok = OdditariumResponseValidator.TryParse(json, out var result);
            ok.Should().BeTrue();
            result.Mode.Should().Be("expand");
            result.Anchor.Should().Be("subject");
            result.Options.Should().HaveCount(6);
        }

        [Fact]
        public void TryParse_ToleratesProseWrapper()
        {
            var json = """
                Here is the JSON: { "question": "What?", "mode": "expand", "anchor": "subject", "facet": "core-archetype",
                  "options": [
                    { "label": "A" }, { "label": "B" }, { "label": "C" },
                    { "label": "D" }, { "label": "E" }, { "label": "F" }
                  ] } That was the output.
                """;
            OdditariumResponseValidator.TryParse(json, out _).Should().BeTrue();
        }

        // ─── OdditariumResponseValidator.TryParseExpansions ────────────────────

        [Fact]
        public void TryParseExpansions_ReturnsFalseForNull()
        {
            OdditariumResponseValidator.TryParseExpansions(null, out _).Should().BeFalse();
        }

        [Fact]
        public void TryParseExpansions_ParsesValidExpansionsJson()
        {
            var json = """
                { "expansions": { "subject": "A weathered marble bust, cracked crown", "setting": "Fog-covered stone ruins" } }
                """;
            var ok = OdditariumResponseValidator.TryParseExpansions(json, out var exp);
            ok.Should().BeTrue();
            exp.Should().ContainKey("subject");
            exp["subject"].Should().Be("A weathered marble bust, cracked crown");
        }

        [Fact]
        public void TryParseExpansions_ReturnsFalseWhenNoExpansions()
        {
            var json = """{ "question": "What?", "options": [] }""";
            OdditariumResponseValidator.TryParseExpansions(json, out _).Should().BeFalse();
        }

        // ─── OdditariumResponseValidator.TryParseDraft ─────────────────────────

        [Fact]
        public void TryParseDraft_ReturnsFalseForNull()
        {
            OdditariumResponseValidator.TryParseDraft(null, out _).Should().BeFalse();
        }

        [Fact]
        public void TryParseDraft_ParsesValidDraftJson()
        {
            var json = """{ "draft": "A weathered marble bust under crimson moonlight, coastal cliff, low-angle shot" }""";
            var ok = OdditariumResponseValidator.TryParseDraft(json, out var draft);
            ok.Should().BeTrue();
            draft.Should().Contain("weathered marble bust");
        }

        [Fact]
        public void TryParseDraft_ReturnsFalseWhenNoDraft()
        {
            var json = """{ "question": "What?", "options": [] }""";
            OdditariumResponseValidator.TryParseDraft(json, out _).Should().BeFalse();
        }

        // ─── OdditariumService.BuildContextWindow ──────────────────────────────

        [Fact]
        public void BuildContextWindow_ReturnsAllTurnsWhenUnderLimit()
        {
            var body = new OdditariumBody();
            for (int i = 0; i < 3; i++)
                body.History.Add(new OdditariumTurn { Source = "round", Choice = $"opt{i}" });

            var window = OdditariumService.BuildContextWindow(body);
            window.Should().HaveCount(3);
        }

        [Fact]
        public void BuildContextWindow_ReturnsLastNTurnsWhenOverLimit()
        {
            var body = new OdditariumBody();
            for (int i = 0; i < 12; i++)
                body.History.Add(new OdditariumTurn { Source = "round", Choice = $"opt{i}" });

            var window = OdditariumService.BuildContextWindow(body);
            window.Should().HaveCount(OdditariumService.ContextWindowTurns);
            window.Last().Choice.Should().Be("opt11");
        }
    }
}
