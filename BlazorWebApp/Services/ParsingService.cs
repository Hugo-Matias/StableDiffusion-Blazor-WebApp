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
				_app.ResetStyles();
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

	}
}
