namespace BlazorWebApp.Data.Entities
{
	public class Project
	{
		public int Id { get; set; }
		public string Name { get; set; }
		public List<Image> Images { get; set; }
		public DateTime CreationTime { get; set; }
		public string SampleImagePath { get; set; }
	}
}
