namespace BlazorWebApp.Models
{
    public class TextInfoModel
    {
        public string Prompt { get; set; } = string.Empty;
        public string NegativePrompt { get; set; } = string.Empty;
        public string Parameters { get; set; } = string.Empty;
    }
}
