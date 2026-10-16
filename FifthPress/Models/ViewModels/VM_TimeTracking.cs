using FifthPress.Models.QA;
using FifthPress.Models.TimeTracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_TimeTracking
    {
        public List<OtherItem> OtherItems { get; set; }
        public Job Job { get; set; }
        public JobItem JobItem { get; set; }
        public List<QARecord> QARecords { get; set; }
        /// <summary>
        /// Maps JobItemFinishingMaterial ID → pull status: 0 = none, 1 = partial, 2 = fully pulled
        /// </summary>
        public Dictionary<int, int> FinishingMaterialPullStatus { get; set; }
        /// <summary>
        /// Primary material (substrate) pull status: 0 = none, 1 = partial, 2 = fully pulled
        /// </summary>
        public int MaterialPullStatus { get; set; }
    }
}