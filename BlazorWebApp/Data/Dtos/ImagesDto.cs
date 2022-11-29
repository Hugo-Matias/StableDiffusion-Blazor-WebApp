using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Data.Dtos
{
	public class ImagesDto : PaginatedDto
	{
		public List<Image> Images { get; set; }
	}
}
