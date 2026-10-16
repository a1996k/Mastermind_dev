using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    public class JobPriceInformation
    {
        public List<JobParentItemTotals_Result> ItemTotals { get; set; }
        public decimal Total { get; set; }
        public string TotalString { get; set; }
        public int JobId { get; set; }
        public bool UseSqftPricing { get; set; }
        public decimal? SqftPriceOverride { get; set; }
        public HashSet<int> ServiceOverrideItemIds { get; set; } = new HashSet<int>();
    }
}