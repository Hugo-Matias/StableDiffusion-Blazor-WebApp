using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    public class CmdFlags
    {
        [JsonPropertyName("data_dir")]
        public string BaseDir { get; set; }
        [JsonPropertyName("config")]
        public string Config { get; set; }
        [JsonPropertyName("ckpt")]
        public string Ckpt { get; set; }
        [JsonPropertyName("ckpt_dir")]
        public string CkptDir { get; set; }
        [JsonPropertyName("var_path")]
        public string Vae { get; set; }
        [JsonPropertyName("vae_dir")]
        public string VaeDir { get; set; }
        [JsonPropertyName("embeddings_dir")]
        public string EmbeddingDir { get; set; }
        [JsonPropertyName("hypernetwork_dir")]
        public string HypernetworkDir { get; set; }
        [JsonPropertyName("lora_dir")]
        public string LoraDir { get; set; }
        [JsonPropertyName("codeformer_models_path")]
        public string CodeformerModelsDir { get; set; }
        [JsonPropertyName("gfpgan_models_path")]
        public string GfpganModelsDir { get; set; }
        [JsonPropertyName("esrgan_models_path")]
        public string EsrganModelsDir { get; set; }
        [JsonPropertyName("bsrgan_models_path")]
        public string BsrganModelsDir { get; set; }
        [JsonPropertyName("realesrgan_models_path")]
        public string RealesrganModelsDir { get; set; }
        [JsonPropertyName("ldsr_models_path")]
        public string LdsrModelsDir { get; set; }
        [JsonPropertyName("scunet_models_path")]
        public string ScunetModelsDir { get; set; }
        [JsonPropertyName("swinir_models_path")]
        public string SwinirModelsDir { get; set; }
        [JsonPropertyName("deepdanbooru_projects_path")]
        public string DeepdanbooruProjectsDir { get; set; }
        [JsonPropertyName("ui_config_file")]
        public string UiConfigFile { get; set; }
        [JsonPropertyName("ui_settings_file")]
        public string UiSettingsFile { get; set; }
        [JsonPropertyName("styles_file")]
        public string StylesFile { get; set; }

    }
}
