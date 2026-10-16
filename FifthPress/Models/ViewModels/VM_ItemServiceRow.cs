using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_ItemServiceRow
    {
        public int ServiceId { get; set; }
        public string ServiceTitle { get; set; }
        public int RecordId { get; set; }
        public int Ordinal { get; set; }
        public bool Required { get; set; }
        public string Notes { get; set; }
        public bool IsCuttingService { get; set; }
        public bool IsTimed { get; set; }
        public string FormattedServiceTime { get; set; }
        public bool RequiresQualityCheck { get; set; }
        public bool IsPrintingService { get; set; }
        public bool ShowQAToggle { get; set; }
        public bool IsFinishingService { get; set; }
    }
}