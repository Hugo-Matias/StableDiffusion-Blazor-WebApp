namespace BlazorWebApp.Data.Entities
{
    public class ResourceTemplate
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public DateTime DateCreated { get; set; }
        public List<int> ResourceIds { get; set; }

        public ResourceTemplate()
        {
            Title = string.Empty;
            DateCreated = DateTime.Now;
            ResourceIds = new List<int>();
        }
    }
}
