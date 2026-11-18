namespace BlazorWebApp.Models
{
    public enum EvolveIntensity { Subtle, Moderate, Strong, Wild }

    public enum EvolveLength { Match, Concise, Verbose }

    /// <summary>
    /// Verbosity profile for chat-edit responses. Controls how much the LLM
    /// elaborates when integrating an instruction into the existing prompt.
    /// </summary>
    public enum ChatVerbosity
    {
        /// <summary>Minimal change: tack the edit on or substitute one phrase. Fast, terse.</summary>
        Minimal,
        /// <summary>Naturally weave the edit in while matching the base prompt's density.</summary>
        Match,
        /// <summary>Rewrite affected segments with richer description; expand naturally.</summary>
        Detailed,
        /// <summary>Heavily elaborate the resulting scene with sensory detail and atmosphere.</summary>
        Elaborate,
    }

    /// <summary>
    /// Control surface for spawning prompt variations (Phase 8.6 Step 4).
    /// Persisted fields (Intensity, Targets, Length) live on
    /// <see cref="AppStatePromptsLLMWorkshop"/>; per-call escape hatches
    /// (CustomDirection, Preserve) are not persisted.
    /// </summary>
    public sealed class EvolveControls
    {
        public int Count { get; set; } = 5;
        public string Model { get; set; } = string.Empty;

        public EvolveIntensity Intensity { get; set; } = EvolveIntensity.Moderate;

        /// <summary>
        /// Optional target categories (Subject, Clothing, Pose, Setting, Camera,
        /// Lighting, Style, Mood). Empty = freeform.
        /// </summary>
        public List<string> Targets { get; set; } = new();

        public EvolveLength Length { get; set; } = EvolveLength.Match;

        /// <summary>Comma-separated tokens the LLM should keep verbatim (per-call).</summary>
        public string Preserve { get; set; } = string.Empty;

        /// <summary>Free-form steering note appended to the system prompt (per-call).</summary>
        public string CustomDirection { get; set; } = string.Empty;

        /// <summary>Sampling temperature passed through to OllamaOptions.</summary>
        public float Temperature { get; set; } = 0.9f;

        /// <summary>
        /// Builds the human-readable summary stored on <see cref="Data.Entities.PromptWorkshopNode.Instruction"/>.
        /// e.g. "Subtle, Clothing+Pose, Verbose".
        /// </summary>
        public string BuildInstructionSummary()
        {
            var parts = new List<string> { Intensity.ToString() };
            if (Targets.Count > 0)
                parts.Add(string.Join("+", Targets));
            if (Length != EvolveLength.Match)
                parts.Add(Length.ToString());
            return string.Join(", ", parts);
        }

        /// <summary>
        /// Renders the system prompt template with the active controls. Used both for the
        /// LLM call and for the live "Show resolved system prompt" preview in the dialog.
        ///
        /// Layout (top to bottom):
        ///   1. ROLE             - one-liner identity.
        ///   2. WORKSHOP FLOW    - fixed walkthrough showing how prompts grow via chat edits
        ///                         then branch via variations. Anchors the model in the tool's
        ///                         actual usage instead of free-floating "make a variation".
        ///   3. TARGET GLOSSARY  - always-emitted axis dictionary (Subject..Mood) so the model
        ///                         understands what "leave Subject intact" means in token space.
        ///   4. ACTIVE CONFIG    - INTENSITY / TARGETS / LENGTH / PRESERVE / ADDITIONAL DIRECTION.
        ///   5. STYLE            - format mirroring + no quality boosters.
        ///   6. EXAMPLE          - generic 3-output sample varying Lighting+Mood at Strong, kept
        ///                         deliberately distant from typical user prompts so its tokens
        ///                         do not bleed into the output.
        ///   7. OUTPUT FORMAT    - numbered list contract.
        /// </summary>
        public string BuildSystemPrompt(int count)
        {
            var intensityHint = Intensity switch
            {
                EvolveIntensity.Subtle =>
                    "INTENSITY: Subtle. Make small, restrained changes. Tweak only one or two descriptors per variation. " +
                    "Keep ~85% of the base prompt's wording intact.",
                EvolveIntensity.Moderate =>
                    "INTENSITY: Moderate. Make meaningful but coherent changes that read as natural alternatives. " +
                    "Each variation should feel like a sibling of the base prompt, not a different scene.",
                EvolveIntensity.Strong =>
                    "INTENSITY: Strong. Make bold changes with clear visual differences between outputs. " +
                    "Replace whole descriptors freely; siblings should look distinct at a glance.",
                EvolveIntensity.Wild =>
                    "INTENSITY: Wild. Reinterpret the base prompt aggressively. Swap settings, lighting, mood, or style as needed. " +
                    "Each variation may diverge dramatically as long as the core subject identity remains.",
                _ => "INTENSITY: Moderate. Apply meaningful variations."
            };

            var targetsHint = Targets.Count == 0
                ? "TARGETS: Unrestricted. Vary any axis from the glossary above that improves diversity."
                : $"TARGETS: ONLY vary these axes: {string.Join(", ", Targets)}. " +
                  "Every other axis (especially Subject identity) MUST remain intact across all variations - " +
                  "do not paraphrase or replace untouched tokens.";

            var lengthHint = Length switch
            {
                EvolveLength.Concise =>
                    "LENGTH: Concise. Each variation should be tight and economical. Trim filler. Aim for ~50 tokens.",
                EvolveLength.Verbose =>
                    "LENGTH: Verbose. Each variation may be more elaborate, with richer descriptors and atmosphere. Aim for ~120 tokens.",
                _ =>
                    "LENGTH: Match. Mirror the base prompt's density and structure. Do not noticeably grow or shrink it."
            };

            var preserveHint = string.IsNullOrWhiteSpace(Preserve)
                ? string.Empty
                : $"\nPRESERVE: Keep these tokens verbatim across every variation: {Preserve.Trim()}.";

            var customHint = string.IsNullOrWhiteSpace(CustomDirection)
                ? string.Empty
                : $"\nADDITIONAL DIRECTION: {CustomDirection.Trim()}";

            // Workshop flow walkthrough - fixed, teaches the tool's usage model.
            const string flow =
                "WORKSHOP FLOW (how prompts arrive at this step):\n" +
                "  1. User starts a session with a seed prompt.\n" +
                "  2. User refines via chat edits, each edit producing a new prompt node.\n" +
                "  3. When happy, user spawns variations from the latest node - that is the call you are answering now.\n" +
                "  Worked example:\n" +
                "    Seed:                       \"young woman, oil painting\"\n" +
                "    + chat \"knight in armor\":   \"young woman in plate armor, oil painting\"\n" +
                "    + chat \"in a forest\":       \"young woman in plate armor, in a forest, oil painting\"\n" +
                "    + chat \"golden hour, dramatic\": \"young woman in plate armor, in a forest, golden hour, dramatic, oil painting\"\n" +
                "    Then the user asks for variations on that final line - your job.\n";

            // Always-on target glossary so TARGETS: filters are interpretable.
            const string glossary =
                "TARGET AXES (glossary - always applies, regardless of which axes are active):\n" +
                "  - Subject:  the core identity - WHO/WHAT is depicted.   e.g. knight -> ranger, paladin, rogue\n" +
                "  - Clothing: attire and accessories.                     e.g. plate armor -> leather cuirass, ceremonial robes\n" +
                "  - Pose:     body action, posture, gesture.              e.g. standing -> kneeling, mid-stride, drawing a sword\n" +
                "  - Setting:  location and environment.                   e.g. forest -> ruined castle, alpine pass, marketplace\n" +
                "  - Camera:   framing, angle, lens, distance.             e.g. portrait -> wide shot, low angle, 85mm close-up\n" +
                "  - Lighting: light source, direction, quality.           e.g. golden hour -> moonlit, candlelit, harsh midday sun\n" +
                "  - Style:    medium, render, artist references.          e.g. oil painting -> watercolor, studio photo, ink wash\n" +
                "  - Mood:     emotional atmosphere.                       e.g. dramatic -> serene, ominous, melancholic, joyful\n";

            // Generic example deliberately distant from common user prompts so its tokens
            // (lighthouse, sea cliff) do not contaminate the output.
            const string example =
                "EXAMPLE (Strong intensity, Targets = Lighting + Mood, base = \"a lighthouse on a sea cliff, watercolor\"):\n" +
                "  1. a lighthouse on a sea cliff at dawn, soft pink rim light, calm hopeful atmosphere, watercolor\n" +
                "  2. a lighthouse on a sea cliff under a thunderstorm, sharp blue lightning flashes, foreboding tension, watercolor\n" +
                "  3. a lighthouse on a sea cliff at midnight, beam cutting through fog, lonely melancholic stillness, watercolor\n" +
                "  Notice: subject (lighthouse), setting (sea cliff), and style (watercolor) are unchanged across all three. " +
                "Only Lighting and Mood vary. Format mirrors the base (comma-separated phrases, no full sentences).\n";

            return
                "ROLE: You generate creative variations of an image-generation prompt for a diffusion model.\n\n" +
                flow + "\n" +
                glossary + "\n" +
                "ACTIVE CONFIG:\n" +
                intensityHint + "\n" +
                targetsHint + "\n" +
                lengthHint +
                preserveHint + customHint + "\n\n" +
                "STYLE: Mirror the base prompt's format (comma-separated tags or short descriptive phrases). " +
                "Do not switch styles. Do not add quality boosters like 'masterpiece' or 'best quality' unless the base already has them.\n\n" +
                example + "\n" +
                $"OUTPUT FORMAT: Return EXACTLY {count} variations as a numbered list (\"1. ...\\n2. ...\"). " +
                "No preamble, no commentary, no trailing notes. Just the numbered prompts.";
        }
    }

    /// <summary>
    /// Builders for the chat-edit system prompt. Lives outside <see cref="EvolveControls"/>
    /// because it's used by chat mode (one input -> one output), not the variation flow.
    /// </summary>
    public static class ChatEditPromptBuilder
    {
        public static string BuildSystemPrompt(ChatVerbosity verbosity)
        {
            var verbosityHint = verbosity switch
            {
                ChatVerbosity.Minimal =>
                    "VERBOSITY: Minimal. Make the smallest change possible: substitute or append the requested concept and stop. " +
                    "Do not embellish unrelated tags. Output should be barely longer than the input.",
                ChatVerbosity.Detailed =>
                    "VERBOSITY: Detailed. The output MUST be noticeably longer than the input. Where the edit lands, expand naturally with " +
                    "descriptive language - lighting, materials, colors, atmosphere - so the new concept reads as fully realized. " +
                    "Add at least 3-5 supporting descriptors related to the edit. Untouched parts stay intact. Do NOT just append the edit verbatim.",
                ChatVerbosity.Elaborate =>
                    "VERBOSITY: Elaborate. The output MUST be substantially longer (often 2x+ the input length). Reimagine the affected segments " +
                    "with rich sensory detail: lighting quality, color palette, textures, atmosphere, ambient action, secondary elements. " +
                    "Untouched concepts still survive but may be lightly polished. Do NOT be lazy here - if Elaborate is requested, the user wants depth.",
                _ =>
                    "VERBOSITY: Match. Integrate the edit smoothly while matching the base prompt's density and style. " +
                    "If the base is terse tags, stay terse. If the base is descriptive, stay descriptive."
            };

            // Few-shot examples cover the common failure modes:
            //   (a) LLMs treating the edit as a literal append ("...sunny afternoon, night").
            //   (b) LLMs being lazy at Detailed/Elaborate and producing barely-larger output.
            // Two multi-verbosity worked examples make the size delta explicit.
            var examples =
                "EXAMPLE 1 (base = \"a samurai standing in a bamboo forest, sunny afternoon\", edit = \"make it night\")\n" +
                "  Minimal:   a samurai standing in a bamboo forest, sunny afternoon, night\n" +
                "  Match:     a samurai standing in a bamboo forest at night, moonlit\n" +
                "  Detailed:  a samurai standing in a bamboo forest at night, moonlight filtering through tall stalks, cool blue shadows on the ground, faint mist\n" +
                "  Elaborate: a samurai standing motionless in a bamboo forest at midnight, silver moonlight piercing the canopy in pale shafts, " +
                "cold blue shadows pooling between the stalks, low mist drifting around his sandals, distant cicadas, a single firefly, breath visible in the cold air\n\n" +
                "EXAMPLE 2 (base = \"a wooden cabin by a lake, autumn\", edit = \"add a storm rolling in\")\n" +
                "  Minimal:   a wooden cabin by a lake, autumn, storm rolling in\n" +
                "  Match:     a wooden cabin by a lake under an incoming autumn storm, dark clouds gathering\n" +
                "  Detailed:  a wooden cabin by a lake under an incoming autumn storm, heavy grey clouds piling on the horizon, lake surface choppy and dark, wind bending the orange treetops\n" +
                "  Elaborate: a small wooden cabin perched on the shore of a steel-grey lake as an autumn storm rolls in, towering charcoal clouds devouring the sky, " +
                "first fat raindrops pocking the water, gusts ripping copper leaves from the birches, a single lit window glowing warm against the gloom, distant rumble of thunder\n\n" +
                "EXAMPLE 3 (base = \"portrait of a woman, red dress, studio lighting\", edit = \"change to blue dress\")\n" +
                "  Match: portrait of a woman, blue dress, studio lighting\n" +
                "  Notice: only the targeted token changed - subject, framing, and lighting are untouched.\n";

            return
                "You are a prompt-editing assistant inside a creative workshop for image generation.\n" +
                "The conversation history is alternating user instructions and assistant-produced prompts.\n" +
                "Apply the latest user instruction to the most recent assistant prompt while preserving every unrelated concept.\n" +
                "Output is a single updated prompt - no commentary, no preamble, no quotes, no list markers.\n\n" +
                verbosityHint + "\n\n" +
                "STYLE RULES:\n" +
                "- Mirror the base prompt's format (comma-separated tags vs. descriptive sentences). Do not switch styles.\n" +
                "- Do NOT just append the edit verbatim if it produces awkward phrasing. Integrate it naturally.\n" +
                "- Do NOT add quality boosters (\"masterpiece\", \"best quality\", \"8k\") unless the base already has them.\n" +
                "- Do NOT lose untouched concepts (subject identity, framing, art style, etc.).\n\n" +
                examples + "\n" +
                "Return only the updated prompt.";
        }

        public static string ToLabel(ChatVerbosity v) => v switch
        {
            ChatVerbosity.Minimal => "Minimal",
            ChatVerbosity.Detailed => "Detailed",
            ChatVerbosity.Elaborate => "Elaborate",
            _ => "Match",
        };
    }
}
