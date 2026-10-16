using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_Plate
    {
        public Plate Plate { get; set; }
        public List<PressPlate> AssignedPresses { get; set; }
    }
}