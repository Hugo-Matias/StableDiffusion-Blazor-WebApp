using System.Runtime.Serialization;

namespace BlazorWebApp.Models
{
    public class Tag
    {
        public string Name { get; set; }
        public int Color { get; set; }
        public int Uses { get; set; }
        [IgnoreDataMember]
        public int LocalUses { get; set; }
        public string? Aliases { get; set; }
    }
}
