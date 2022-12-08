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

		public SharedParametersModel ParseParameters(SharedParametersModel param)
		{
			if (_app.Style1 != "None" || _app.Style2 != "None")
			{
				ParsePromptStyles(param);
				//_app.ResetStyles();
			}

			return param;
		}

		private void ParsePromptStyles(SharedParametersModel param)
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

		public string ParseResizeModeValue(int value)
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

		public int ParseResizeModeSelection(string selection)
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

		public string ParseInpaintingFillValue(int value)
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

		public int ParseInpaintingFillSelection(string selection)
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

	}
}
