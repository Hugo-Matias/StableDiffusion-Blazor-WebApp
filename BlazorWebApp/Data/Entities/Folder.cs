namespace BlazorWebApp.Data.Entities
{
    public class Folder
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<Project> Projects { get; set; }
        public int SortOrder { get; set; }
    }
}
