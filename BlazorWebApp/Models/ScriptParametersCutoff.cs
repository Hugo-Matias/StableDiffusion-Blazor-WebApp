namespace BlazorWebApp.Models
{
    public class ScriptParametersCutoff : BaseScriptParameters
    {
        // Properties will be passed to the payload as an object[] and order is important!
        public string Targets { get; set; }
        public float Weight { get; set; }
        public bool DisableNegative { get; set; }
        public bool Strong { get; set; }
        public string Padding { get; set; }
        public string Interpolation { get; set; }
        public bool Debug { get; set; }
    }
}
