using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using BlazorWebApp.Data.Dtos.Ollama;

namespace BlazorWebApp.Data.Entities
{
    public class SystemPromptTemplate
    {
        public int Id { get; set; }
        
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string? Description { get; set; }
        
        [Required]
        public string MessagesJson { get; set; } = "[]";
        
        public bool IsDefault { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime UpdatedAt { get; set; }
        
        [NotMapped]
        public List<OllamaChatMessage> Messages
        {
            get => string.IsNullOrEmpty(MessagesJson) 
                ? new List<OllamaChatMessage>() 
                : JsonSerializer.Deserialize<List<OllamaChatMessage>>(MessagesJson) ?? new();
            set => MessagesJson = JsonSerializer.Serialize(value);
        }
    }
}
