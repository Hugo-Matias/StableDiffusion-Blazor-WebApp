namespace BlazorWebApp.Events
{
    public class ImageRecordSavedEventArgs : EventArgs
    {
        public int ImageId { get; init; }
        public int ProjectId { get; init; }
        public string ImagePath { get; init; } = string.Empty;
        public bool IsUpdate { get; init; }
    }
}