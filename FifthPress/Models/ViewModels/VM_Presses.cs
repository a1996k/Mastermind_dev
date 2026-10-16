using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_Presses
    {
        public List<Press> Presses { get; set; }
        public List<string> PressCategories { get; set; }
        public List<string> PressKeys { get; set; }
    }
}