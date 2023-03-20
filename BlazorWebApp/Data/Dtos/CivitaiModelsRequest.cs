namespace BlazorWebApp.Data.Dtos
{
    public class CivitaiModelsRequest : CivitaiBaseRequest
    {
        public string? Username { get; set; }
        public string? Tag { get; set; }
        public CivitaiModelType? Type { get; set; }
        public CivitaiSort? Sort { get; set; }
        public CivitaiPeriod? Period { get; set; }
        public int? Rating { get; set; }
        public bool? Favorites { get; set; }
        public bool? Hidden { get; set; }
        public bool? IsPrimaryFileOnly { get; set; }
        public string? Hash { get; set; }
    }

    public enum CivitaiModelType { All, Checkpoint, LORA, LoCon, TextualInversion, Hypernetwork, AestheticGradient, Controlnet, Poses, Wildcards, Other }
    public enum CivitaiSort { Highest_Rated, Most_Downloaded, Newest }
    public enum CivitaiPeriod { AllTime, Year, Month, Week, Day }
}
