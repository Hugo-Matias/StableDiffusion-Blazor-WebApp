namespace BlazorWebApp.Models
{
    /// <summary>
    /// Loader settings for a video source consumed by Video Helper Suite upload nodes.
    /// </summary>
    public class VideoSourceOptions
    {
        public double ForceRate { get; set; }
        public int CustomWidth { get; set; }
        public int CustomHeight { get; set; }
        public int FrameLoadCap { get; set; }
        public int SkipFirstFrames { get; set; }
        public int SelectEveryNth { get; set; } = 1;
        public string Format { get; set; } = "AnimateDiff";
        public int PreviewFrameOffset { get; set; }

        public VideoSourceOptions Clone()
        {
            return new VideoSourceOptions
            {
                ForceRate = ForceRate,
                CustomWidth = CustomWidth,
                CustomHeight = CustomHeight,
                FrameLoadCap = FrameLoadCap,
                SkipFirstFrames = SkipFirstFrames,
                SelectEveryNth = SelectEveryNth,
                Format = Format,
                PreviewFrameOffset = PreviewFrameOffset
            };
        }
    }
}