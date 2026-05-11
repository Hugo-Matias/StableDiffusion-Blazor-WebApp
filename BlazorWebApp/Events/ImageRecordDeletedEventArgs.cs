namespace BlazorWebApp.Events
{
    public class ImageRecordDeletedEventArgs : EventArgs
    {
        public int ImageId { get; init; }
        public int ProjectId { get; init; }
        public string ImagePath { get; init; } = string.Empty;
        public bool FileDeleted { get; init; }
        public IReadOnlyList<int> CleanupGroupIds { get; init; } = Array.Empty<int>();
    }
}