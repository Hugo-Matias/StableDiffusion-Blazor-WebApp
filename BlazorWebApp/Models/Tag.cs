namespace BlazorWebApp.Models
{
    public class Tag
    {
        public string Name { get; set; }
        public int Color { get; set; }
        public int Uses { get; set; }
        public int LocalUses { get; set; }
        public string? Aliases { get; set; }
        public int FuzzyScore { get; set; }
        public bool IsRecentlyUsed { get; set; }
        public string Source { get; set; } = string.Empty;
    }
}
