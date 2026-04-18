namespace BlazorWebApp.Data
{
    public class Enums
    {
        public enum ModelType { Checkpoint, Diffusion };
        public enum ModelBase { StableDiffusion, Flux, Chroma, Qwen, ZImage, Wan, Anima, Ernie };
        public enum Backend { WebUI, ComfyUI };
    }
}
