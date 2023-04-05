namespace BlazorWebApp.Models
{
    public class ScriptParametersXYZPlot : BaseScriptParameters
    {
        public int XTypeIndex { get; set; }
        public string XValues { get; set; }
        public int YTypeIndex { get; set; }
        public string YValues { get; set; }
        public int ZTypeIndex { get; set; }
        public string ZValues { get; set; }
        public bool DrawLegend { get; set; }
        public bool IncludeSubImages { get; set; }
        public bool IncludeSubGrids { get; set; }
        public bool RandomSeed { get; set; }
        public int Margin { get; set; }
    }
}
