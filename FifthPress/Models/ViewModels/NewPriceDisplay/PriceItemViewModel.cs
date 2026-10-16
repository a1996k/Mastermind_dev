using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Metadata.Edm;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.NewPriceDisplay
{
    public class PriceItemViewModel
    {
        public int JobItemId { get; set; }
        public string ItemName { get; set; }
        public int Ordinal { get; set; }
        public List<PriceRecordViewModel> PriceRecords { get; set; }

        // Totals for this job item:
        public decimal TotalCost => PriceRecords?.Sum(r => r.Cost) ?? 0;
        public decimal TotalPrice => PriceRecords?.Sum(r => r.Price) ?? 0;
        public decimal TotalDifference => TotalPrice - TotalCost;

        /// <summary>
        /// If the markup can be set uniformly across all records for this job item,
        /// store the group markup value here.
        /// </summary>
        public decimal? GroupMarkup { get; set; }
    }

}