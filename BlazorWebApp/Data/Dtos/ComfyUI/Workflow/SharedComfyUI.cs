namespace BlazorWebApp.Data.Dtos.ComfyUI.Workflow
{
    public class SharedComfyUI
    {
        public string? Prompt { get; set; }
        public string? NegativePrompt { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public long? Seed { get; set; }
        public int? Steps { get; set; }
        public float? CfgScale { get; set; }
        public float? Guidance { get; set; }
        public string? SamplerName { get; set; }
        public string? Scheduler { get; set; }
        public int? BatchSize { get; set; }
    }
}
