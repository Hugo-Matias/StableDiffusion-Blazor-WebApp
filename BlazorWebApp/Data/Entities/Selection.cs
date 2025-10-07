namespace BlazorWebApp.Data.Entities
{
    public class Selection
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<Image> Images { get; set; } = new();
    }
}
