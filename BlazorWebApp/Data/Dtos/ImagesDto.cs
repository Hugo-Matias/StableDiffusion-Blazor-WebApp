using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Data.Dtos
{
	public class ImagesDto
	{
		public List<Image> Images { get; set; }
		public int CurrentPage { get; set; }
		public int PageCount { get; set; }
	}
}
