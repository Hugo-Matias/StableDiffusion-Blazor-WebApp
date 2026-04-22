using BlazorWebApp.Services;
using MudBlazor;

namespace BlazorWebApp.Components.Scheduler;

/// <summary>
/// Builds the <see cref="InfoContent"/> shown in the right-side <c>InfoDrawer</c>
/// while the user is on the Scheduler page. Structured into per-concept sections
/// (Directives, Variations, Targets, ...) so future forms can auto-expand the
/// matching panel via <see cref="IInfoService.HighlightSection(string?)"/>.
/// </summary>
public static class SchedulerInfoContent
{
    // Section ids used to cross-reference from form dialogs.
    public static class Sections
    {
        public const string Jobs = "jobs";
        public const string Targets = "targets";
        public const string Limit = "limit";
        public const string Permutation = "permutation";
        public const string Output = "output";
        public const string AdvancedJson = "advanced-json";

        // Directive ids use "directive:<kind>"
        public static string Directive(string kind) => $"directive:{kind}";

        // Variation ids use "variation:<kind>"
        public static string Variation(string kind) => $"variation:{kind}";
    }

    // Directive kinds - match the keys used in SchedulerEditorTab.DirectiveTypes.
    public static class DirectiveKinds
    {
        public const string SetValue = "SetValue";
        public const string AppendPrompt = "AppendPrompt";
        public const string ReplacePrompt = "ReplacePrompt";
        public const string AddLora = "AddLora";
        public const string RemoveLora = "RemoveLora";
        public const string ToggleLora = "ToggleLora";
        public const string AddPromptStyle = "AddPromptStyle";
        public const string SwapAsset = "SwapAsset";
        public const string SetOutput = "SetOutput";
    }

    // Variation kinds - match the keys used in SchedulerEditorTab.VariationTypes.
    public static class VariationKinds
    {
        public const string List = "List";
        public const string Range = "Range";
        public const string Random = "Random";
        public const string Wildcard = "Wildcard";
        public const string Llm = "LLM";
        public const string SearchReplace = "SearchReplace";
        public const string Toggle = "Toggle";
    }

    public static InfoContent Build() => new(
        Title: "Scheduler",
        Overview: "Compose multi-iteration generation campaigns from the current workflow. " +
                  "Each Job holds a snapshot of base parameters and a sequence of Actions. " +
                  "Every Action produces one or more generations by applying Directives (unit " +
                  "operations) and Variations (value sequences combined via cartesian product).",
        Shortcuts: new List<ShortcutInfo>(),
        Tips: new List<string>
        {
            "Actions are isolated: each one starts from the job's base parameters.",
            "Use directives for fixed changes, variations for sequences of values.",
            "The Limit caps an action's iterations when the cartesian product is large.",
            "Switching jobs while the editor is dirty prompts before losing changes.",
        },
        Sections: BuildSections()
    );

    // ------------------------------------------------------------------

