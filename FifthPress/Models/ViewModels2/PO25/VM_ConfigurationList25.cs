using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class VM_ConfigurationList25
    {
        public List<PO_Configuration25> Configurations { get; set; }
        public List<string> Categories { get; set; }
        public string SelectedCategory { get; set; }
    }
}