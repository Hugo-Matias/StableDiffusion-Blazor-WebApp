namespace BlazorWebApp.Models
{
    public class Lora
    {
        public string File { get; set; }
        public float Strength { get; set; }
        public bool IsNegative { get; set; }
        public bool IsEnabled { get; set; }

        public Lora() { }
        public Lora(Lora clone)
        {
            File = clone.File;
            Strength = clone.Strength;
            IsNegative = clone.IsNegative;
            IsEnabled = clone.IsEnabled;
        }
    }
}
