using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using FifthPress.Models.ViewModels.NewPriceDisplay;

namespace FifthPress.Models.ViewModels
{
    public class VM_QuoteWrapper
    {
        public VM_Quote VM_Quote { get; set; }
        public bool RequiresValidator { get; set; } = true;
        public List<Contact> Contacts { get; set; }
        public List<GenericItemOrganizer> GenericItemOrganizer { get; set; }

        public QuotePriceInformation ItemTotals { get; set; }

        //public List<QuoteParentItemTotals_Result> QuoteItemTotals { get; set; }
        public VM_PrepressServices AvailablePrepressServices { get; set; }

        public VM_CuttingServices AvailableCuttingServices { get; set; }
        public VM_FinishingServices AvailableFinishingServices { get; set; }
        public VM_FinishingMaterials AvailableFinishingMaterials { get; set; }
        public VM_Presses VM_Presses { get; set; }
        public List<RelatedJob> RelatedJobs { get; set; }

        public PriceListViewModel PriceListViewModel { get; set; }

        // Client Cost Splits for multi-client allocation
        public VM_ClientCostSplitEditor ClientCostSplits { get; set; }
    }
}