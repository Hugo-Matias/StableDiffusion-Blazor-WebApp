namespace BlazorWebApp.Models
{
    public class SamplerModel
    {
        public string Name { get; set; }
        public string[] Aliases { get; set; }
        public SamplerOptionsModel Options { get; set; }
    }

    public class SamplerOptionsModel
    {
        //[JsonPropertyName("scheduler")]
        public string Scheduler { get; set; }
    }
}
