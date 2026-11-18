using BlazorWebApp.Data.Dtos.HuggingFace;
using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Infers Type, SubType, and BaseModel hints from HuggingFace metadata.
    /// All results are suggestions only — the user must confirm before download.
    /// </summary>
    public static class HuggingFaceModelMapper
    {
        // Known pipeline tags that map to image-generation relevant types
        private static readonly HashSet<string> CheckpointPipelines = new(StringComparer.OrdinalIgnoreCase)
        {
            "text-to-image",
            "image-to-image",
            "inpainting",
            "image-to-video"
        };

        private static readonly HashSet<string> LoRAPipelines = new(StringComparer.OrdinalIgnoreCase)
        {
            "text-to-image",
            "image-to-image"
        };

        // Tags that hint strongly at a specific type
        private static readonly Dictionary<string, string> TagToTypeHint = new(StringComparer.OrdinalIgnoreCase)
        {
            ["lora"] = "LORA",
            ["locon"] = "LoCon",
            ["dora"] = "DoRA",
            ["textual-inversion"] = "TextualInversion",
            ["embedding"] = "TextualInversion",
            ["controlnet"] = "Controlnet",
            ["vae"] = "VAE",
            ["upscaler"] = "Upscaler",
            ["motion-module"] = "MotionModule",
            ["diffusers"] = "Checkpoint",
            ["gguf"] = "Checkpoint",
            ["safetensors"] = "Checkpoint",
        };

        // Base model hints derived from common tag/repo patterns
        private static readonly Dictionary<string, string> BaseModelHintMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["flux"] = "Flux",
            ["flux.1"] = "Flux",
            ["flux-dev"] = "Flux",
            ["flux-schnell"] = "Flux",
            ["flux1"] = "Flux",
            ["sdxl"] = "SDXL 1.0",
            ["sd-xl"] = "SDXL 1.0",
            ["sdxl-base"] = "SDXL 1.0",
            ["stable-diffusion-xl"] = "SDXL 1.0",
            ["pony"] = "Pony",
            ["ponyxl"] = "Pony",
            ["sd15"] = "SD 1.5",
            ["sd1.5"] = "SD 1.5",
            ["stable-diffusion-v1-5"] = "SD 1.5",
            ["sd2"] = "SD 2.1",
            ["sd-2"] = "SD 2.1",
            ["stable-diffusion-2"] = "SD 2.1",
            ["kolors"] = "Kolors",
            ["hidream"] = "HiDream",
            ["wan2.1"] = "Wan2.1",
            ["wan"] = "Wan2.1",
            ["hunyuan"] = "Hunyuan Video",
            ["cogvideox"] = "CogVideoX",
            ["ltx-video"] = "LTX-Video",
            ["mochi"] = "Mochi",
            ["pixart-sigma"] = "PixArt-Σ",
        };

        // Extension hints for subtype
        private static readonly Dictionary<string, string> ExtensionToSubTypeHint = new(StringComparer.OrdinalIgnoreCase)
        {
            [".safetensors"] = "SafeTensor",
            [".gguf"] = "GGUF",
            [".bin"] = "Other",
            [".pt"] = "Other",
            [".ckpt"] = "Checkpoint",
        };

        /// <summary>
        /// Infer Type, SubType, and BaseModel from HF model summary and a selected file path.
        /// Returns suggestions with <see cref="HuggingFaceMappingHints.IsConfident"/> indicating
        /// whether we have enough signal to pre-populate controls with reasonable confidence.
        /// </summary>
        public static HuggingFaceMappingHints InferHints(
            HuggingFaceModelSummaryDto model,
            string? selectedFilePath = null)
        {
            var hints = new HuggingFaceMappingHints();
            var tags = model.Tags ?? new List<string>();
            var pipeline = model.PipelineTag ?? string.Empty;

            // --- Type hint ---
            hints.TypeHint = InferType(tags, pipeline, selectedFilePath);
            hints.SubTypeHint = InferSubType(tags, selectedFilePath);
            hints.BaseModelHint = InferBaseModel(model, tags);

            // --- Confidence ---
            hints.IsConfident = !string.IsNullOrEmpty(hints.TypeHint)
                && !string.IsNullOrEmpty(hints.BaseModelHint);

            return hints;
        }

        /// <summary>
        /// Builds a <see cref="Resource"/> pre-populated with available HF metadata.
        /// Type and SubType must be confirmed and set by the caller before persisting.
        /// </summary>
        public static Resource BuildResource(
            HuggingFaceModelSummaryDto model,
            string localFilename,
            string repoFilePath,
            string revision,
            double sizeKb,
            string? sha = null)
        {
            return new Resource
            {
                Title = model.DisplayName,
                Filename = localFilename,
                Author = model.ResolvedAuthor,
                HuggingFaceModelId = model.Id,
                HuggingFaceRevision = revision,
                HuggingFaceFilePath = repoFilePath,
                HuggingFaceSha = sha,
                Tags = model.Tags,
                Description = null, // populated from README separately
                SizeKb = sizeKb,
                IsEnabled = false,
                CreatedDate = DateTime.Now,
                // Type and SubType must be set by the caller
                Type = new ResourceType { Name = string.Empty },
            };
        }

        // -------------------------------------------------------------------------
        // Private inference helpers
        // -------------------------------------------------------------------------

        private static string? InferType(IEnumerable<string> tags, string pipeline, string? filePath)
        {
            // File extension can be a stronger signal than tags for some types
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext == ".gguf") return "Checkpoint";
            }

            foreach (var tag in tags)
            {
                if (TagToTypeHint.TryGetValue(tag, out var typeHint))
                    return typeHint;
            }

            // Fall back to pipeline tag
            if (CheckpointPipelines.Contains(pipeline))
                return "Checkpoint";

            return null;
        }

        private static string? InferSubType(IEnumerable<string> tags, string? filePath)
        {
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ExtensionToSubTypeHint.TryGetValue(ext, out var extHint))
                    return extHint;
            }

            foreach (var tag in tags)
            {
                if (tag.Equals("gguf", StringComparison.OrdinalIgnoreCase)) return "GGUF";
                if (tag.Equals("safetensors", StringComparison.OrdinalIgnoreCase)) return "SafeTensor";
            }

            return null;
        }

        private static string? InferBaseModel(HuggingFaceModelSummaryDto model, IEnumerable<string> tags)
        {
            // 1. card_data base_model field is the most explicit
            var cardBase = model.CardData?.BaseModelString;
            if (!string.IsNullOrWhiteSpace(cardBase))
            {
                // Try to map the card base_model repo id to a display name
                foreach (var (key, value) in BaseModelHintMap)
                {
                    if (cardBase.Contains(key, StringComparison.OrdinalIgnoreCase))
                        return value;
                }
                // Return the raw card value if no mapping found — still more useful than null
                return cardBase;
            }

            // 2. Tags
            foreach (var tag in tags)
            {
                if (BaseModelHintMap.TryGetValue(tag, out var hint))
                    return hint;
            }

            // 3. Repo id / display name
            var repoName = model.Id ?? string.Empty;
            foreach (var (key, value) in BaseModelHintMap)
            {
                if (repoName.Contains(key, StringComparison.OrdinalIgnoreCase))
                    return value;
            }

            return null;
        }
    }

    /// <summary>Suggested classification values inferred from HuggingFace metadata.</summary>
    public class HuggingFaceMappingHints
    {
        public string? TypeHint { get; set; }
        public string? SubTypeHint { get; set; }
        public string? BaseModelHint { get; set; }

        /// <summary>
        /// True when both TypeHint and BaseModelHint are non-empty,
        /// indicating enough signal to pre-populate the mapping form.
        /// </summary>
        public bool IsConfident { get; set; }
    }
}
