using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    public class Scheduler
    {
        public string Name { get; set; }
        public string Label { get; set; }
        public List<string> Aliases { get; set; }
        [JsonPropertyName("default_rho")]
        public float DefaultRho { get; set; }
        [JsonPropertyName("need_inner_model")]
        public bool NeedInnerModel { get; set; }
    }
}
