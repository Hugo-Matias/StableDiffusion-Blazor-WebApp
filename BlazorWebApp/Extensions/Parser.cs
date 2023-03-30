using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Extensions
{
    public static class Parser
    {
        public static string CreateScriptParameters(this string payloadKey, ref SharedParameters parameters, BaseScriptParameters scriptParam)
        {
            if (scriptParam != null && scriptParam.IsEnabled)
            {
                var argsArray = scriptParam.GetType().GetProperties().Select(p => p.GetValue(scriptParam, null)).ToArray();
                // Since the shared ScriptParameteresBase properties are loaded last and order is important, we need to reorder them
                var tempList = argsArray.ToList();
                if (scriptParam.IsAlwaysOn)
                {
                    // Last element at this point is BaseScriptParameters.IsAlwaysOn, since we don't need the value in the payload it's just discarded
                    tempList.RemoveAt(tempList.Count - 1);
                    // Last element at this point is BaseScriptParameters.IsEnabled, the value we need move to top
                    var isEnabledValue = tempList[tempList.Count - 1];
                    tempList.RemoveAt(tempList.Count - 1);
                    tempList.Insert(0, isEnabledValue);

                    //Expand MultiDiffusion box region controls
                    if (payloadKey == "Tiled Diffusion")
                    {
                        var controls = tempList[tempList.Count - 1];
                        tempList.RemoveAt(tempList.Count - 1);
                        foreach (var control in (List<ScriptParametersMultiDiffusionBBoxControl>)controls)
                        {
                            tempList.AddRange(control.GetType().GetProperties().Select(p => p.GetValue(control, null)).ToArray());
                        }
                    }

                    argsArray = tempList.ToArray();
                    var payloadValue = new Dictionary<string, object[]>() { { "args", argsArray } };
                    if (parameters.AlwaysOnScripts == null) parameters.AlwaysOnScripts = new() { { payloadKey, payloadValue } };
                    else parameters.AlwaysOnScripts.Add(payloadKey, payloadValue);
                }
                else
                {
                    // Remove the 2 shared values from the payload since they are not needed on triggered scripts like Ultimate Upscale
                    tempList.RemoveAt(tempList.Count - 1);
                    tempList.RemoveAt(tempList.Count - 1);
                    argsArray = tempList.ToArray();
                    parameters.ScriptName = payloadKey;
                    parameters.ScriptArgs = argsArray;
                    return payloadKey;
                }
            }
            return string.Empty;
        }

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

        /// <summary>
        /// Parses the info text lines returned from the WebUI after inference.
        /// </summary>
        /// <param name="info">Info text</param>
        /// <param name="mode">ModeType to unsure that Upscale generations are properly parsed</param>
        /// <returns>Dictionary key values ["prompt", "negative", "param"] </returns>
        public static Dictionary<string, string>? ParseInfoStrings(this string info, ModeType mode)
        {
            if (string.IsNullOrWhiteSpace(info)) return null;

            var prompt = string.Empty;
            var negative = string.Empty;
            var param = string.Empty;

            if (mode == ModeType.Extras) param = info;
            else
            {
                var lines = info.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("Negative prompt:", StringComparison.InvariantCultureIgnoreCase)) negative = Regex.Replace(line, @"^Negative prompt: ", "");
                    else if (line.StartsWith("Steps: ", StringComparison.InvariantCultureIgnoreCase)) param = line;
                    else if (!string.IsNullOrWhiteSpace(line)) prompt = line;
                }
            }
            return new Dictionary<string, string>() { { "prompt", prompt }, { "negative", negative }, { "param", param } };
        }

        public static Dictionary<string, string>? ParseInfoParameters(this string param)
        {
            if (string.IsNullOrWhiteSpace(param)) return null;

            Dictionary<string, string>? parameters = new();
            // Adding ", " to comply with the pattern
            var groups = Regex.Matches(param + ", ", @"((.+?): ([^"",\n]*|""([^""]*|"")*""), )");
            foreach (Match group in groups)
            {
                if (group.Groups.Count != 5) Console.WriteLine($"[ImageService:ParseInfoParameters] Incorrect group match: {group.Value} | {group.Groups.Count}");
                var key = group.Groups[2].Value;
                var value = group.Groups[3].Value;
                parameters.Add(key, value);
            }
            return parameters;
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
            return value switch
            {
                1 => "Crop and Resize",
                2 => "Resize and Fill",
                3 => "Just Resize (latent upscale)",
                _ => "Just Resize",
            };
        }

        public static string ParseInpaintingFillValue(this int value)
        {
            return value switch
            {
                1 => "Original",
                2 => "Latent Noise",
                3 => "Latent Nothing",
                _ => "Fill",
            };
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
            return color switch
            {
                0 => Color.Info,
                1 => Color.Error,
                2 => Color.Warning,
                3 => Color.Secondary,
                4 => Color.Tertiary,
                5 => Color.Warning,
                6 => Color.Secondary,
                7 => Color.Success,
                8 => Color.Info,
                _ => Color.Default,
            };
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
            return process switch
            {
                "txt2img" => "Txt2Img",
                "txt2imgHiRes" => "Txt2Img (HighRes)",
                "img2img" => "Img2Img",
                "inpainting" => "Inpainting",
                _ => process,
            };
        }

        public static Color ParseCivitaiImageGenerationProcessColor(this string process)
        {
            return process switch
            {
                "txt2img" => Color.Info,
                "txt2imgHiRes" => Color.Secondary,
                "img2img" => Color.Warning,
                "inpainting" => Color.Success,
                _ => Color.Primary,
            };
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
            return result switch
            {
                CivitaiScanResult.Success => "fa-solid fa-shield-halved",
                CivitaiScanResult.Pending => "fa-solid fa-file-shield",
                CivitaiScanResult.Error => "fa-solid fa-triangle-exclamation",
                CivitaiScanResult.Danger => "fa-solid fa-shield-virus",
                _ => string.Empty,
            };
        }

        public static Color ParseCivitaiScanColor(this CivitaiScanResult result)
        {
            return result switch
            {
                CivitaiScanResult.Success => Color.Success,
                CivitaiScanResult.Pending or CivitaiScanResult.Error => Color.Warning,
                CivitaiScanResult.Danger => Color.Error,
                _ => Color.Default,
            };
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

        public static string ConvertCloudMount(this string path) => path.Replace(@"Z:\", @"H:\O meu disco\");

        public static ModeType ModeTypeFromOutdir(this Outdir outdir)
        {
            return outdir switch
            {
                Outdir.Txt2ImgSamples => ModeType.Txt2Img,
                Outdir.Txt2ImgGrid => ModeType.Txt2Img,
                Outdir.Img2ImgSamples => ModeType.Img2Img,
                Outdir.Img2ImgGrid => ModeType.Img2Img,
                Outdir.Extras => ModeType.Extras,
            };
        }
    }
}
