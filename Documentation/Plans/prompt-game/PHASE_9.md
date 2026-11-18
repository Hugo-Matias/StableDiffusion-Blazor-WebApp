# Phase 9 — Persona Depth Pass

> **Status:** [ ] Not started
> **Complexity:** 5 points (Fibonacci)
> **Depends on:** Phase 8 complete (anchor sequencer must be live so persona biases can be observed against the structured flow)

---

## Problem Statement

The five current personas establish distinct voice and visual identity, but their influence on actual game output is shallow. All persona differentiation currently flows through a single paragraph in the system prompt (`ToneBias` + `Description` + `StylePreferences`). The LLM acknowledges the persona in question phrasing but the actual _option content_ — which anchors it picks for deepen rounds, which facets it prefers, what it avoids — is not shaped by persona at all.

The result: runs with The Muse and runs with Iron Mother produce different question wording but surprisingly similar option sets, especially after anchor sequencing locks the anchor/mode frame.

The goal is to give each persona **semantic territory** — a declared field of concepts it gravitates toward, a declared field it avoids, and a set of facet preferences per anchor tier — without collapsing the option contrast spread or constraining the player into a predictable tunnel.

---

## Design Principles

1. **Voice ≠ Option content.** The persona shapes _which facets_ feel natural within an anchor and _what flavors_ hints carry. It does not determine which option the player "should" pick.
2. **No banned anchors.** A persona never refuses to generate an anchor. It can be _reluctant_ (expressed in the question voice) but must produce 6 viable options.
3. **Avoidance is a lean, not a wall.** `AvoidanceConcepts` lists words/themes the persona would _not_ be excited about, injected into the system prompt as "you find these less interesting." The LLM can still produce one token from that field if contrast spread demands it.
4. **Anchor-tier facet preferences are optional.** If the persona has nothing distinctive to say about a tier, the field is empty and no extra instruction is injected. Zero overhead for anchors the persona is neutral on.
5. **Reaction messages add richness without gameplay impact.** Persona-voiced reactions to the player's choice (shown briefly after a pick) reinforce character without affecting generation.

---

## New `OdditariumPersona` Fields

```csharp
/// <summary>
/// Concepts, aesthetics, or themes this persona finds uninteresting or actively avoids.
/// Injected as a lean (not a hard ban) in the system prompt.
/// Examples: "photorealism, modern architecture, corporate settings"
/// </summary>
public string AvoidanceConcepts { get; set; } = string.Empty;

/// <summary>
/// When not null, overrides the generic facet examples for Core tier anchors (subject + setting).
/// Lowercase-kebab list of facets this persona naturally reaches for.
/// Examples: "material-and-patina, symbolic-weight, compositional-role, visual-geometry"
/// </summary>
public string[]? CoreFacetPreferences { get; set; }

/// <summary>
/// When not null, overrides the generic facet examples for Structural tier anchors
/// (action, lighting, framing, atmosphere).
/// </summary>
public string[]? StructuralFacetPreferences { get; set; }

/// <summary>
/// When not null, influences Enrichment tier facet generation (mood, style, detail, etc.).
/// </summary>
public string[]? EnrichmentFacetPreferences { get; set; }

/// <summary>
/// Short in-character reaction lines shown after the player picks an option.
/// The UI cycles these at random. 8-12 entries. Reacts to a *pick* rather than a *wait*.
/// Examples: "A decisive soul.", "That one has weight.", "Interesting. I would have chosen otherwise."
/// </summary>
public string[] PickReactions { get; set; } = Array.Empty<string>();

/// <summary>
/// Short in-character reaction shown when player skips an anchor.
/// 3-5 entries. Should feel like the persona commenting on the skip, not condemning it.
/// </summary>
public string[] SkipReactions { get; set; } = Array.Empty<string>();
```

---

## Roster Updates (per persona)

### The Muse

