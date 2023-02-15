using Excubo.Generators.Blazor;

namespace BlazorWebApp.Data.Entities
{
	public class Project
	{
		public int Id { get; set; }
		[Required]
		public string Name { get; set; }
		public List<Image> Images { get; set; }
		public DateTime CreationTime { get; set; }
		public string? SampleImagePath { get; set; }
		public int? FolderId { get; set; }
		public Folder Folder { get; set; }
	}
}
