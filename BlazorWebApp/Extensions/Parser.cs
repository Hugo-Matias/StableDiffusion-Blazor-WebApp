using BlazorWebApp.Models;
using Microsoft.AspNetCore.Components;
using Sylvan.Data;
using Sylvan.Data.Csv;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Extensions
{
    public static class Parser
    {
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

        private static string ParsePrompt(this string prompt, string style)
        {
            if (style.Contains("{prompt}"))
            {
                return Regex.Replace(style, "{prompt}", prompt ?? "");
            }
            return $"{prompt}{style}";
        }

        public static MarkupString ParseHighresFixResizeInfo(this Txt2ImgParameters param)
        {
            var currentRes = $"{param.Width}x{param.Height} px";
            var ar = (float)param.Width / param.Height;
            string? resizeRes;
            if (param.HRWidth == 0 && param.HRHeight == 0)
                resizeRes = $"{(int)(param.Width * param.HRScale)}x{(int)(param.Height * param.HRScale)} px";
            else if (param.HRWidth > 0 && param.HRHeight == 0)
                resizeRes = $"{param.HRWidth}x{(int)(param.HRWidth / ar)} px";
            else if (param.HRWidth == 0 && param.HRHeight > 0)
                resizeRes = $"{(int)(param.HRHeight * ar)}x{param.HRHeight} px";
            else
                resizeRes = $"{param.HRWidth}x{param.HRHeight} px";
            return new MarkupString($"From: {currentRes} | To: <strong>{resizeRes}</strong>");
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
                default:
                    return "Just Resize";
            }
        }

        public static int ParseResizeModeSelection(this string selection)
        {
            switch (selection)
            {
                case "Crop and Resize":
                    return 1;
                case "Resize and Fill":
                    return 2;
                default:
                    return 0;
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

        public static int ParseInpaintingFillSelection(this string selection)
        {
            switch (selection)
            {
                case "Original":
                    return 1;
                case "Latent Noise":
                    return 2;
                case "Latent Nothing":
                    return 3;
                default:
                    return 0;
            }
        }

        public static List<CsvTag> ParseCsvTags(this string path)
        {
            var schema = Schema.Parse("Name,Color,Uses,Aliases");
            var opts = new CsvDataReaderOptions() { Schema = new CsvSchema(schema), HasHeaders = false };
            using var reader = CsvDataReader.Create(path, opts);
            var tags = reader.GetRecords<CsvTag>().ToList();
            for (int i = 0; i < tags.Count; i++)
            {
                tags[i].Name = tags[i].Name.Replace("_", " ");
                tags[i].Aliases = tags[i].Aliases.Replace("_", " ");
            }
            return tags;
        }

        public static string ParseCsvTagAlias(this string aliases, string search)
        {
            var aliasesList = aliases.Replace("\"", "").Split(',');
            return aliasesList.FirstOrDefault(a => a.Contains(search, StringComparison.InvariantCultureIgnoreCase));
        }

        public static string ParseCsvTagColor(this int color)
        {
            switch (color)
            {
                case 0:
                    return "var(--app-blue-5);";
                case 1:
                    return "var(--app-red)";
                case 2:
                    return "var(--app-yellow)";
                case 3:
                    return "var(--app-purple)";
                case 4:
                    return "var(--app-lime)";
                case 5:
                    return "var(--app-orange)";
                case 6:
                    return "var(--app-magenta)";
                case 7:
                    return "var(--app-green)";
                case 8:
                    return "var(--app-cyan)";
                default:
                    return "var(--app-light-2);";
            }
        }
    }
}
