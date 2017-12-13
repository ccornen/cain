using System.Collections.Generic;

namespace Packer.Model
{
    public class Function
    {
        public int Start { get; set; }
        public int End { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public List<string> Parameters { get; set; } 
    }
}
