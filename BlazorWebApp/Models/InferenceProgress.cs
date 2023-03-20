using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    public class InferenceProgress : BaseProgress
    {
        [JsonPropertyName("progress")]
        public new float Value { get; set; }
        [JsonPropertyName("eta_relative")]
        public float EtaRelative { get; set; }
        [JsonPropertyName("state")]
        public InferenceProgressState State { get; set; }
        [JsonPropertyName("current_image")]
        public string CurrentImage { get; set; }

        public class InferenceProgressState
        {
            [JsonPropertyName("skipped")]
            public bool Skipped { get; set; }
            [JsonPropertyName("interrupted")]
            public bool Interrupted { get; set; }
            [JsonPropertyName("job")]
            public string Job { get; set; }
            [JsonPropertyName("job_count")]
            public int JobCount { get; set; }
            [JsonPropertyName("job_no")]
            public int JobNo { get; set; }
            [JsonPropertyName("sampling_step")]
            public int SamplingStep { get; set; }
            [JsonPropertyName("sampling_steps")]
            public int SamplingSteps { get; set; }
        }

    }
}
