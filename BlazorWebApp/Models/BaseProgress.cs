using MudBlazor;

namespace BlazorWebApp.Models
{
    public class BaseProgress
    {
        public Guid Id { get; set; }
        public float Value { get; set; }
        public Color BarColor { get; set; }
        public string? Label { get; set; }
        public bool IsIndeterminate { get; set; }

        public BaseProgress() => Id = Guid.NewGuid();
    }
}
