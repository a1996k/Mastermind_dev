using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ControllerModels
{
    public class JobLayoutOverride_CM
    {
        public int JobItemId { get; set; }
        public int Quantity { get; set; }
        public int Up { get; set; }
        public string MarginXL { get; set; }
        public string MarginXR { get; set; }
        public string MarginYT { get; set; }
        public string MarginYB { get; set; }
        public string SideGuides { get; set; }
        public string Gripper { get; set; }
        public string Tail { get; set; }
        public bool UpValueLocked { get; set; }
        //public string LayoutString { get; set; }
        
    }
}