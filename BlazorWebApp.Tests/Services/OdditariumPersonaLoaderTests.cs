using BlazorWebApp.Models;
using FluentAssertions;
using Xunit;

namespace BlazorWebApp.Tests.Services
{
    /// <summary>
    /// Tests for Odditarium persona JSON loading via OdditariumPersonaRoster.
    /// Verifies that personas are loaded from Data/Odditarium/Personas/*.json at startup.
    /// </summary>
    public class OdditariumPersonaLoaderTests
    {
        // ─── Roster Loading ──────────────────────────────────────────────────────

        [Fact]
        public void Roster_All_LoadsAtLeast5Personas()
        {
            // The 5 migrated personas: muse, iron-mother, void-walker, pixel-pixie, architect
            OdditariumPersonaRoster.All.Count.Should().BeGreaterThan(4);
        }

        [Fact]
        public void Roster_All_ContainsExpectedIds()
        {
            var ids = OdditariumPersonaRoster.All.Select(p => p.Id).ToList();

            ids.Should().Contain("muse");
            ids.Should().Contain("iron-mother");
            ids.Should().Contain("void-walker");
            ids.Should().Contain("pixel-pixie");
            ids.Should().Contain("architect");
        }

        [Fact]
        public void Roster_All_NoDuplicateIds()
        {
            var ids = OdditariumPersonaRoster.All.Select(p => p.Id).ToList();
            ids.Should().OnlyHaveUniqueItems();
        }

        // ─── FindById ────────────────────────────────────────────────────────────

        [Fact]
        public void FindById_ExistingId_ReturnsPersona()
        {
            var persona = OdditariumPersonaRoster.FindById("muse");

            persona.Should().NotBeNull();
            persona!.Id.Should().Be("muse");
        }

        [Fact]
        public void FindById_NonExistentId_ReturnsNull()
        {
            var persona = OdditariumPersonaRoster.FindById("does-not-exist");

            persona.Should().BeNull();
        }

        // ─── Image Asset Auto-Resolution ─────────────────────────────────────────

        [Fact]
        public void Persona_ImageAssets_AutoResolvedFromId()
        {
            var persona = OdditariumPersonaRoster.FindById("muse");
            persona.Should().NotBeNull();

            persona!.ImageAssetIdle.Should().Be("/odditarium/personas/muse/idle.png");
            persona.ImageAssetHover.Should().Be("/odditarium/personas/muse/hover.png");
            persona.ImageAssetActive.Should().Be("/odditarium/personas/muse/active.png");
            persona.ImageAssetThinking.Should().Be("/odditarium/personas/muse/thinking-1.png");
        }

        [Fact]
        public void Persona_ImageAssets_HyphenatedId_ResolvedCorrectly()
        {
            var persona = OdditariumPersonaRoster.FindById("iron-mother");
            persona.Should().NotBeNull();

            persona!.ImageAssetIdle.Should().Be("/odditarium/personas/iron-mother/idle.png");
            persona.ImageAssetHover.Should().Be("/odditarium/personas/iron-mother/hover.png");
            persona.ImageAssetActive.Should().Be("/odditarium/personas/iron-mother/active.png");
            persona.ImageAssetThinking.Should().Be("/odditarium/personas/iron-mother/thinking-1.png");
        }

        // ─── Required Fields Validation ──────────────────────────────────────────

        [Fact]
        public void Persona_All_HaveNonEmptyId()
        {
            foreach (var p in OdditariumPersonaRoster.All)
                p.Id.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void Persona_All_HaveNonEmptyName()
        {
            foreach (var p in OdditariumPersonaRoster.All)
                p.Name.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void Persona_All_HaveNonEmptyTagline()
        {
            foreach (var p in OdditariumPersonaRoster.All)
                p.Tagline.Should().NotBeNullOrWhiteSpace();
        }

        // ─── Specific Persona Data Integrity ─────────────────────────────────────

        [Fact]
        public void Muse_HasCorrectIdentity()
        {
            var persona = OdditariumPersonaRoster.FindById("muse");
            persona.Should().NotBeNull();
            persona!.Name.Should().Be("Aurelie");
            persona.Tagline.Should().Be("Golden-Tongued Critic");
        }

        [Fact]
        public void IronMother_HasCorrectIdentity()
        {
            var persona = OdditariumPersonaRoster.FindById("iron-mother");
            persona.Should().NotBeNull();
            persona!.Name.Should().Be("Vera Colt");
            persona.Tagline.Should().Be("Field-Worn Veteran");
        }

        [Fact]
        public void VoidWalker_HasCorrectIdentity()
        {
            var persona = OdditariumPersonaRoster.FindById("void-walker");
            persona.Should().NotBeNull();
            persona!.Name.Should().Be("Nyx");
            persona.Tagline.Should().Be("Between-Space Entity");
        }

        [Fact]
        public void PixelPixie_HasCorrectIdentity()
        {
            var persona = OdditariumPersonaRoster.FindById("pixel-pixie");
            persona.Should().NotBeNull();
            persona!.Name.Should().Be("Trixel");
            persona.Tagline.Should().Be("Digital Chaos Sprite");
        }

        [Fact]
        public void Architect_HasCorrectIdentity()
        {
            var persona = OdditariumPersonaRoster.FindById("architect");
            persona.Should().NotBeNull();
            persona!.Name.Should().Be("Null");
            persona.Tagline.Should().Be("Cold Logic Engine");
        }

        // ─── Voice Depth Fields ──────────────────────────────────────────────────

        [Fact]
        public void Persona_All_HaveVoiceDepthFields()
        {
            foreach (var p in OdditariumPersonaRoster.All)
            {
                p.QuestionFraming.Should().NotBeNullOrWhiteSpace();
                p.OptionVoice.Should().NotBeNullOrWhiteSpace();
                p.ForbiddenZones.Should().NotBeNullOrWhiteSpace();
                p.CreativePhilosophy.Should().NotBeNullOrWhiteSpace();
            }
        }

        // ─── Anchor Affinity ─────────────────────────────────────────────────────

        [Fact]
        public void Persona_All_HaveAnchorAffinity()
        {
            foreach (var p in OdditariumPersonaRoster.All)
                p.AnchorAffinity.Should().NotBeEmpty();
        }

        // ─── Messages ────────────────────────────────────────────────────────────

        [Fact]
        public void Persona_All_HaveThinkingMessages()
        {
            foreach (var p in OdditariumPersonaRoster.All)
                ((int)p.ThinkingMessages.Length).Should().BeGreaterThan(14);
        }

        [Fact]
        public void Persona_All_HaveProdMessages()
        {
            foreach (var p in OdditariumPersonaRoster.All)
                ((int)p.ProdMessages.Length).Should().BeGreaterThan(14);
        }
    }
}