- `AvoidanceConcepts`: "digital interfaces, industrial machinery, corporate environments, pixel art"
- `CoreFacetPreferences`: `["material-and-patina", "symbolic-weight", "silhouette-and-proportion", "visual-geometry"]`
- `StructuralFacetPreferences`: `["directional-light", "shadow-depth", "color-temperature", "compositional-balance"]`
- `EnrichmentFacetPreferences`: `["tonal-register", "medium-and-texture", "surface-luminosity"]`
- `PickReactions`: 10 entries, poetic and slightly evaluative ("That has a certain poise.", "Yes. That line works.", "Not my first choice — but it breathes.")
- `SkipReactions`: 4 entries, politely dismissive ("Some doors should remain closed.", "Not every frame deserves filling.")

### Iron Mother

- `AvoidanceConcepts`: "pastel palettes, fantasy whimsy, delicate ornamentation, kawaii aesthetics"
- `CoreFacetPreferences`: `["wear-and-damage", "silhouette-bulk", "material-hardness", "ground-contact"]`
- `StructuralFacetPreferences`: `["motion-economy", "light-source-tactical", "camera-angle-threat", "environmental-hazard"]`
- `EnrichmentFacetPreferences`: `["weathering-detail", "texture-roughness", "color-desaturation"]`
- `PickReactions`: 10 entries, pragmatic ("Solid call.", "That'll hold.", "Good instinct.", "Field-tested logic.")
- `SkipReactions`: 4 entries, dismissive-tactical ("Not worth the ammo.", "Cut and move.")

### Void Walker

- `AvoidanceConcepts`: "mundane interiors, everyday objects, literal realism, domestic scenes"
- `CoreFacetPreferences`: `["spatial-impossibility", "scale-distortion", "recursive-form", "threshold-nature"]`
- `StructuralFacetPreferences`: `["light-as-entity", "geometry-distortion", "atmospheric-density", "perspective-collapse"]`
- `EnrichmentFacetPreferences`: `["chromatic-anomaly", "temporal-ambiguity", "surface-uncanny"]`
- `PickReactions`: 10 entries, cryptic and approving ("The fold accepts this.", "It was always going to be that one.", "Yes. You are beginning to see.")
- `SkipReactions`: 4 entries, indifferent ("That void needed no filling.", "It recedes. As it should.")

### Pixel Pixie

- `AvoidanceConcepts`: "desaturated palettes, brutalist architecture, gritty realism, monochrome"
- `CoreFacetPreferences`: `["scale-vs-environment", "expressive-pose", "color-accent-point", "character-personality-read"]`
- `StructuralFacetPreferences`: `["energy-trail", "lighting-from-below", "dynamic-angle", "atmosphere-sparkle"]`
- `EnrichmentFacetPreferences`: `["palette-pop", "texture-softness", "ambient-playfulness"]`
- `PickReactions`: 10 entries, enthusiastic ("YES that one!! Perfect!!", "OHHH that's going in the top five EVER!!", "Bold!! I love bold!!")
- `SkipReactions`: 4 entries, disappointed-but-bouncy ("Nooo but FINE!! Onwards!!", "The door was RIGHT THERE but ok!!")

### The Architect

- `AvoidanceConcepts`: "organic chaos, hand-painted textures, loose composition, natural disorder"
- `CoreFacetPreferences`: `["structural-purpose", "material-specification", "dimensional-ratio", "load-bearing-logic"]`
- `StructuralFacetPreferences`: `["light-directionality", "shadow-as-structure", "framing-precision", "atmospheric-clarity"]`
- `EnrichmentFacetPreferences`: `["surface-specification", "color-system", "geometric-detail"]`
- `PickReactions`: 10 entries, clinical ("Optimal.", "Within acceptable parameters.", "That selection satisfies the constraint.", "Input logged.")
- `SkipReactions`: 4 entries, matter-of-fact ("Sector skipped. Continuing.", "Variable eliminated.")

---

