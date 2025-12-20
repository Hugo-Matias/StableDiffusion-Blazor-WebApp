using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    /// <summary>
    /// Response from ComfyUI generation containing generated images.
    /// </summary>
    public class GeneratedImages
    {
        /// <summary>
        /// Base64 encoded image data.
        /// </summary>
        [JsonPropertyName("images")]
        public List<string> Images { get; set; }
        
        /// <summary>
        /// Workflow JSON info from ComfyUI.
        /// </summary>
        [JsonPropertyName("info")]
        public string Info { get; set; }
    }
}
