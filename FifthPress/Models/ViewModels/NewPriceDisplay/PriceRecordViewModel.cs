using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels.NewPriceDisplay
{
    public class PriceRecordViewModel
    {
        public int Id { get; set; }
        /// <summary>
        /// Either the service name or a composite string like "{printer} Ink Cost"
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Either a quantity (for materials) or a formatted time (e.g., "0:16 (0.27 hrs)")
        /// </summary>
        public string DurationOrQuantity { get; set; }

        public decimal Cost { get; set; }
        public decimal Markup { get; set; } // in .6 format for 60% etc.
        public decimal Price { get; set; }
        public bool OverriddenMarkup { get; set; }
        public int Ordinal { get; set; }

        public decimal Difference
        {
            get { return Price - Cost; }
        }
    }
}