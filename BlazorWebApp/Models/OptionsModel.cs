using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
	public class OptionsModel
	{
		[JsonPropertyName("sd_model_checkpoint")]
		public string SDModelCheckpoint { get; set; }
		[JsonPropertyName("outdir_txt2img_samples")]
		public string OutdirSamplesTxt2Img { get; set; }
		[JsonPropertyName("outdir_img2img_samples")]
		public string OutdirSamplesImg2Img { get; set; }
		[JsonPropertyName("outdir_extras_samples")]
		public string OutdirSamplesExtras { get; set; }
		[JsonPropertyName("outdir_txt2img_grids")]
		public string OutdirGridTxt2Img { get; set; }
		[JsonPropertyName("outdir_img2img_grids")]
		public string OutdirGridImg2Img { get; set; }
		[JsonPropertyName("samples_filename_pattern")]
		public string FilenamePatternSamples { get; set; }
		[JsonPropertyName("directories_filename_pattern")]
		public string FilenamePatternDir { get; set; }
		//[JsonPropertyName("samples_save")]
		//[JsonPropertyName("grid_save")]
		//[JsonPropertyName("return_grid")]
		//[JsonPropertyName("grid_only_if_multiple")]
		//[JsonPropertyName("samples_format")]
		//[JsonPropertyName("grid_format")]


		// DEPRECATED
		//[JsonPropertyName("txt2img/Sampling Steps/value")]
		//public float StepsValue { get; set; }
		//[JsonPropertyName("txt2img/Sampling Steps/minimum")]
		//public float StepsMin { get; set; }
		//[JsonPropertyName("txt2img/Sampling Steps/maximum")]
		//public float StepsMax { get; set; }
		//[JsonPropertyName("txt2img/Sampling Steps/step")]
		//public float StepsStep { get; set; }
	}
}
