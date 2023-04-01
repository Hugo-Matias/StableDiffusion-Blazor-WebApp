using BlazorWebApp.Models;

namespace BlazorWebApp.Data.Entities
{
    public class State
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int Version { get; set; }
        public DateTime CreationDate { get; set; }
        public AppState? AppState { get; set; }
        public Txt2ImgParameters? Txt2ImgParameters { get; set; }
        public Img2ImgParameters? Img2ImgParameters { get; set; }
        public UpscaleParameters? UpscaleParameters { get; set; }
    }
}
