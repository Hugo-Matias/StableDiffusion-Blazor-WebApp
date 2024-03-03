namespace BlazorWebApp.Data.Dtos
{
    public class CivitaiImagesRequest
    {
        public int Limit { get; set; }
        public int? PostId { get; set; }
        public int? ModelId { get; set; }
        public int? ModelVersionId { get; set; }
        public string? Username { get; set; }
        public CivitaiNsfw? Nsfw { get; set; }
        public CivitaiImageSort? Sort { get; set; }
        public CivitaiPeriod? Period { get; set; }
        public int Page { get; set; }
    }

    public enum CivitaiNsfw { All, None, Soft, Mature, X }
    public enum CivitaiImageSort { Most_Reactions, Most_Buzz, Most_Comments, Most_Collected, Newest, Random }
}