    private static List<InfoSection> BuildSections()
    {
        var list = new List<InfoSection>
        {
            new InfoSection(
                Id: Sections.Jobs,
                Title: "Jobs",
                Icon: Icons.Material.Filled.PlayCircle,
                Items: new List<InfoItem>
                {
                    new(null, "A Job captures a workflow, base generation parameters, and an ordered list of Actions."),
                    new("Base parameters", "Snapshotted from the Generate page via the Schedule button. Readonly inside the editor."),
                    new("Workflow", "Readonly after creation. Changing the workflow would invalidate targeted Directives and Variations."),
                    new("Output config", "Default Project and Folder for saved images. Individual actions can override through SetOutput."),
                    new("Status", "Draft -> Queued -> Running -> Paused / Completed / Failed / Cancelled. Resume picks up from the last completed iteration."),
                }
            ),
            new InfoSection(
                Id: Sections.Targets,
                Title: "Targets",
                Icon: Icons.Material.Filled.GpsFixed,
                Items: new List<InfoItem>
                {
                    new(null, "A Target addresses a single parameter location that Directives and Variations can write to. Targets are polymorphic on $type just like their parents."),
                    new("Fragment", "A parameter inside a workflow fragment. Discriminator: \"$type\": \"fragment\". Example: { \"$type\": \"fragment\", \"fragmentId\": \"main_sampler\", \"paramKey\": \"steps\" }."),
                    new("LoRA", "Refers to a LoRA by name inside the base parameters' LoRA stack. Discriminator: \"$type\": \"lora\". Example: { \"$type\": \"lora\", \"loraName\": \"detailer_v2\" }."),
                    new("Asset", "Workflow asset slot: Model, Vae, Clip, etc. Discriminator: \"$type\": \"asset\". Example: { \"$type\": \"asset\", \"assetKey\": \"Model\" }."),
                    new("Prompt", "Either the positive or negative prompt text. Discriminator: \"$type\": \"prompt\". Example: { \"$type\": \"prompt\", \"isNegative\": false }."),
                    new("Output", "Project or Folder field on the job's output config. Discriminator: \"$type\": \"output\". Field is an enum: Project or Folder. Example: { \"$type\": \"output\", \"field\": \"Project\" }."),
                }
            ),
            new InfoSection(
                Id: Sections.Limit,
                Title: "Limit & cartesian size",
                Icon: Icons.Material.Filled.Speed,
                Items: new List<InfoItem>
                {
                    new(null, "Each Action's total iterations is the cartesian product of its Variations' counts."),
                    new("Limit", "Hard cap on iterations. Applied after the cartesian product is computed. Default is the cartesian total."),
                    new("Non-deterministic variations", "Random / LLM / Wildcard (with repeats) use their configured Count since they are not fixed sets."),
                    new("Tip", "Set Limit before configuring heavy variations to avoid accidental 1000+ iteration runs."),
                }
            ),
            new InfoSection(
                Id: Sections.Permutation,
                Title: "Permutation order",
                Icon: Icons.Material.Filled.Shuffle,
                Items: new List<InfoItem>
                {
                    new("Sequential", "Iterate the cartesian product in declaration order: outer variation varies slowest, inner fastest."),
                    new("Random", "Sample iterations from the full cartesian product without replacement; honours the optional Seed for reproducibility."),
                    new("Example", "Two variations: steps=[10,20,30], cfg=[5,7].\n" +
                                   "Sequential -> (10,5), (10,7), (20,5), (20,7), (30,5), (30,7).\n" +
                                   "Random (Seed=42) -> same six pairs, shuffled deterministically."),
                }
            ),
            new InfoSection(
                Id: Sections.Output,
                Title: "Project / Folder output",
                Icon: Icons.Material.Filled.FolderSpecial,
                Items: new List<InfoItem>
                {
                    new(null, "Output selectors reference existing Projects / Folders managed in the Gallery. Creating new ones must happen in the Gallery."),
                    new("Default", "Empty Project means the current gallery project is used at run time."),
                    new("Per-action override", "The SetOutput directive lets one action route generations to a different project or folder."),
                    new("Orphaned values", "If a referenced Project or Folder no longer exists, the editor flags a warning and blocks save until the value is fixed."),
                }
            ),
        };

        list.AddRange(BuildDirectiveSections());
        list.AddRange(BuildVariationSections());
        list.Add(BuildAdvancedJsonSection());
        return list;
    }

