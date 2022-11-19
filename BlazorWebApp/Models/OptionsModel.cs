using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
	public class OptionsModel
	{
		//[JsonPropertyName("outdir_txt2img_samples")]
		//[JsonPropertyName("outdir_img2img_samples")]
		//[JsonPropertyName("outdir_extras_samples")]
		//[JsonPropertyName("outdir_txt2img_grids")]
		//[JsonPropertyName("outdir_img2img_grids")]
		//[JsonPropertyName("samples_save")]
		//[JsonPropertyName("grid_save")]
		//[JsonPropertyName("return_grid")]
		//[JsonPropertyName("grid_only_if_multiple")]
		//[JsonPropertyName("samples_format")]
		//[JsonPropertyName("grid_format")]

		[JsonPropertyName("sd_model_checkpoint")]
		public string SDModelCheckpoint { get; set; }

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
