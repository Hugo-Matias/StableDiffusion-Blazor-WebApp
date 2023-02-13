namespace BlazorWebApp.Models
{
    public class PromptButton
    {
        public string Title { get; set; }
        public List<string>? Tags { get; set; }
        public IEnumerable<PromptButton>? Items { get; set; }
    }
}
