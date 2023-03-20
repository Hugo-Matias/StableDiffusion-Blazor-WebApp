using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Extensions
{
    public static class Parser
    {
        public static string SanitizePath(this string path) => string.Join("_", path.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.').Trim();

        public static string NormalizePath(this string path)
        {
            return Path.GetFullPath(new Uri(path).LocalPath)
                       .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       .ToLowerInvariant();
        }

        public static string RemoveBase64Header(this string data)
        {
            if (string.IsNullOrWhiteSpace(data)) return string.Empty;
            return Regex.Replace(data, @"data.+?,", "", RegexOptions.Compiled);
        }

        public static string EscapeParenthesis(this string input)
        {
            input = input.Replace("(", @"\(");
            input = input.Replace(")", @"\)");
            return input;
        }

        public static SharedParameters ParseParameters(this SharedParameters param, IEnumerable<PromptStyle> styles)
        {
            if (styles != null && styles.Count() > 0)
            {
                foreach (var style in styles)
                {
                    param.Prompt = param.Prompt.ParsePrompt(style.Prompt);
                    param.NegativePrompt = param.NegativePrompt.ParsePrompt(style.NegativePrompt);
                }
            }
            return param;
        }

        public static string ParsePrompt(this string prompt, string style)
        {
            if (style == null) return prompt;
            if (style.Contains("{prompt}"))
            {
                return Regex.Replace(style, "{prompt}", prompt ?? "");
            }
            return $"{prompt}{style}";
        }

        public static string ParseCivitaiImageResources(this string prompt, List<CivitaiImageMetaResource> resources)
        {
            if (resources == null || prompt == null) return prompt?.Replace("\n", "");
            var comp = StringComparison.InvariantCultureIgnoreCase;
            foreach (var resource in resources)
            {
                var resourceString = string.Empty;
                if (resource.Type.Equals("lora", comp))
                    resourceString = $"<lora:{resource.Name}:{resource.Weight}>";
                else if (resource.Type.Equals("hypernet"))
                    resourceString = $"<hypernet:{resource.Name}:{resource.Weight}>";
                else if (!resource.Type.Equals("model")) Console.WriteLine($"NEW IMAGE RESOURCE TYPE FOUND: {resource.Type} | {resource.Name}");
                if (!string.IsNullOrWhiteSpace(resourceString) && !prompt.Contains(resourceString, comp)) prompt += ", " + resourceString;
            }
            return prompt.Replace("\n", "");
        }

        public static MarkupString ParseHighresFixResizeInfo(this Txt2ImgParameters param)
        {
            var currentRes = $"{param.Width}x{param.Height} px";
            var resizeRes = ParseHighresResolution((int)param.Width, (int)param.Height, param.HRWidth, param.HRHeight, param.HRScale);
            return new MarkupString($"From: {currentRes} | To: <strong>{resizeRes.Item1}x{resizeRes.Item2} px</strong>");
        }

        public static (int, int) ParseHighresResolution(this int width, int height, int hrWidth = 0, int hrHeight = 0, double scale = 0)
        {
            var ar = (float)width / height;
            if (hrWidth == 0 && hrHeight == 0)
                return ((int)(width * scale), (int)(height * scale));
            else if (hrWidth > 0 && hrHeight == 0)
                return (hrWidth, (int)(hrWidth / ar));
            else if (hrWidth == 0 && hrHeight > 0)
                return ((int)(hrHeight * ar), hrHeight);
            else
                return (hrWidth, hrHeight);
        }

        public static ImageInfo ParseImageInfoString(this ImageInfo image)
        {
            foreach (var line in image.InfoString)
            {
                if (line.StartsWith("Negative prompt:"))
                    image.NegativePrompt = line.Replace("Negative prompt: ", "");

                else if (line.StartsWith("Steps:"))
                    image.ParseImageInfoParameters(line);

                else
                    image.Prompt = line;
            }

            return image;
        }

        public static ImageInfo ParseImageInfoParameters(this ImageInfo image, string info)
        {
            image.Steps = int.Parse(Regex.Match(info, @"(Steps: )(\d+)").Groups[2].Value);
            image.Sampler = Regex.Match(info, @"(Sampler: )(.+?),").Groups[2].Value;
            image.CfgScale = float.Parse(Regex.Match(info, @"(CFG scale: )(.+?),").Groups[2].Value);
            image.Seed = long.Parse(Regex.Match(info, @"(Seed: )(\d+)").Groups[2].Value);
            var size = Regex.Match(info, @"(Size: )(\d+)x(\d+)");
            image.Width = int.Parse(size.Groups[2].Value);
            image.Height = int.Parse(size.Groups[3].Value);

            return image;
        }

        public static string ParseResizeModeValue(this int value)
        {
            switch (value)
            {
                case 1:
                    return "Crop and Resize";
                case 2:
                    return "Resize and Fill";
                case 3:
                    return "Just Resize (latent upscale)";
                default:
                    return "Just Resize";
            }
        }

        public static string ParseInpaintingFillValue(this int value)
        {
            switch (value)
            {
                case 1:
                    return "Original";
                case 2:
                    return "Latent Noise";
                case 3:
                    return "Latent Nothing";
                default:
                    return "Fill";
            }
        }

        public static CsvTag ParseCsvTag(this CsvTag tag) => new CsvTag()
        {
            Name = tag.Name.Replace("_", " "),
            Aliases = tag.Aliases.Replace("_", " "),
            Color = tag.Color,
            Uses = tag.Uses
        };

        public static string ParseCsvTagAlias(this string aliases, string search)
        {
            var aliasesList = aliases.Replace("\"", "").Split(',');
            return aliasesList.FirstOrDefault(a => a.Contains(search, StringComparison.InvariantCultureIgnoreCase));
        }

        public static Color ParseCsvTagColor(this int color)
        {
            switch (color)
            {
                case 0:
                    return Color.Info;
                case 1:
                    return Color.Error;
                case 2:
                    return Color.Warning;
                case 3:
                    return Color.Secondary;
                case 4:
                    return Color.Tertiary;
                case 5:
                    return Color.Warning;
                case 6:
                    return Color.Secondary;
                case 7:
                    return Color.Success;
                case 8:
                    return Color.Info;
                default:
                    return Color.Default;
            }
        }

        public static string ParseCivitaiResourceColorAsString(this CivitaiModelType type)
        {
            return type switch
            {
                CivitaiModelType.Checkpoint => "mud-palette-primary",
                CivitaiModelType.TextualInversion => "mud-palette-secondary",
                CivitaiModelType.Hypernetwork => "mud-palette-info",
                CivitaiModelType.AestheticGradient => "mud-palette-warning",
                CivitaiModelType.LORA => "mud-palette-success",
                CivitaiModelType.LoCon => "mud-palette-success",
                CivitaiModelType.Controlnet => "mud-palette-error",
                CivitaiModelType.Poses => "mud-palette-tertiary",
                CivitaiModelType.Wildcards => "mud-palette-tertiary",
                CivitaiModelType.Other => "mud-palette-tertiary",
                _ => "mud-palette-default",
            };
        }

        public static Color ParseCivitaiResourceColorAsColor(this CivitaiModelType type)
        {
            return type switch
            {
                CivitaiModelType.Checkpoint => Color.Primary,
                CivitaiModelType.TextualInversion => Color.Secondary,
                CivitaiModelType.Hypernetwork => Color.Info,
                CivitaiModelType.AestheticGradient => Color.Warning,
                CivitaiModelType.LORA => Color.Success,
                CivitaiModelType.LoCon => Color.Success,
                CivitaiModelType.Controlnet => Color.Error,
                CivitaiModelType.Poses => Color.Tertiary,
                CivitaiModelType.Wildcards => Color.Tertiary,
                CivitaiModelType.Other => Color.Tertiary,
                _ => Color.Default
            };
        }

        public static string ParseCivitaiImageSize(this string metaSize, int width, int height)
        {
            if (metaSize == null) return $"{width}x{height}";
            var metaWidth = int.Parse(metaSize.Split('x')[0]);
            var metaHeigth = int.Parse(metaSize.Split('x')[1]);
            if (metaWidth == width && metaHeigth == height) return metaSize;
            else return $"{metaSize} | {width}x{height}";
        }

        public static string ParseCivitaiImageFullSize(this string url, int width, string? metaSize)
        {
            var splitUrl = Regex.Matches(url, @"(.+?width=).+", RegexOptions.Compiled)[0].Groups[1].Value;
            if (!string.IsNullOrWhiteSpace(metaSize))
            {
                var parsedWidth = int.Parse(metaSize.Split("x")[0]);
                if (parsedWidth > width) width = parsedWidth;
            }
            return splitUrl + width.ToString();
        }
        public static string ParseCivitaiImageGenerationProcess(this string process)
        {
            switch (process)
            {
                case "txt2img":
                    return "Txt2Img";
                case "txt2imgHiRes":
                    return "Txt2Img (HighRes)";
                case "img2img":
                    return "Img2Img";
                case "inpainting":
                    return "Inpainting";
                default:
                    return process;
            }
        }

        public static Color ParseCivitaiImageGenerationProcessColor(this string process)
        {
            switch (process)
            {
                case "txt2img":
                    return Color.Info;
                case "txt2imgHiRes":
                    return Color.Secondary;
                case "img2img":
                    return Color.Warning;
                case "inpainting":
                    return Color.Success;
                default:
                    return Color.Primary;
            }
        }

        public static string ParseCivitaiFilesize(this double filesize)
        {
            if (filesize < 1024)
                return $"{filesize:#.##} KB";
            else if (filesize / 1024 < 1024)
                return $"{filesize / 1024:#.##} MB";
            else
                return $"{filesize / 1024 / 1024:#.##} GB";
        }

        public static CivitaiScanResult ParseCivitaiScanResult(this string result) => (CivitaiScanResult)Enum.Parse(typeof(CivitaiScanResult), result);

        public static string ParseCivitaiScanIcon(this CivitaiScanResult result)
        {
            switch (result)
            {
                case CivitaiScanResult.Success:
                    return "fa-solid fa-shield-halved";
                case CivitaiScanResult.Pending:
                    return "fa-solid fa-file-shield";
                case CivitaiScanResult.Error:
                    return "fa-solid fa-triangle-exclamation";
                case CivitaiScanResult.Danger:
                    return "fa-solid fa-shield-virus";
                default:
                    return string.Empty;
            }
        }

        public static Color ParseCivitaiScanColor(this CivitaiScanResult result)
        {
            switch (result)
            {
                case CivitaiScanResult.Success:
                    return Color.Success;
                case CivitaiScanResult.Pending:
                case CivitaiScanResult.Error:
                    return Color.Warning;
                case CivitaiScanResult.Danger:
                    return Color.Error;
                default:
                    return Color.Default;
            }
        }

        public static string ParseCivitaiScanTimespan(this DateTime scanTime)
        {
            var timeSpan = DateTime.Now - scanTime;
            if (timeSpan.Minutes < 1) return $"{timeSpan.Seconds} seconds ago";
            if (timeSpan.Hours < 1)
            {
                if (timeSpan.Minutes == 1) return "1 minute ago";
                else return $"{timeSpan.Minutes} minutes ago";
            }
            if (timeSpan.Days < 1)
            {
                if (timeSpan.Hours == 1) return "1 hour ago";
                else return $"{timeSpan.Hours} hours ago";
            }
            else
            {
                if (timeSpan.Days == 1) return "1 day ago";
                else return $"{timeSpan.Days} days ago";
            }
        }

        public static string CollapseInteger(this int number)
        {
            if (number < 1000) return number.ToString();
            else return $"{number / 1000}K";
        }
        public static string ConvertCloudMount(this string path) => path.Replace(@"H:\O meu disco\", @"Z:\");
    }
}