    private static IEnumerable<InfoSection> BuildDirectiveSections()
    {
        var icon = Icons.Material.Filled.PlayArrow;

        yield return new InfoSection(
            Id: Sections.Directive(DirectiveKinds.SetValue),
            Title: "Directive: SetValue",
            Icon: icon,
            Items: new List<InfoItem>
            {
                new("Purpose", "Write a constant value to any Target at the start of every iteration."),
                new("Discriminator", "\"$type\": \"set\"", IsCode: true),
                new("Required", "Target, Value (type-coerced to the target parameter)."),
                new("Example", "Target=main_sampler.steps, Value=20 -> every iteration uses 20 steps regardless of base."),
                new("JSON example", IsCode: true, Text:
                    "{\n" +
                    "  \"$type\": \"set\",\n" +
                    "  \"enabled\": true,\n" +
                    "  \"target\": { \"$type\": \"fragment\", \"fragmentId\": \"main_sampler\", \"paramKey\": \"steps\" },\n" +
                    "  \"value\": 20\n" +
                    "}"),
            }
        );

        yield return new InfoSection(
            Id: Sections.Directive(DirectiveKinds.AppendPrompt),
            Title: "Directive: AppendPrompt",
            Icon: icon,
            Items: new List<InfoItem>
            {
                new("Purpose", "Append or prefix extra text to the positive or negative prompt."),
                new("Discriminator", "\"$type\": \"append_prompt\"", IsCode: true),
                new("Required", "Text."),
                new("Options", "IsPrefix (prepend instead of append), IsNegative (target negative prompt), Separator (default ', ')."),
                new("Example", "Text='masterpiece, best quality', Separator=', ' -> suffix added to base prompt."),
                new("JSON example", IsCode: true, Text:
                    "{\n" +
                    "  \"$type\": \"append_prompt\",\n" +
                    "  \"enabled\": true,\n" +
                    "  \"text\": \"masterpiece, best quality\",\n" +
                    "  \"isPrefix\": false,\n" +
                    "  \"isNegative\": false,\n" +
                    "  \"separator\": \", \"\n" +
                    "}"),
            }
        );

        yield return new InfoSection(
            Id: Sections.Directive(DirectiveKinds.ReplacePrompt),
            Title: "Directive: ReplacePrompt",
            Icon: icon,
            Items: new List<InfoItem>
            {
                new("Purpose", "Search-and-replace inside the positive or negative prompt."),
                new("Discriminator", "\"$type\": \"replace_prompt\"", IsCode: true),
                new("Required", "Search, Replace."),
                new("Options", "IsNegative, CaseSensitive."),
                new("Example", "Search='sunset', Replace='aurora' -> rewrites every iteration's prompt."),
                new("JSON example", IsCode: true, Text:
                    "{\n" +
                    "  \"$type\": \"replace_prompt\",\n" +
                    "  \"enabled\": true,\n" +
                    "  \"search\": \"sunset\",\n" +
                    "  \"replace\": \"aurora\",\n" +
                    "  \"isNegative\": false,\n" +
                    "  \"caseSensitive\": false\n" +
                    "}"),
            }
        );

        yield return new InfoSection(
            Id: Sections.Directive(DirectiveKinds.AddLora),
            Title: "Directive: AddLora",
            Icon: icon,
            Items: new List<InfoItem>
            {
                new("Purpose", "Add a LoRA to the stack for every iteration of this action."),
                new("Discriminator", "\"$type\": \"add_lora\"", IsCode: true),
                new("Required", "Lora (name + path + strength)."),
                new("Note", "Directives are re-applied each iteration against the base parameters; duplicates are safe."),
                new("JSON example", IsCode: true, Text:
                    "{\n" +
                    "  \"$type\": \"add_lora\",\n" +
                    "  \"enabled\": true,\n" +
                    "  \"lora\": {\n" +
                    "    \"name\": \"detailer_v2\",\n" +
                    "    \"path\": \"loras/detailer_v2.safetensors\",\n" +
                    "    \"strength\": 0.8,\n" +
                    "    \"isEnabled\": true\n" +
                    "  }\n" +
                    "}"),
            }
        );

        yield return new InfoSection(
            Id: Sections.Directive(DirectiveKinds.RemoveLora),
            Title: "Directive: RemoveLora",
            Icon: icon,
            Items: new List<InfoItem>
            {
                new("Purpose", "Remove a LoRA present in base parameters for this action's iterations."),
                new("Discriminator", "\"$type\": \"remove_lora\"", IsCode: true),
                new("Required", "LoraName."),
                new("JSON example", IsCode: true, Text:
                    "{\n" +
                    "  \"$type\": \"remove_lora\",\n" +
                    "  \"enabled\": true,\n" +
                    "  \"loraName\": \"detailer_v2\"\n" +
                    "}"),
            }
        );

        yield return new InfoSection(
            Id: Sections.Directive(DirectiveKinds.ToggleLora),
            Title: "Directive: ToggleLora",
            Icon: icon,
            Items: new List<InfoItem>
            {
                new("Purpose", "Enable or disable a LoRA already present in base parameters."),
                new("Discriminator", "\"$type\": \"toggle_lora\"", IsCode: true),
                new("Required", "LoraName, Enable (bool)."),
                new("JSON example", IsCode: true, Text:
                    "{\n" +
                    "  \"$type\": \"toggle_lora\",\n" +
                    "  \"enabled\": true,\n" +
                    "  \"loraName\": \"detailer_v2\",\n" +
                    "  \"enable\": false\n" +
                    "}"),
            }
        );

        yield return new InfoSection(
            Id: Sections.Directive(DirectiveKinds.AddPromptStyle),
            Title: "Directive: AddPromptStyle",
            Icon: icon,
            Items: new List<InfoItem>
            {
                new("Purpose", "Apply one or more named Prompt Styles (managed on the Prompts page) to the current iteration's prompt."),
                new("Discriminator", "\"$type\": \"add_style\"", IsCode: true),
                new("Required", "StyleNames."),
                new("JSON example", IsCode: true, Text:
                    "{\n" +
                    "  \"$type\": \"add_style\",\n" +
                    "  \"enabled\": true,\n" +
                    "  \"styleNames\": [\"cinematic\", \"high-detail\"]\n" +
                    "}"),
            }
        );

        yield return new InfoSection(
            Id: Sections.Directive(DirectiveKinds.SwapAsset),
            Title: "Directive: SwapAsset",
            Icon: icon,
            Items: new List<InfoItem>
            {
                new("Purpose", "Replace a workflow asset (Model / Vae / Clip / ...) for every iteration of this action."),
                new("Discriminator", "\"$type\": \"swap_asset\"", IsCode: true),
                new("Required", "AssetKey, AssetValue."),
                new("Example", "AssetKey='Model', AssetValue='flux1-dev.safetensors'."),
                new("JSON example", IsCode: true, Text:
                    "{\n" +
                    "  \"$type\": \"swap_asset\",\n" +
                    "  \"enabled\": true,\n" +
                    "  \"assetKey\": \"Model\",\n" +
                    "  \"assetValue\": \"flux1-dev.safetensors\"\n" +
                    "}"),
            }
        );

        yield return new InfoSection(
            Id: Sections.Directive(DirectiveKinds.SetOutput),
            Title: "Directive: SetOutput",
            Icon: icon,
            Items: new List<InfoItem>
            {
                new("Purpose", "Route this action's generations to a specific Project and/or Folder instead of the job default."),
                new("Discriminator", "\"$type\": \"set_output\"", IsCode: true),
                new("Required", "ProjectName or FolderName (either or both)."),
                new("Tip", "Combine with Variations on a per-action basis to build organized result sets."),
                new("JSON example", IsCode: true, Text:
                    "{\n" +
                    "  \"$type\": \"set_output\",\n" +
                    "  \"enabled\": true,\n" +
                    "  \"projectName\": \"experiments-q1\",\n" +
                    "  \"folderName\": \"portraits\"\n" +
                    "}"),
            }
        );
    }

