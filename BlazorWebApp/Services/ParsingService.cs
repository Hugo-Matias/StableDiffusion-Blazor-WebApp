using BlazorWebApp.Models;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
    public class ParsingService
    {
        private readonly AppState _app;

        public ParsingService(AppState app)
        {
            _app = app;
        }

        public Txt2ImgParametersModel ParseParameters(Txt2ImgParametersModel param)
        {
            if (_app.Style1 != "None" || _app.Style2 != "None")
            {
                ParsePromptStyles(param);
                //_app.ResetStyles();
            }

            return param;
        }

        private void ParsePromptStyles(Txt2ImgParametersModel param)
        {
            foreach (var style in _app.Styles)
            {
                if (style.Name != "None" && (_app.Style1 == style.Name || _app.Style2 == style.Name))
                {
                    param.Prompt = ParsePrompt(param.Prompt, style.Prompt);
                    param.NegativePrompt = ParsePrompt(param.NegativePrompt, style.NegativePrompt);
                }
            }
        }

        private string ParsePrompt(string prompt, string style)
        {
            if (style.Contains("{prompt}"))
            {
                return Regex.Replace(style, "{prompt}", prompt ?? "");
            }

            return $"{prompt}{style}";
        }

        public ImageInfoModel ParseImageInfoString(ImageInfoModel image)
        {
            foreach (var line in image.InfoString)
            {
                if (line.StartsWith("Negative prompt:"))
                    image.NegativePrompt = line.Replace("Negative prompt: ", "");

                else if (line.StartsWith("Steps:"))
                    ParseImageInfoParameters(image, line);

                else
                    image.Prompt = line;
            }

            return image;
        }

        public ImageInfoModel ParseImageInfoParameters(ImageInfoModel image, string info)
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
    }
}
