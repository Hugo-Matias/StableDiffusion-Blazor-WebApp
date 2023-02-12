namespace BlazorWebApp.Models
{
    public class PromptButtonModel
    {
        public string Title { get; set; }
        public List<string>? Tags { get; set; }
        public IEnumerable<PromptButtonModel>? Items { get; set; }
    }
}