    private static IEnumerable<InfoSection> BuildVariationSections()
    {
        var icon = Icons.Material.Filled.Tune;

        yield return new InfoSection(
            Id: Sections.Variation(VariationKinds.List),
            Title: "Variation: List",
            Icon: icon,
            Items: new List<InfoItem>
            {
                new("Purpose", "Iterate over an explicit list of values."),
                new("Discriminator", "\"$type\": \"list\"", IsCode: true),
                new("Required", "Target, Values."),
                new("Count", "Equal to Values.Count."),
                new("Example", "Target=main_sampler.cfg, Values=[5, 6, 7] -> 3 iterations."),
                new("JSON example", IsCode: true, Text:
                    "{\n" +
                    "  \"$type\": \"list\",\n" +
                    "  \"target\": { \"$type\": \"fragment\", \"fragmentId\": \"main_sampler\", \"paramKey\": \"cfg\" },\n" +
                    "  \"values\": [5, 6, 7]\n" +
                    "}"),
            }
        );

        yield return new InfoSection(
            Id: Sections.Variation(VariationKinds.Range),
            Title: "Variation: Range",
            Icon: icon,
            Items: new List<InfoItem>
            {
                new("Purpose", "Generate a numeric sequence from Start to End stepping by Step."),
                new("Discriminator", "\"$type\": \"range\"", IsCode: true),
                new("Required", "Target, Start, End, Step."),
                new("Options", "IsInteger - cast values to long."),
                new("Count", "floor((End - Start) / Step) + 1."),
                new("Example", "Start=0.3, End=0.9, Step=0.1 -> 7 values."),
                new("JSON example", IsCode: true, Text:
                    "{\n" +
                    "  \"$type\": \"range\",\n" +
                    "  \"target\": { \"$type\": \"fragment\", \"fragmentId\": \"main_sampler\", \"paramKey\": \"denoise\" },\n" +
                    "  \"start\": 0.3,\n" +
                    "  \"end\": 0.9,\n" +
                    "  \"step\": 0.1,\n" +
                    "  \"isInteger\": false\n" +
                    "}"),
            }
        );

        yield return new InfoSection(
            Id: Sections.Variation(VariationKinds.Random),
            Title: "Variation: Random",
            Icon: icon,
            Items: new List<InfoItem>
            {
                new("Purpose", "Generate Count uniformly-distributed samples in [Min, Max]."),
                new("Discriminator", "\"$type\": \"random\"", IsCode: true),
                new("Required", "Target, Min, Max, Count."),
                new("Options", "Seed (reproducibility), IsInteger."),
                new("Note", "Count is mandatory - Random has no natural size."),
                new("JSON example", IsCode: true, Text:
                    "{\n" +
                    "  \"$type\": \"random\",\n" +
                    "  \"target\": { \"$type\": \"fragment\", \"fragmentId\": \"main_sampler\", \"paramKey\": \"seed\" },\n" +
                    "  \"min\": 0,\n" +
                    "  \"max\": 2147483647,\n" +
                    "  \"count\": 4,\n" +
                    "  \"seed\": 42,\n" +
                    "  \"isInteger\": true\n" +
                    "}"),
            }
        );

        yield return new InfoSection(
            Id: Sections.Variation(VariationKinds.Wildcard),
            Title: "Variation: Wildcard",
            Icon: icon,
            Items: new List<InfoItem>
            {
                new("Purpose", "Sample values from a wildcard collection defined on the Prompts page."),
                new("Discriminator", "\"$type\": \"wildcard\"", IsCode: true),
                new("Required", "Target, CollectionName."),
                new("Options", "Count (null -> all entries without repeats), AllowRepeats, Weighted."),
                new("Note", "Materialized at run time; collection edits after save are honoured."),
                new("JSON example", IsCode: true, Text:
                    "{\n" +
                    "  \"$type\": \"wildcard\",\n" +
                    "  \"target\": { \"$type\": \"prompt\", \"isNegative\": false },\n" +
                    "  \"collectionName\": \"characters\",\n" +
                    "  \"count\": 5,\n" +
                    "  \"allowRepeats\": false,\n" +
                    "  \"weighted\": false\n" +
                    "}"),
            }
        );

        yield return new InfoSection(
            Id: Sections.Variation(VariationKinds.Llm),
            Title: "Variation: LLM",
            Icon: icon,
            Items: new List<InfoItem>
            {
                new("Purpose", "Use Ollama to generate Count prompt variations guided by a system prompt."),
                new("Discriminator", "\"$type\": \"llm\"", IsCode: true),
                new("Required", "Target, ModelName, BasePrompt, Count."),
                new("Options", "SystemPrompt (guidance), IsNegative."),
                new("Performance", "LLM responses are prefetched in parallel with the prior image generation to hide latency."),
                new("JSON example", IsCode: true, Text:
                    "{\n" +
                    "  \"$type\": \"llm\",\n" +
                    "  \"target\": { \"$type\": \"prompt\", \"isNegative\": false },\n" +
                    "  \"modelName\": \"llama3.2\",\n" +
                    "  \"basePrompt\": \"a medieval knight in armor\",\n" +
                    "  \"systemPrompt\": \"Rewrite the prompt keeping the subject but changing setting and mood.\",\n" +
                    "  \"count\": 4,\n" +
                    "  \"isNegative\": false\n" +
                    "}"),
            }
        );

        yield return new InfoSection(
            Id: Sections.Variation(VariationKinds.SearchReplace),
            Title: "Variation: SearchReplace",
            Icon: icon,
            Items: new List<InfoItem>
            {
                new("Purpose", "Iterate by substituting Search with each entry in Replacements inside the prompt."),
                new("Discriminator", "\"$type\": \"search_replace\"", IsCode: true),
                new("Required", "Search, Replacements."),
                new("Options", "IsNegative, CaseSensitive."),
                new("Count", "Equal to Replacements.Count."),
                new("JSON example", IsCode: true, Text:
                    "{\n" +
                    "  \"$type\": \"search_replace\",\n" +
                    "  \"target\": { \"$type\": \"prompt\", \"isNegative\": false },\n" +
                    "  \"search\": \"{season}\",\n" +
                    "  \"replacements\": [\"spring\", \"summer\", \"autumn\", \"winter\"],\n" +
                    "  \"isNegative\": false,\n" +
                    "  \"caseSensitive\": false\n" +
                    "}"),
            }
        );

        yield return new InfoSection(
            Id: Sections.Variation(VariationKinds.Toggle),
            Title: "Variation: Toggle",
            Icon: icon,
            Items: new List<InfoItem>
            {
                new("Purpose", "Two-iteration A/B test between OnValue and OffValue."),
                new("Discriminator", "\"$type\": \"toggle\"", IsCode: true),
                new("Required", "Target, OnValue, OffValue."),
                new("Count", "Always 2."),
                new("JSON example", IsCode: true, Text:
                    "{\n" +
                    "  \"$type\": \"toggle\",\n" +
                    "  \"target\": { \"$type\": \"fragment\", \"fragmentId\": \"main_sampler\", \"paramKey\": \"cfg\" },\n" +
                    "  \"onValue\": 7,\n" +
                    "  \"offValue\": 4\n" +
                    "}"),
            }
        );
    }

