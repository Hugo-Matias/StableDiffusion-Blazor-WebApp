using BlazorWebApp.Scheduler.Directives;
using BlazorWebApp.Scheduler.Targets;
using BlazorWebApp.Scheduler.Variations;
using MudBlazor;

namespace BlazorWebApp.Scheduler
{
    /// <summary>
    /// Shared card-formatting helpers used by the Scheduler editor UI and by the read-only
    /// run parameters dialog. Keeps the "what does this directive / variation say?" text
    /// consistent in both places.
    /// </summary>
    public static class SchedulerCardFormatting
    {
        public static string DirectiveTitle(Directive d) => d switch
        {
            SetValueDirective => "Set Value",
            AppendPromptDirective => "Append Prompt",
            ReplacePromptDirective => "Replace Prompt",
            AddLoraDirective => "Add LoRA",
            RemoveLoraDirective => "Remove LoRA",
            ToggleLoraDirective => "Toggle LoRA",
            AddPromptStyleDirective => "Add Prompt Style",
            SwapAssetDirective => "Swap Asset",
            SetOutputDirective => "Set Output",
            _ => d.GetType().Name,
        };

        public static string DirectiveIcon(Directive d) => d switch
        {
            SetValueDirective => Icons.Material.Filled.Edit,
            AppendPromptDirective => Icons.Material.Filled.AddComment,
            ReplacePromptDirective => Icons.Material.Filled.FindReplace,
            AddLoraDirective => Icons.Material.Filled.AddCircle,
            RemoveLoraDirective => Icons.Material.Filled.RemoveCircle,
            ToggleLoraDirective => Icons.Material.Filled.ToggleOn,
            AddPromptStyleDirective => Icons.Material.Filled.Palette,
            SwapAssetDirective => Icons.Material.Filled.SwapHoriz,
            SetOutputDirective => Icons.Material.Filled.FolderSpecial,
            _ => Icons.Material.Filled.Settings,
        };

        public static string VariationTitle(Variation v) => v switch
        {
            ListVariation => "List",
            RangeVariation => "Range",
            RandomVariation => "Random",
            WildcardVariation => "Wildcard",
            LlmVariation => "LLM",
            SearchReplaceVariation => "Search / Replace",
            ToggleVariation => "Toggle",
            _ => v.GetType().Name,
        };

        public static string VariationIcon(Variation v) => v switch
        {
            ListVariation => Icons.Material.Filled.FormatListBulleted,
            RangeVariation => Icons.Material.Filled.Straighten,
            RandomVariation => Icons.Material.Filled.Shuffle,
            WildcardVariation => Icons.Material.Filled.Casino,
            LlmVariation => Icons.Material.Filled.SmartToy,
            SearchReplaceVariation => Icons.Material.Filled.FindReplace,
            ToggleVariation => Icons.Material.Filled.ToggleOn,
            _ => Icons.Material.Filled.Science,
        };

        public static string TargetLabel(ParameterTarget? t) => t switch
        {
            FragmentTarget f => $"{f.FragmentId}.{f.ParamKey}",
            LoraTarget l => $"LoRA: {l.LoraName}",
            AssetTarget a => $"Asset: {a.AssetKey}",
            PromptTarget p => $"Prompt ({(p.IsNegative ? "negative" : "positive")}, {p.Mode.ToString().ToLowerInvariant()})",
            OutputTarget o => $"Output: {o.Field}",
            null => "(no target)",
            _ => t.GetType().Name,
        };

        public static string DirectiveSummary(Directive d) => d switch
        {
            SetValueDirective s => $"{TargetLabel(s.Target)} = {FormatValue(s.Value)}",
            AppendPromptDirective a => $"{(a.IsPrefix ? "prepend" : "append")} to {(a.IsNegative ? "negative" : "positive")}: \"{Truncate(a.Text, 60)}\"",
            ReplacePromptDirective r => $"in {(r.IsNegative ? "negative" : "positive")}: \"{Truncate(r.Search, 30)}\" -> \"{Truncate(r.Replace, 30)}\"",
            AddLoraDirective l => $"{(l.Lora?.Name ?? "(unset)")} @ {l.Lora?.Strength.ToString("0.##") ?? "1"}",
            RemoveLoraDirective l => $"{l.LoraName}",
            ToggleLoraDirective l => $"{l.LoraName} -> {(l.Enable ? "enabled" : "disabled")}",
            AddPromptStyleDirective s => s.StyleNames is { Count: > 0 } ? string.Join(", ", s.StyleNames) : "(no styles)",
            SwapAssetDirective a => $"{a.AssetKey} -> {Truncate(a.AssetValue, 50)}",
            SetOutputDirective o => $"project -> {o.ProjectName ?? "(job default)"}",
            _ => d.GetType().Name,
        };

        public static string VariationSummary(Variation v) => v switch
        {
            ListVariation l => $"{TargetLabel(l.Target)} = [{string.Join(", ", (l.Values ?? new()).Take(3).Select(x => Truncate(x?.ToString(), 15)))}{(l.Values?.Count > 3 ? ", ..." : "")}] ({l.Values?.Count ?? 0} values)",
            RangeVariation r => $"{TargetLabel(r.Target)} = {r.Start}..{r.End} step {r.Step}{(r.IsInteger ? " (int)" : "")}",
            RandomVariation r => $"{TargetLabel(r.Target)} = [{r.Min}..{r.Max}] x {r.Count}{(r.IsInteger ? " (int)" : "")}{(r.Seed is int s ? $", seed {s}" : "")}",
            WildcardVariation w => $"{TargetLabel(w.Target)} <- {(string.IsNullOrEmpty(w.CollectionName) ? "(no collection)" : w.CollectionName)}{(w.Count is int c ? $" x {c}" : "")}",
            LlmVariation ll => $"{TargetLabel(ll.Target)} <- {(string.IsNullOrEmpty(ll.ModelName) ? "(no model)" : ll.ModelName)} x {ll.Count}",
            SearchReplaceVariation sr => $"{(sr.IsNegative ? "negative" : "positive")}: \"{Truncate(sr.Search, 20)}\" -> {sr.Replacements?.Count ?? 0} option(s)",
            ToggleVariation t => $"{TargetLabel(t.Target)} = {FormatValue(t.OnValue)} / {FormatValue(t.OffValue)}",
            _ => v.GetType().Name,
        };

        public static string FormatValue(object? v) => v switch
        {
            null => "(null)",
            string s => $"\"{Truncate(s, 40)}\"",
            _ => Truncate(v.ToString(), 40),
        };

        public static string Truncate(string? s, int max)
            => string.IsNullOrEmpty(s) ? "" : (s!.Length <= max ? s : s.Substring(0, max) + "...");
    }
}
