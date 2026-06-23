namespace BlazorWebApp.Models
{
    public class DictionaryWord
    {
        public string Word { get; set; } = string.Empty;
        public DictionaryTheme Theme { get; set; }
        public int Score { get; set; } = 0;
        public string Source { get; set; } = string.Empty;
    }
}