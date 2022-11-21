using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
	public class Txt2ImgParametersModel
	{
		[JsonPropertyName("enable_hr")]
		public bool? EnableHR { get; set; }
		[JsonPropertyName("denoising_strength")]
		public int? DenoisingStrength { get; set; }
		[JsonPropertyName("firstphase_width")]
		public int? FirstphaseWidth { get; set; }
		[JsonPropertyName("firstphase_height")]
		public int? FirstphaseHeight { get; set; }
		[JsonPropertyName("prompt")]
		public string? Prompt { get; set; }
		[JsonPropertyName("styles")]
		public string[]? Styles { get; set; }
		[JsonPropertyName("seed")]
		public long? Seed { get; set; }
		[JsonPropertyName("subseed")]
		public long? Subseed { get; set; }
		[JsonPropertyName("subseed_strength")]
		public float? SubseedStrength { get; set; }
		[JsonPropertyName("seed_resize_from_h")]
		public int? SeedResizeFromH { get; set; }
		[JsonPropertyName("seed_resize_from_w")]
		public int? SeedResizeFromW { get; set; }
		[JsonPropertyName("batch_size")]
		public int? BatchSize { get; set; }
		[JsonPropertyName("n_iter")]
		public int? NIter { get; set; }
		[JsonPropertyName("steps")]
		public int? Steps { get; set; }
		[JsonPropertyName("cfg_scale")]
		public float? CfgScale { get; set; }
		[JsonPropertyName("width")]
		public int? Width { get; set; }
		[JsonPropertyName("height")]
		public int? Height { get; set; }
		[JsonPropertyName("restore_faces")]
		public bool? RestoreFaces { get; set; }
		[JsonPropertyName("tiling")]
		public bool? Tiling { get; set; }
		[JsonPropertyName("negative_prompt")]
		public string? NegativePrompt { get; set; }
		[JsonPropertyName("eta")]
		public float? Eta { get; set; }
		[JsonPropertyName("s_churn")]
		public float? SChurn { get; set; }
		[JsonPropertyName("s_tmax")]
		public float? STmax { get; set; }
		[JsonPropertyName("s_tmin")]
		public float? STmin { get; set; }
		[JsonPropertyName("s_noise")]
		public float? SNoise { get; set; }
		[JsonPropertyName("sampler_index")]
		public string? SamplerIndex { get; set; }
	}
}
