namespace BlazorWebApp.Data.Dtos
{
	public class PaginatedDto
	{
		public int CurrentPage { get; set; }
		public int PageCount { get; set; }
		public bool HasNext { get; set; }
		public bool HasPrev { get; set; }
	}
}
