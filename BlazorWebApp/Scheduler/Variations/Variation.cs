using System.Text.Json.Serialization;
using BlazorWebApp.Scheduler.Targets;

namespace BlazorWebApp.Scheduler.Variations
{
    /// <summary>
    /// A sequence-producing dimension contributing to an action's cartesian iteration plan.
    /// Deterministic variations (List, Range, Toggle, SearchReplace) yield a concrete value list
    /// up front. Random, Wildcard, and LLM variations resolve their values just before the action
    /// runs, using services provided by the execution engine (Phase 3).
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(ListVariation), "list")]
    [JsonDerivedType(typeof(RangeVariation), "range")]
    [JsonDerivedType(typeof(RandomVariation), "random")]
    [JsonDerivedType(typeof(WildcardVariation), "wildcard")]
    [JsonDerivedType(typeof(LlmVariation), "llm")]
    [JsonDerivedType(typeof(SearchReplaceVariation), "search_replace")]
    [JsonDerivedType(typeof(ToggleVariation), "toggle")]
    public abstract class Variation
    {
        /// <summary>
        /// Optional user-facing label for the Scheduler UI.
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// Parameter location this variation writes to for each produced value.
        /// </summary>
        public ParameterTarget Target { get; set; } = default!;
    }
}
