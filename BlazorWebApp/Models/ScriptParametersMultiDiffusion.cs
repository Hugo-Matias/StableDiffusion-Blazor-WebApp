namespace BlazorWebApp.Models
{
    public class ScriptParametersMultiDiffusionTiledDiffusion : BaseScriptParameters
    {
        public string Method { get; set; }
        public bool OverwriteImageSize { get; set; }
        public bool KeepInputSize { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public int TileWidth { get; set; }
        public int TileHeight { get; set; }
        public int Overlap { get; set; }
        public int TileBatchSize { get; set; }
        public string UpscalerIndex { get; set; }
        public float ScaleFactor { get; set; }
        public bool ControlTensorCpu { get; set; }
        public bool EnableBBoxControl { get; set; }
        public bool DrawBackground { get; set; }
        public List<ScriptParametersMultiDiffusionBBoxControl>? BBoxControlStates { get; set; }
    }

    public class ScriptParametersMultiDiffusionTiledVae : BaseScriptParameters
    {
        public bool VaeToGpu { get; set; }
        public bool FastDecoder { get; set; }
        public bool FastEncoder { get; set; }
        public bool ColorFix { get; set; }
        public int EncoderTileSize { get; set; }
        public int DecoderTileSize { get; set; }
    }

    public class ScriptParametersMultiDiffusionBBoxControl
    {
        public bool IsEnabled { get; set; }
        public float CoordX { get; set; }
        public float CoordY { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string Prompt { get; set; }
        public string NegativePrompt { get; set; }
        public string BlendMode { get; set; }
        public float FeatherRadius { get; set; }
    }
}
