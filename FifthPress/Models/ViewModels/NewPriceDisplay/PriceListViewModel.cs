using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.NewPriceDisplay
{
    public class PriceListViewModel
    {
        public List<PriceItemViewModel> Items { get; set; }
        public int RecordId { get; set; }
        public string RecordType { get; set; } // "JOB" or "QUOTE"
        public bool UseSqftPricing { get; set; }
        public decimal? SqftPriceOverride { get; set; }
    }


}