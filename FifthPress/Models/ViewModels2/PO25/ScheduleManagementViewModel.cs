using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels2.PO25
{
    public class ScheduleManagementViewModel
    {
        public int RecurringId { get; set; }
        public string Action { get; set; } // "Pause", "Activate", "Delete"
        public string Reason { get; set; }
    }
}