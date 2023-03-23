namespace BlazorWebApp.Models
{
    public class ScriptParametersUltimateUpscale : BaseScriptParameters
    {
        // Properties will be passed to the payload as an object[] and order is important!
        public bool UnusedValue { get; set; } // An extra argument that is not used in the SD script but must be passed in nonetheless
        public int TileWidth { get; set; }
        public int TileHeight { get; set; }
        public int MaskBlur { get; set; }
        public int Padding { get; set; }
        public int SeamFixWidth { get; set; }
        public float SeamFixDenoise { get; set; }
        public int SeamFixPadding { get; set; }
        public int UpscalerIndex { get; set; }
        public bool SaveUpscaledImage { get; set; }
        public int RedrawMode { get; set; }
        public bool SaveSeamFixImage { get; set; }
        public int SeamFixMaskBlur { get; set; }
        public int SeamFixType { get; set; }
        public int TargetSizeType { get; set; }
        public int CustomWidth { get; set; }
        public int CustomHeight { get; set; }
        public float CustomScale { get; set; }
    }
}
