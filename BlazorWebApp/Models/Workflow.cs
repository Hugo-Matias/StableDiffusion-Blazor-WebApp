using BlazorWebApp.Data.Entities;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Models
{
    public class Workflow
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public ModelBase Base { get; set; }
        public ModeType Mode { get; set; }
        public ModelType ModelType { get; set; }
        public string Prompt { get; set; }
    }
}