    private static InfoSection BuildAdvancedJsonSection() => new(
        Id: Sections.AdvancedJson,
        Title: "Advanced: JSON mode",
        Icon: Icons.Material.Filled.Code,
        Items: new List<InfoItem>
        {
            new(null, "Every Directive, Variation, and Target serializes to JSON with a $type discriminator identifying the subtype. Property names use camelCase."),
            new("Discriminator", "$type is required and must match the kind strings listed in each section (e.g. 'set', 'append_prompt', 'list', 'range').", IsCode: true),
            new("Polymorphism", "Targets embedded inside Directives / Variations are themselves polymorphic ($type = fragment | lora | asset | prompt | output)."),
            new("When to use", "Bulk imports, cross-job copy/paste, or subtype features not yet surfaced in the form editor."),
            new("Validation", "Invalid JSON is rejected and the editor surfaces the parse error inline."),
            new("Full action example", IsCode: true, Text:
                "{\n" +
                "  \"label\": \"Sweep CFG vs Steps\",\n" +
                "  \"limit\": 12,\n" +
                "  \"permutationOrder\": \"Sequential\",\n" +
                "  \"directives\": [\n" +
                "    {\n" +
                "      \"$type\": \"append_prompt\",\n" +
                "      \"enabled\": true,\n" +
                "      \"text\": \"masterpiece, best quality\",\n" +
                "      \"separator\": \", \"\n" +
                "    }\n" +
                "  ],\n" +
                "  \"variations\": [\n" +
                "    {\n" +
                "      \"$type\": \"list\",\n" +
                "      \"target\": { \"$type\": \"fragment\", \"fragmentId\": \"main_sampler\", \"paramKey\": \"cfg\" },\n" +
                "      \"values\": [5, 6, 7]\n" +
                "    },\n" +
                "    {\n" +
                "      \"$type\": \"range\",\n" +
                "      \"target\": { \"$type\": \"fragment\", \"fragmentId\": \"main_sampler\", \"paramKey\": \"steps\" },\n" +
                "      \"start\": 10,\n" +
                "      \"end\": 30,\n" +
                "      \"step\": 10,\n" +
                "      \"isInteger\": true\n" +
                "    }\n" +
                "  ]\n" +
                "}"),
        }
    );
}
