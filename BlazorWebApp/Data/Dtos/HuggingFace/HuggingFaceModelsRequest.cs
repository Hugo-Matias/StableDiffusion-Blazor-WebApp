namespace BlazorWebApp.Data.Dtos.HuggingFace
{
    public class HuggingFaceModelsRequest
    {
        public string? Query { get; set; }
        public string? Author { get; set; }
        public string? Filter { get; set; }
        public string? PipelineTag { get; set; }
        public string? Library { get; set; }
        public HuggingFaceSort Sort { get; set; } = HuggingFaceSort.Downloads;
        /// <summary>Sort direction: "-1" for descending (HF API convention). Null omits the parameter.</summary>
        public string? Direction { get; set; } = "-1";
        public int Limit { get; set; } = 20;
        public string? Cursor { get; set; }
        public bool Full { get; set; } = false;
        public bool CardData { get; set; } = true;
    }

    public class HuggingFaceQuickSearchRequest
    {
        public string Query { get; set; } = string.Empty;
        public string? PipelineTag { get; set; }
        public string? Library { get; set; }
        public string? Author { get; set; }
        public int Limit { get; set; } = 20;
    }

    public enum HuggingFaceSort
    {
        Downloads,
        Likes,
        LastModified,
        Trending
    }
}
