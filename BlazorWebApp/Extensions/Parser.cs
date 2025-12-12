using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Dtos.ComfyUI.Workflow;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Extensions
{
    public static class Parser
    {
        public static string CreateScriptParameters(this string payloadKey, ref SharedParameters parameters, BaseScriptParameters scriptParam, bool ignoreBaseParam = false)
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
                    // Last element at this point is BaseScriptParameters.IsEnabled, the value we need move to top or remove
                    if (ignoreBaseParam) tempList.RemoveAt(tempList.Count - 1);
                    else
                    {
                        var isEnabledValue = tempList[tempList.Count - 1];
                        tempList.RemoveAt(tempList.Count - 1);
                        tempList.Insert(0, isEnabledValue);
                    }

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
            param.Prompt = param.Prompt.ParseStyles(styles.Where(s => !string.IsNullOrWhiteSpace(s.Prompt)).ToList(), false);
            param.NegativePrompt = param.NegativePrompt.ParseStyles(styles.Where(s => !string.IsNullOrWhiteSpace(s.NegativePrompt)).ToList(), true);

            (var prompt, var negative) = param.Loras.ParseLoras();
            param.Prompt += prompt;
            param.NegativePrompt += negative;

            if (param.Seed == -1) param.Seed = new Random().Next(0, int.MaxValue);
            return param;
        }

        public static void ParseDetailerModelLoras(this ScriptParametersADetailer detailer)
        {
            void ParseModelPrompts(ScriptParametersADetailerModel model)
            {
                if (model == null) return;
                (var prompt, var negative) = model.Loras != null && model.Loras.Count > 0 ? model.Loras.ParseLoras() : (string.Empty, string.Empty);
                model.Prompt += prompt;
                model.NegativePrompt += negative;
            }

            ParseModelPrompts(detailer.Model1);
            ParseModelPrompts(detailer.Model2);
            ParseModelPrompts(detailer.Model3);
            ParseModelPrompts(detailer.Model4);
            ParseModelPrompts(detailer.Model5);
        }

        public static void ParseComfyDetailerLoras(this DetailerParameters detailer)
        {
            (var prompt, var negative) = detailer.Loras != null && detailer.Loras.Count > 0 ? detailer.Loras.ParseLoras() : (string.Empty, string.Empty);
            detailer.Prompt += prompt;
            detailer.NegativePrompt += negative;
        }

        public static (string prompt, string negative) ParseLoras(this List<Lora> loras)
        {
            string prompt = string.Empty;
            string negative = string.Empty;
            foreach (var lora in loras.Where(l => l.IsEnabled))
            {
                var loraString = $" <lora:{lora.Name}:{lora.Strength:N2}>";
                if (lora.IsNegative) negative += loraString;
                else prompt += loraString;
            }
            return (prompt, negative);
        }

        public static string ParseStyles(this string prompt, List<PromptStyle> styles, bool isNegative)
        {
            if (styles == null || styles.Count == 0) return prompt;
            foreach (var style in styles)
            {
                prompt = prompt.ParseStyle(isNegative ? style.NegativePrompt : style.Prompt);
            }
            return prompt;
        }

        public static string ParseStyle(this string prompt, string style)
        {
            if (string.IsNullOrWhiteSpace(style)) return prompt;
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
        public static Dictionary<string, string>? ParseInfoStrings(this string info, ModeType mode, bool isComfyui)
        {
            if (string.IsNullOrWhiteSpace(info)) return null;

            var prompt = string.Empty;
            var negative = string.Empty;
            var param = string.Empty;

            if (isComfyui)
            {
                param = info;
                try
                {
                    using var doc = JsonDocument.Parse(info);
                    var root = doc.RootElement;

                    // Search for prompt nodes (conventionally named like "text_positive", "detailer_text_positive", etc.)
                    foreach (var nodeProperty in root.EnumerateObject())
                    {
                        if (nodeProperty.Value.ValueKind == JsonValueKind.Object &&
                            nodeProperty.Value.TryGetProperty("inputs", out var inputs) &&
                            inputs.TryGetProperty("value", out var valueEl) &&
                            valueEl.ValueKind == JsonValueKind.String)
                        {
                            var nodeName = nodeProperty.Name.ToLowerInvariant();

                            if (nodeName.Contains("text_positive") || nodeName.Contains("positive"))
                            {
                                if (string.IsNullOrEmpty(prompt))
                                    prompt = valueEl.GetString() ?? string.Empty;
                            }
                            else if (nodeName.Contains("text_negative") || nodeName.Contains("negative"))
                            {
                                if (string.IsNullOrEmpty(negative))
                                    negative = valueEl.GetString() ?? string.Empty;
                            }
                        }
                    }
                }
                catch
                {
                    throw new Exception("Failed to parse prompt/negative prompt from image info.");
                }
            }
            else
            {
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
            }
            return new Dictionary<string, string>() { { "prompt", prompt }, { "negative", negative }, { "param", param } };
        }

        public static Dictionary<string, string>? ParseWebUIInfoParameters(this string param)
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

        public static JsonTreeNode ParseComfyUIInfoParameters(this string json)
        {
            using var doc = JsonDocument.Parse(json);
            return ConvertElement(doc.RootElement, "root");

            static JsonTreeNode ConvertElement(JsonElement element, string name)
            {
                var node = new JsonTreeNode { Name = name };
                switch (element.ValueKind)
                {
                    case JsonValueKind.Object:
                        foreach (var prop in element.EnumerateObject())
                            node.Children.Add(ConvertElement(prop.Value, prop.Name));
                        break;
                    case JsonValueKind.Array:
                        int i = 0;
                        foreach (var item in element.EnumerateArray())
                            node.Children.Add(ConvertElement(item, $"[{i++}]"));
                        break;
                    default:
                        node.Value = element.ToString();
                        break;
                }
                return node;
            }
        }

        public static string GetDefaultModelFromWorkflow(this string workflow)
        {
            var defaultModel = string.Empty;
            var match = Regex.Match(workflow, @"\{\{\s*Model\s*\?\?\s*""([^""]+)""");
            if (match.Success)
            {
                defaultModel = match.Groups[1].Value;
            }
            return defaultModel;
        }

        public static string ParseCivitaiImageResources(this string prompt, List<CivitaiImageMetaResourceDto> resources)
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

        /// <summary>
        /// Finds Lora tags in the prompt of form: &lt;lora:File:Strength&gt;
        /// Removes them from the prompt (collapsing extra spaces) and returns parsed Loras.
        /// </summary>
        public static List<Lora> ExtractLorasFromPrompt(this string prompt, out string cleanedPrompt, bool isNegative)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                cleanedPrompt = prompt ?? string.Empty;
                return new List<Lora>();
            }

            var pattern = @"<lora:([^:>]+):([0-9]*\.?[0-9]+)>";
            var matches = Regex.Matches(prompt, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var loras = new List<Lora>();
            var result = prompt;

            foreach (Match m in matches)
            {
                if (!m.Success) continue;
                var file = m.Groups[1].Value.Trim();
                var strengthText = m.Groups[2].Value;
                if (!float.TryParse(strengthText, NumberStyles.Float, CultureInfo.InvariantCulture, out var strength))
                    strength = 1.0f;

                loras.Add(new Lora
                {
                    Name = file,
                    Strength = strength,
                    IsEnabled = true,
                    IsNegative = isNegative
                });

                result = result.Replace(m.Value, "");
            }

            // collapse multiple spaces and trim
            cleanedPrompt = Regex.Replace(result, @"\s{2,}", "").Trim();
            return loras;
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

        public static Tag ParseCsvTag(this Tag tag) => new Tag()
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

        public static string ParseSizeKb(this double filesize)
        {
            if (filesize <= 0) return "N/D";
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
                Outdir.Img2VidSamples => ModeType.Img2Vid,
                _ => ModeType.Txt2Img
            };
        }

        public static string Truncate(this string value, int maxChars)
        {
            const string ellipses = "...";
            return value.Length <= maxChars ? value : value.Substring(0, maxChars - ellipses.Length) + ellipses;
        }

        public static T? FindJsonValueByKey<T>(JsonElement element, string propertyName)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        if (property.NameEquals(propertyName))
                        {
                            return GetValueAs<T>(property.Value);
                        }

                        var found = FindJsonValueByKey<T>(property.Value, propertyName);
                        if (found != null)
                            return found;
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        var found = FindJsonValueByKey<T>(item, propertyName);
                        if (found != null)
                            return found;
                    }
                    break;
            }

            return default;
        }

        private static T? GetValueAs<T>(JsonElement element)
        {
            try
            {
                return element.ValueKind switch
                {
                    JsonValueKind.String when typeof(T) == typeof(string) => (T)(object)element.GetString()!,
                    JsonValueKind.Number when typeof(T) == typeof(int) => (T)(object)element.GetInt32(),
                    JsonValueKind.Number when typeof(T) == typeof(long) => (T)(object)element.GetInt64(),
                    JsonValueKind.Number when typeof(T) == typeof(float) => (T)(object)element.GetSingle(),
                    JsonValueKind.Number when typeof(T) == typeof(double) => (T)(object)element.GetDouble(),
                    JsonValueKind.True or JsonValueKind.False when typeof(T) == typeof(bool) => (T)(object)element.GetBoolean(),
                    JsonValueKind.Object when typeof(T) == typeof(JsonElement) => (T)(object)element,
                    _ => default
                };
            }
            catch
            {
                return default;
            }
        }

        public static List<string> FindAllJsonValuesByKey(JsonElement element, string propertyName)
        {
            var results = new List<string>();
            TraverseJson(element, propertyName, results);
            return results;
        }

        private static void TraverseJson(JsonElement element, string propertyName, List<string> results)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        if (property.NameEquals(propertyName))
                        {
                            if (property.Value.ValueKind == JsonValueKind.String)
                                results.Add(property.Value.GetString());
                            else
                                results.Add(property.Value.ToString());
                        }

                        TraverseJson(property.Value, propertyName, results);
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        TraverseJson(item, propertyName, results);
                    }
                    break;
            }
        }

        public static JsonElement? GetFirstJsonProperty(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                using var enumerator = element.EnumerateObject().GetEnumerator();
                if (enumerator.MoveNext())
                {
                    return enumerator.Current.Value;
                }
            }
            return null;
        }

        public static bool TryGetPropertyIgnoreCase(this JsonElement element, string propertyName, out JsonElement value)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }

        public static string? GetStringProperty(this JsonElement element, string propertyName)
        {
            if (element.TryGetPropertyIgnoreCase(propertyName, out var el) && el.ValueKind == JsonValueKind.String)
                return el.GetString();
            return null;
        }

        public static Dictionary<string, object?> ConvertJsonObjectToDictionary(this JsonElement element)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (element.ValueKind != JsonValueKind.Object) return dict;
            foreach (var prop in element.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.ConvertJsonElementToObject();
            }
            return dict;
        }

        public static object? ConvertJsonElementToObject(this JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    if (element.TryGetInt32(out var i)) return i;
                    if (element.TryGetInt64(out var l)) return l;
                    if (element.TryGetDouble(out var d)) return d;
                    return element.GetDecimal();

                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetBoolean();

                case JsonValueKind.Array:
                    var list = new List<object?>();
                    foreach (var it in element.EnumerateArray())
                        list.Add(it.ConvertJsonElementToObject());
                    return list;

                case JsonValueKind.Object:
                    return element.ConvertJsonObjectToDictionary();

                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                default:
                    return null;
            }
        }
    }
}
