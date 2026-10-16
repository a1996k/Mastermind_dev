using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_ActiveTimeEntry
    {
        public int RecordId { get; set; }
        public int JobId { get; set; }
        public int JobItemId { get; set; }
        public int JobItemServiceId { get; set; }
        public DateTime StartTime { get; set; }
        public string ServiceTitle { get; set; }
        public string JobNumber { get; set; }
        public int ItemNumber { get; set; }
        public string ItemDescription { get; set; }
    }
}