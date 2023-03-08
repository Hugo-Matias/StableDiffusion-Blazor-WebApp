using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    public class Options
    {
        [JsonPropertyName("sd_model_checkpoint")]
        public string? SDModelCheckpoint { get; set; }
        [JsonPropertyName("outdir_txt2img_samples")]
        public string? OutdirSamplesTxt2Img { get; set; }
        [JsonPropertyName("outdir_img2img_samples")]
        public string? OutdirSamplesImg2Img { get; set; }
        [JsonPropertyName("outdir_extras_samples")]
        public string? OutdirSamplesExtras { get; set; }
        [JsonPropertyName("outdir_txt2img_grids")]
        public string? OutdirGridTxt2Img { get; set; }
        [JsonPropertyName("outdir_img2img_grids")]
        public string? OutdirGridImg2Img { get; set; }
        [JsonPropertyName("samples_filename_pattern")]
        public string? FilenamePatternSamples { get; set; }
        [JsonPropertyName("directories_filename_pattern")]
        public string? FilenamePatternDir { get; set; }
        [JsonPropertyName("samples_format")]
        public string? SamplesFormat { get; set; }
        [JsonPropertyName("samples_save")]
        public bool? SamplesSave { get; set; }
        [JsonPropertyName("save_txt")]
        public bool? SaveTxt { get; set; }
        [JsonPropertyName("grid_format")]
        public string? GridFormat { get; set; }
        [JsonPropertyName("grid_save")]
        public bool? GridSave { get; set; }
        [JsonPropertyName("grid_only_if_multiple")]
        public bool? GridOnlyIfMultiple { get; set; }
        [JsonPropertyName("sd_vae")]
        public string? SDVae { get; set; }
        [JsonPropertyName("CLIP_stop_at_last_layers")]
        public float? ClipSkip { get; set; }
        [JsonPropertyName("eta_noise_seed_delta")]
        public float? ENSD { get; set; }
        [JsonPropertyName("eta_ddim")]
        public float? EtaDdim { get; set; }
        [JsonPropertyName("eta_ancestral")]
        public float? EtaAncestral { get; set; }
        [JsonPropertyName("ddim_discretize")]
        public string? DdimDiscretize { get; set; }
        [JsonPropertyName("s_churn")]
        public float? SChurn { get; set; }
        [JsonPropertyName("s_tmin")]
        public float? STmin { get; set; }
        [JsonPropertyName("s_noise")]
        public float? SNoise { get; set; }
        //[JsonPropertyName("return_grid")]


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
