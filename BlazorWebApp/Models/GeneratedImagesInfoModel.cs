using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
	public class GeneratedImagesInfoModel
	{

		[JsonPropertyName("all_prompts")]
		public string[] AllPrompts { get; set; }
		[JsonPropertyName("all_seeds")]
		public long[] AllSeeds { get; set; }
		[JsonPropertyName("all_subseeds")]
		public long[] AllSubseeds { get; set; }
		[JsonPropertyName("face_restoration_model")]
		public object FaceRestorationModel { get; set; }
		[JsonPropertyName("sd_model_hash")]
		public string SdModelHash { get; set; }
		[JsonPropertyName("index_of_first_image")]
		public int IndexOfFirstImage { get; set; }
		[JsonPropertyName("infotexts")]
		public string[] InfoTexts { get; set; }
		[JsonPropertyName("job_timestamp")]
		public string JobTimestamp { get; set; }
		[JsonPropertyName("clip_skip")]
		public int ClipSkip { get; set; }
		[JsonPropertyName("seed")]
		public long Seed { get; set; }

		// Other unused fields

		//public string prompt { get; set; }
		//public string negative_prompt { get; set; }
		//public long subseed { get; set; }
		//public int subseed_strength { get; set; }
		//public int width { get; set; }
		//public int height { get; set; }
		//public int sampler_index { get; set; }
		//public string sampler { get; set; }
		//public int cfg_scale { get; set; }
		//public int steps { get; set; }
		//public int batch_size { get; set; }
		//public bool restore_faces { get; set; }
		//public int seed_resize_from_w { get; set; }
		//public int seed_resize_from_h { get; set; }
		//public int denoising_strength { get; set; }
		//public object[] styles { get; set; }
	}
}
