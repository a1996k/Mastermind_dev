using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_EditTimeEntry
    {
        public bool HasPermission { get; set; }
        public int Id { get; set; }
        public string JobDescription { get; set; } = String.Empty;
        public string ItemDescription { get; set; } = String.Empty;
        public string ServiceTitle { get; set; } = String.Empty;
        public DateTime StartTime { get; set; }
        public bool HasEndTime { get; set; }
        public DateTime? EndTime { get; set; }
    }
}