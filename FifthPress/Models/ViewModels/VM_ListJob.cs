using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_ListJob
    {
        public string Description { get; set; }
        public string TruncatedDescription { get; set; }
        public string ClientName { get; set; }
        public int RecordId { get; set; }
        public string MinStartDate { get; set; }
        public string DueDate { get; set; }
        public string JobNumber { get; set; }
        public bool HasDueDate { get; set; }
        public bool HasMinStartDate { get; set; }
    }
}