using BlazorWebApp.Models;

namespace BlazorWebApp.Data.Entities
{
    public class State
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int Version { get; set; }
        public DateTime CreationDate { get; set; }
        public AppState? AppState { get; set; }
        
        /// <summary>
        /// Unified generation parameters.
        /// Replaces the legacy mode-specific parameter properties.
        /// </summary>
        public GenerationParameters? GenerationParameters { get; set; }
    }
}
