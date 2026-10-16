using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_JobTiming
    {
        public string JobDescription { get; set; }
        public int JobId { get; set; }
        public DateTime TimeEntry { get; set; }
        public string DateString { get; set; }
        public string EmployeeName { get; set; }
        public int UserId { get; set; }
    }
}