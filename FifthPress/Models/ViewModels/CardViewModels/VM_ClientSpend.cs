using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using FifthPress.Models.Dashboard;

namespace FifthPress.Models.ViewModels.CardViewModels
{
    public class VM_ClientSpend
    {
        public List<IGrouping<string, JobMetrics>> ClientJobMetrics { get; set; }

        public bool IsLocked { get; set; }

        public int RangeOverride { get; set; }
    }
}