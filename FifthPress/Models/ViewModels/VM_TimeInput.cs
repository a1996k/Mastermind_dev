using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_TimeInput
    {
        public string FormattedServiceTime { get; set; }
        public decimal ServiceTime { get; set; }
        public bool LockTime { get; set; }
    }
}