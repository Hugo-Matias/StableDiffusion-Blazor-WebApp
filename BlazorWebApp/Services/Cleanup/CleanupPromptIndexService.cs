using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services.Cleanup
{
    public class CleanupPromptIndexService : ICleanupPromptIndexService
    {
        private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex LoraRegex = new(@"<\s*(lora|lyco|hypernet)\s*:\s*([^:>]+)(?::[^>]+)*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex WeightedTokenRegex = new(@"[\(\[]+\s*([^\(\)\[\]]+?)\s*:\s*[-+]?\d+(?:\.\d+)?\s*[\)\]]+", RegexOptions.Compiled);
        private static readonly Regex EmphasisRegex = new(@"[\(\)\[\]{}]", RegexOptions.Compiled);

        public CleanupPromptIndex BuildPromptIndex(string? prompt)
        {
            var normalized = NormalizePrompt(prompt);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return new CleanupPromptIndex();
            }

            return new CleanupPromptIndex
            {
                NormalizedPrompt = normalized,
                Fingerprint = ComputeFingerprint(normalized),
                TokenSignature = BuildTokenSignature(normalized)
            };
        }

        private static string NormalizePrompt(string? prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return string.Empty;
            }

            var normalized = prompt.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
            normalized = normalized.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            normalized = LoraRegex.Replace(normalized, match => $"{match.Groups[1].Value} {NormalizeToken(match.Groups[2].Value)}");

            for (var pass = 0; pass < 4; pass++)
            {
                var updated = WeightedTokenRegex.Replace(normalized, "$1");
                if (updated == normalized)
                {
                    break;
                }

                normalized = updated;
            }

            normalized = EmphasisRegex.Replace(normalized, " ");

            var tokens = normalized
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeToken)
                .Where(token => !string.IsNullOrWhiteSpace(token));

            return string.Join(", ", tokens);
        }

        private static string NormalizeToken(string token)
        {
            var normalized = token.Replace('_', ' ').Trim();
            normalized = WhitespaceRegex.Replace(normalized, " ");
            return normalized.Trim(' ', ',', ';');
        }

        private static string ComputeFingerprint(string normalizedPrompt)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPrompt));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string? BuildTokenSignature(string normalizedPrompt)
        {
            var tokens = normalizedPrompt
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeToken)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

            return tokens.Length == 0 ? null : string.Join("|", tokens);
        }
    }
}