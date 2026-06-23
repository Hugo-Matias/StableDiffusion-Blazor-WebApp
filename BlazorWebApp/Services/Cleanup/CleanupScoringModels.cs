namespace BlazorWebApp.Services.Cleanup
{
    public record CleanupScoringOptions
    {
        public const string SectionName = "Cleanup:Scoring";

        public bool Enabled { get; set; }
        public bool RunForSavedImages { get; set; }
        public CleanupEmbeddingRuntimeProvider RuntimeProvider { get; set; } = CleanupEmbeddingRuntimeProvider.CPU;
        public int CudaDeviceId { get; set; }
        public CleanupScoreModelOptions Model { get; set; } = new();
    }

    public record CleanupScoreModelOptions
    {
        public string ModelKey { get; set; } = string.Empty;
        public string ModelPath { get; set; } = string.Empty;
        public string ScoreName { get; set; } = "aesthetic";
        public string InputName { get; set; } = string.Empty;
        public string OutputName { get; set; } = string.Empty;
        public int InputWidth { get; set; } = 224;
        public int InputHeight { get; set; } = 224;
        public string InputLayout { get; set; } = "NCHW";
        public double MinScore { get; set; }
        public double MaxScore { get; set; } = 1;
        public List<float> Mean { get; set; } = new() { 0.48145466f, 0.4578275f, 0.40821073f };
        public List<float> StandardDeviation { get; set; } = new() { 0.26862954f, 0.26130258f, 0.27577711f };
    }

    public record CleanupScoreModelIdentity
    {
        public string ModelKey { get; init; } = string.Empty;
        public string? ModelHash { get; init; }
        public string ScoreName { get; init; } = string.Empty;
        public double MinScore { get; init; }
        public double MaxScore { get; init; } = 1;
        public string RuntimeProvider { get; init; } = CleanupEmbeddingRuntimeProvider.CPU.ToString();
    }

    public record CleanupScoringValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; init; } = new();
    }
}