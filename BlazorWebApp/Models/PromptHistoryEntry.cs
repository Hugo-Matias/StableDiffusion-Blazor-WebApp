namespace BlazorWebApp.Models
{
    public class PromptHistoryEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        public string Operation { get; set; } = string.Empty; // "Enhanced", "Simplified", "Custom"
        
        public string OriginalPrompt { get; set; } = string.Empty;
        
        public string ResultPrompt { get; set; } = string.Empty;
        
        public string? TemplateName { get; set; }
        
        public string ModelUsed { get; set; } = string.Empty;
        
        public bool IsSavedToDb { get; set; } // Track if saved to database (future feature)
    }
}
