namespace BlazorWebApp.Data.Dtos
{
    public class CivitaiBaseRequest
    {
        public string? Query { get; set; }
        public int? Limit { get; set; }
        public int? Page { get; set; }
    }
}