## System Prompt Changes (`OdditariumService.cs`)

In `BuildRoundSystemPrompt` and `BuildSequencedRoundSystemPrompt`, after the existing persona block:

```csharp
if (!string.IsNullOrWhiteSpace(persona.AvoidanceConcepts))
    sb.AppendLine($"You find these less interesting (lean away, don't ban): {persona.AvoidanceConcepts}");

var facetPrefs = GetFacetPrefsForAnchor(persona, targetAnchor);
if (facetPrefs is { Length: > 0 })
{
    sb.AppendLine($"For this anchor ({targetAnchor}), your natural facet instincts: {string.Join(", ", facetPrefs)}");
    sb.AppendLine("These are starting points — invent variants or alternatives that fit the session context.");
}
```

Private helper:

```csharp
private static string[]? GetFacetPrefsForAnchor(OdditariumPersona persona, string? anchor)
{
    if (anchor == null) return null;
    if (OdditariumAnchors.Core.Contains(anchor))    return persona.CoreFacetPreferences;
    if (OdditariumAnchors.Structural.Contains(anchor)) return persona.StructuralFacetPreferences;
    if (OdditariumAnchors.Enrichment.Contains(anchor)) return persona.EnrichmentFacetPreferences;
    return null;
}
```

---

## UI Changes

### Pick Reaction Toast

After the user picks an option in `OdditariumRoundPanel.razor`, display a brief persona reaction:

- Trigger: `HandleChoiceAsync` sets a `_pickReaction` field
- Render: a `<div class="odd-pick-reaction">` that auto-fades after 2.5s using CSS `animation: odd-reaction-fade 2.5s forwards`
- Content: `GetRandomReaction(persona.PickReactions)` — random pick from the array, weighted toward lines not recently shown (simple index cycling is fine)
- Positioned: beneath the layer chips sidebar, above the action buttons area

### Skip Reaction

Same mechanism, triggered by `HandleSkipAsync`, uses `persona.SkipReactions`.

### Persona field in `OdditariumBody` — no change needed. `PersonaId` already stored.

---

## Implementation Steps

| #   | Description                                                                                                                                                                                        | Points |
| --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| 1   | Add 6 new fields to `OdditariumPersona` model                                                                                                                                                      | 1      |
| 2   | Write `PickReactions`, `SkipReactions`, `AvoidanceConcepts`, `CoreFacetPreferences`, `StructuralFacetPreferences`, `EnrichmentFacetPreferences` for all 5 personas in `OdditariumPersonaRoster.cs` | 2      |
| 3   | `GetFacetPrefsForAnchor` helper + inject `AvoidanceConcepts` and tier-facet prefs into round system prompts                                                                                        | 1      |
| 4   | Pick/Skip reaction toast in `OdditariumRoundPanel.razor` + CSS fade animation                                                                                                                      | 2      |

**Total: 5 points**

---

## Success Criteria

1. A Muse run and an Iron Mother run on the same anchor/facet frame produce noticeably different facet names and option flavors.
2. Avoidance concepts appear at most once per run per persona (lean, not hard ban — contrast spread can still pull one in when needed).
3. Pick reactions appear after every confirmed choice and fade cleanly.
4. Skip reactions appear after every skip.
5. No new required fields on `OdditariumBody` — zero migration needed.
6. All existing Phase 7 + 8 tests still pass.

---

## Open Clarifications

- **Facet prefs as starting points vs. constraints.** The prompt says "your natural instincts — invent variants." This means the LLM can use them as seeds rather than an exhaustive list, which avoids facet repetition within a run while still shaping the tone.
- **Reaction animation.** CSS-only `forwards` animation is simplest. If the toast needs to stack with other UI elements, consider a `MudSnackbar` instead.
- **Persona count.** Five personas is thin for long-term replayability. Phase 9 intentionally does not add new personas — only deepens the five. A sixth persona pass can be a Phase 10 stretch goal.
