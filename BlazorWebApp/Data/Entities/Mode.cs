namespace BlazorWebApp.Data.Entities
{
	public enum ModeType
	{
		Txt2Img, Img2Img, Extras, Img2Vid, Txt2Vid, Vid2Vid
	}
	public class Mode
	{
		public int Id { get; set; }
		public ModeType Type { get; set; }
		public List<Image> Images { get; set; }
	}
}
