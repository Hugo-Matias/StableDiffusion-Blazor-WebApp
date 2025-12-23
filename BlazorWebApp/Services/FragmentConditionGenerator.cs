using System.Text.Json;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Build-time tool to auto-generate and validate fragment conditions.
    /// Run this during development to ensure all fragments have correct conditions.
    /// </summary>
    public class FragmentConditionGenerator
    {
        private readonly string _fragmentsPath;
        private readonly ILogger<FragmentConditionGenerator> _logger;

        public FragmentConditionGenerator(string workflowPath, ILogger<FragmentConditionGenerator> logger)
        {
            _fragmentsPath = Path.Combine(workflowPath, "Fragments");
            _logger = logger;
        }

        /// <summary>
        /// Scans all fragments and generates/validates conditions.
        /// Returns a report of issues found.
        /// </summary>
        public FragmentConditionReport GenerateAndValidate()
        {
            var report = new FragmentConditionReport();
            var fragmentFiles = Directory.GetFiles(_fragmentsPath, "*.sbn", SearchOption.AllDirectories);

            foreach (var fragmentFile in fragmentFiles)
            {
                // Skip utility fragments
                if (fragmentFile.Contains("utils"))
                    continue;

                var fragmentId = FragmentConditionValidator.NormalizeFragmentId(Path.GetFileName(fragmentFile));
                var fragmentContent = File.ReadAllText(fragmentFile);

                var analysis = AnalyzeFragment(fragmentFile, fragmentId, fragmentContent);
                report.FragmentAnalyses.Add(analysis);

                if (analysis.HasIssues)
                {
                    report.TotalIssues += analysis.Issues.Count;
                }
            }

            return report;
        }

        /// <summary>
        /// Analyzes a single fragment for condition issues.
        /// </summary>
        private FragmentAnalysis AnalyzeFragment(string filePath, string fragmentId, string content)
        {
            var analysis = new FragmentAnalysis
            {
                FilePath = filePath,
                FragmentId = fragmentId
            };

            // Extract #meta block
            var metaMatch = Regex.Match(content, @"#meta\s*([\s\S]*?)\s*#end");
            if (!metaMatch.Success)
            {
                analysis.Issues.Add("No #meta block found");
                return analysis;
            }

            var metaJson = metaMatch.Groups[1].Value.Trim();

            // Check if it's a scoped fragment (has {{ if scope }} in outputs)
            var isScoped = metaJson.Contains("{{ if scope }}");

            // Check for conditional conditions (scoped fragments)
            var hasConditionalCondition = metaJson.Contains("{{~ if scope");

            if (isScoped)
            {
                analysis.IsScoped = true;
                analysis.SuggestedCondition = "Conditional based on scope parameter";

                if (!hasConditionalCondition)
                {
                    analysis.Issues.Add("Scoped fragment should have conditional condition check");
                    analysis.SuggestedFix = GenerateScopedConditionTemplate();
                }
            }
            else
            {
                // Non-scoped fragment - should have explicit condition
                var hasCondition = metaJson.Contains("\"conditions\"");

                if (!hasCondition)
                {
                    // Check if it's a core fragment that should always be active
                    if (IsCoreFragment(fragmentId))
                    {
                        analysis.IsCore = true;
                        analysis.SuggestedCondition = "Core fragment - always active (no condition needed)";
                    }
                    else
                    {
                        analysis.Issues.Add($"Optional fragment missing condition");
                        analysis.SuggestedCondition = $"\"{fragmentId}.IsActive\"";
                        analysis.SuggestedFix = GenerateConditionTemplate(fragmentId);
                    }
                }
                else
                {
                    // Validate existing condition
                    try
                    {
                        var conditionErrors = ValidateExistingCondition(metaJson, fragmentId);
                        analysis.Issues.AddRange(conditionErrors);
                    }
                    catch (Exception ex)
                    {
                        analysis.Issues.Add($"Failed to parse conditions: {ex.Message}");
                    }
                }
            }

            return analysis;
        }

        /// <summary>
        /// Validates conditions in existing JSON metadata.
        /// </summary>
        private List<string> ValidateExistingCondition(string metaJson, string expectedFragmentId)
        {
            var errors = new List<string>();

            // Try to extract condition paths using regex
            var conditionMatch = Regex.Match(metaJson, @"""required"":\s*\[(.*?)\]", RegexOptions.Singleline);
            if (conditionMatch.Success)
            {
                var conditionsStr = conditionMatch.Groups[1].Value;
                var conditionPaths = Regex.Matches(conditionsStr, @"""([^""]+)""");

                foreach (Match match in conditionPaths)
                {
                    var conditionPath = match.Groups[1].Value;
                    
                    // Check if it's a simple parameter condition (no dot)
                    if (!conditionPath.Contains('.'))
                    {
                        // Parameter-based condition - check casing
                        if (conditionPath != conditionPath.ToLowerInvariant())
                        {
                            errors.Add($"Parameter condition '{conditionPath}' should be lowercase snake_case");
                        }
                        continue;
                    }
                    
                    // Parse fragment.Property condition
                    var parts = conditionPath.Split('.');
                    if (parts.Length != 2)
                    {
                        errors.Add($"Invalid condition format: '{conditionPath}'");
                        continue;
                    }

                    var fragmentInCondition = parts[0];
                    
                    // Check casing
                    if (fragmentInCondition != fragmentInCondition.ToLowerInvariant())
                    {
                        errors.Add($"Condition uses wrong case: '{fragmentInCondition}' should be '{fragmentInCondition.ToLowerInvariant()}'");
                    }

                    // Check if it uses dash instead of underscore
                    if (fragmentInCondition.Contains('-'))
                    {
                        errors.Add($"Condition uses dashes: '{fragmentInCondition}' should use underscores");
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Checks if a fragment is a core fragment that should always be active.
        /// </summary>
        private bool IsCoreFragment(string fragmentId)
        {
            var coreFragments = new[]
            {
                // Core workflow fragments
                "prompts",
                "empty_latent",
                "latent",
                "load_checkpoint",
                "load_diffusion",
                "load_clip_vision",
                "sampler",
                "main_sampler",
                
                // Output/utility fragments
                "save",
                "save_video",
                "vae_decode",
                "vae_encode",
                "clean_vram",
                
                // Basic loaders (always needed in their contexts)
                "load_image",
                "load_video",
                "lora_loader"
            };

            return coreFragments.Contains(fragmentId);
        }

        /// <summary>
        /// Generates a condition template for a fragment.
        /// </summary>
        private string GenerateConditionTemplate(string fragmentId)
        {
            return $@"  ""conditions"": {{
    ""required"": [""{fragmentId}.IsActive""]
  }}";
        }

        /// <summary>
        /// Generates a scoped condition template.
        /// </summary>
        private string GenerateScopedConditionTemplate()
        {
            return @"  }}{{~ if scope && scope | string.contains ""parent_fragment_id"" ~}},
  ""conditions"": {
    ""required"": [""parent_fragment_id.IsActive""]
  }{{~ end ~}}";
        }
    }

    public class FragmentConditionReport
    {
        public List<FragmentAnalysis> FragmentAnalyses { get; set; } = new();
        public int TotalIssues { get; set; }
        public bool HasIssues => TotalIssues > 0;

        public string GenerateReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("Fragment Condition Validation Report");
            report.AppendLine("====================================");
            report.AppendLine();

            if (!HasIssues)
            {
                report.AppendLine("? All fragments have valid conditions!");
                return report.ToString();
            }

            report.AppendLine($"Found {TotalIssues} issues across {FragmentAnalyses.Count(a => a.HasIssues)} fragments:\n");

            foreach (var analysis in FragmentAnalyses.Where(a => a.HasIssues))
            {
                report.AppendLine($"Fragment: {Path.GetFileName(analysis.FilePath)}");
                report.AppendLine($"  ID: {analysis.FragmentId}");
                
                if (analysis.IsScoped)
                    report.AppendLine("  Type: Scoped");
                if (analysis.IsCore)
                    report.AppendLine("  Type: Core");

                report.AppendLine("  Issues:");
                foreach (var issue in analysis.Issues)
                {
                    report.AppendLine($"    - {issue}");
                }

                if (!string.IsNullOrEmpty(analysis.SuggestedCondition))
                {
                    report.AppendLine($"  Suggested Condition: {analysis.SuggestedCondition}");
                }

                if (!string.IsNullOrEmpty(analysis.SuggestedFix))
                {
                    report.AppendLine("  Suggested Fix:");
                    report.AppendLine(analysis.SuggestedFix.Replace("\n", "\n    "));
                }

                report.AppendLine();
            }

            return report.ToString();
        }
    }

    public class FragmentAnalysis
    {
        public string FilePath { get; set; } = string.Empty;
        public string FragmentId { get; set; } = string.Empty;
        public bool IsScoped { get; set; }
        public bool IsCore { get; set; }
        public List<string> Issues { get; set; } = new();
        public string? SuggestedCondition { get; set; }
        public string? SuggestedFix { get; set; }
        public bool HasIssues => Issues.Count > 0;
    }
}
