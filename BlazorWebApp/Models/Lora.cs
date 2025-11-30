namespace BlazorWebApp.Models
{
    public class Lora
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public float Strength { get; set; }
        public bool IsNegative { get; set; }
        public bool IsEnabled { get; set; }

        public Lora() { }
        public Lora(Lora clone)
        {
            Name = clone.Name;
            Path = clone.Path;
            Strength = clone.Strength;
            IsNegative = clone.IsNegative;
            IsEnabled = clone.IsEnabled;
        }
    }
}
