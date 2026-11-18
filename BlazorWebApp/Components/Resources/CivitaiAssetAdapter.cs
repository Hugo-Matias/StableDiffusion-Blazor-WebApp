using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using ImageEntity = BlazorWebApp.Data.Entities.Image;

namespace BlazorWebApp.Components.Resources
{
    /// <summary>
    /// Projects <see cref="CivitaiImageDto"/> instances into transient <see cref="ImageEntity"/>
    /// objects suitable for consumption by <c>AssetViewer</c> / <c>AssetInfoPanel</c>.
    ///
    /// <para>
    /// The resulting entities are <b>transient</b>: they are never attached to <c>AppDbContext</c>
    /// and must not be persisted. <c>Id</c> stays at its default (0) so that <c>AssetViewer</c>'s
    /// favorite / score controls (gated on <c>Id &gt; 0</c>) automatically suppress for remote
    /// assets, in addition to the explicit <c>ShowFavorite</c> / <c>ShowScore</c> opt-outs.
    /// </para>
    ///
    /// <para>Field mapping (CivitAI -&gt; ImageEntity):</para>
    /// <list type="bullet">
    ///   <item><description><c>Url</c> -&gt; <c>Path</c> (URL string; <c>AssetViewer</c> detects video by extension)</description></item>
    ///   <item><description><c>Width</c>, <c>Height</c> -&gt; same</description></item>
    ///   <item><description><c>Meta.Prompt</c> -&gt; <c>Prompt</c></description></item>
    ///   <item><description><c>Meta.NegativePrompt</c> -&gt; <c>NegativePrompt</c></description></item>
    ///   <item><description><c>Meta.Seed</c> -&gt; <c>Seed</c></description></item>
    ///   <item><description><c>Meta.Steps</c> -&gt; <c>Steps</c></description></item>
    ///   <item><description><c>Meta.CfgScale</c> -&gt; <c>CfgScale</c></description></item>
    ///   <item><description><c>Meta.Sampler</c> (name) -&gt; <c>Scheduler</c> (string passthrough; <c>SamplerId</c> stays 0). The receiving workflow resolves the sampler by name on send, mirroring the Danbooru integration.</description></item>
    ///   <item><description><c>Meta.DenoisingStrength</c> -&gt; <c>DenoisingStrength</c> (parsed when numeric)</description></item>
    /// </list>
    ///
    /// <para>Lossy / unmapped fields:</para>
    /// <list type="bullet">
    ///   <item><description><c>Meta.Model</c> / <c>Meta.ModelHash</c>: not mapped (no transient <c>Resource</c> link). The receiving workflow may resolve via hash.</description></item>
    ///   <item><description><c>Meta.ClipSkip</c>, hires fields, face restoration: not exposed by <c>ImageEntity</c>.</description></item>
    ///   <item><description><c>Meta.Resources</c>: not mapped (LoRA / embeds are workflow-specific).</description></item>
    /// </list>
    /// </summary>
    public static class CivitaiAssetAdapter
    {
        private static readonly string[] VideoExtensions = { ".mp4", ".webm", ".mov", ".avi", ".mkv" };

        /// <summary>
        /// Projects a single <see cref="CivitaiImageDto"/> into a transient <see cref="ImageEntity"/>.
        /// </summary>
        public static ImageEntity Project(CivitaiImageDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var image = new ImageEntity
            {
                // Id intentionally left at 0 (transient).
                Path = dto.Url ?? string.Empty,
                Width = dto.Width,
                Height = dto.Height,
            };

            var meta = dto.Meta;
            if (meta != null)
            {
                image.Prompt = meta.Prompt;
                image.NegativePrompt = meta.NegativePrompt;
                image.Seed = meta.Seed != 0 ? meta.Seed : -1;
                image.Steps = meta.Steps > 0 ? meta.Steps : -1;
                image.CfgScale = meta.CfgScale > 0 ? meta.CfgScale : -1;
                image.Scheduler = !string.IsNullOrWhiteSpace(meta.Scheduler) ? meta.Scheduler : meta.Sampler;
                image.Width = image.Width > 0 ? image.Width : meta.Width;
                image.Height = image.Height > 0 ? image.Height : meta.Height;

                if (!string.IsNullOrWhiteSpace(meta.DenoisingStrength)
                    && double.TryParse(meta.DenoisingStrength, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var denoise))
                {
                    image.DenoisingStrength = denoise;
                }
            }

            return image;
        }

        public static ImageEntity Project(CivitaiImageDto dto, ResourceImage resourceImage)
        {
            if (resourceImage == null) throw new ArgumentNullException(nameof(resourceImage));

            var image = Project(dto);
            ApplyResourceImage(image, resourceImage);
            return image;
        }

        public static void ApplyResourceImage(ImageEntity image, ResourceImage resourceImage)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (resourceImage == null) throw new ArgumentNullException(nameof(resourceImage));

            image.Path = resourceImage.Path;
            image.Prompt = !string.IsNullOrWhiteSpace(resourceImage.Prompt) ? resourceImage.Prompt : image.Prompt;
            image.NegativePrompt = !string.IsNullOrWhiteSpace(resourceImage.NegativePrompt) ? resourceImage.NegativePrompt : image.NegativePrompt;
            image.Seed = resourceImage.Seed ?? image.Seed;
            image.Steps = resourceImage.Steps ?? image.Steps;
            image.CfgScale = resourceImage.CfgScale ?? image.CfgScale;
            image.Scheduler = !string.IsNullOrWhiteSpace(resourceImage.Sampler) ? resourceImage.Sampler : image.Scheduler;
            image.Width = resourceImage.Width.GetValueOrDefault() > 0 ? resourceImage.Width.GetValueOrDefault() : image.Width;
            image.Height = resourceImage.Height.GetValueOrDefault() > 0 ? resourceImage.Height.GetValueOrDefault() : image.Height;

            if (!string.IsNullOrWhiteSpace(resourceImage.DenoisingStrength)
                && double.TryParse(resourceImage.DenoisingStrength, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var denoise))
            {
                image.DenoisingStrength = denoise;
            }
        }

        /// <summary>
        /// Projects a sequence of <see cref="CivitaiImageDto"/> into a list of transient
        /// <see cref="ImageEntity"/> objects, preserving order.
        /// </summary>
        public static List<ImageEntity> Project(IEnumerable<CivitaiImageDto> dtos)
        {
            if (dtos == null) throw new ArgumentNullException(nameof(dtos));
            var result = new List<ImageEntity>();
            foreach (var dto in dtos)
            {
                if (dto == null) continue;
                result.Add(Project(dto));
            }
            return result;
        }

        /// <summary>
        /// Returns true when the URL points to a video asset. Mirrors the extension-based
        /// detection used by <c>AssetViewer</c> so call sites can pre-flag video DTOs.
        /// </summary>
        public static bool IsVideoUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            var lower = url.ToLowerInvariant();
            foreach (var ext in VideoExtensions)
            {
                if (lower.Contains(ext)) return true;
            }
            return false;
        }

        public static bool IsVideo(CivitaiImageDto? dto)
            => dto?.ImageType is { Length: > 0 } && dto.ImageType[0] == 0
                || string.Equals(dto?.Type, "video", StringComparison.OrdinalIgnoreCase)
                || IsVideoUrl(dto?.Url);
    }
}
