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

        public TextInfoModel ParseImageInfo(string[] info)
        {
            var infoModel = new TextInfoModel();

            foreach (var line in info)
            {
                if (line.StartsWith("Negative prompt:"))
                    infoModel.NegativePrompt = line;

                else if (line.StartsWith("Steps:"))
                    infoModel.Parameters = line;

                else
                    infoModel.Prompt = line;
            }

            return infoModel;
        }

        public Txt2ImgParametersModel ParseInfoToParameters(TextInfoModel info, Txt2ImgParametersModel param)
        {
            param.Prompt = info.Prompt ?? "";
            param.NegativePrompt = info.NegativePrompt.Replace("Negative prompt: ", "") ?? "";
            param.Steps = int.Parse(Regex.Match(info.Parameters, @"(Steps: )(\d+)").Groups[2].Value);
            param.SamplerIndex = Regex.Match(info.Parameters, @"(Sampler: )(.+?),").Groups[2].Value;
            param.CfgScale = float.Parse(Regex.Match(info.Parameters, @"(CFG scale: )(.+?),").Groups[2].Value);
            param.Seed = long.Parse(Regex.Match(info.Parameters, @"(Seed: )(\d+)").Groups[2].Value);
            var size = Regex.Match(info.Parameters, @"(Size: )(\d+)x(\d+)");
            param.Width = int.Parse(size.Groups[2].Value);
            param.Height = int.Parse(size.Groups[3].Value);
            return param;
        }
    }
}
