namespace BlazorWebApp.Models
{
    public class JsonTreeNode
    {
        public string Name { get; set; }
        public string? Value { get; set; }
        public List<JsonTreeNode> Children { get; set; } = new();
    }
}
