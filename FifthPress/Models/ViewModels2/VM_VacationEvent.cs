using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2
{
    public class VM_VacationEvent
    {
        public int id { get; set; }
        public string title { get; set; }
        public string start { get; set; }   // ISO date string
        public string end { get; set; }     // exclusive ISO date
        public string color { get; set; }   // optional: per-employee color
    }
}