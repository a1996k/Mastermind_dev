using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using FifthPress.Models.ViewModels.NewPriceDisplay;

namespace FifthPress.Models.ViewModels
{
    public class VM_JobWrapper
    {
        public VM_Job VM_Job { get; set; }
        public bool RequiresValidator { get; set; } = true;
        public List<Contact> Contacts { get; set; }
        public JobPriceInformation ItemTotals { get; set; }

        //public List<JobParentItemTotals_Result> JobItemTotals { get; set; }
        public VM_PrepressServices AvailablePrepressServices { get; set; }

        public VM_CuttingServices AvailableCuttingServices { get; set; }
        public VM_FinishingServices AvailableFinishingServices { get; set; }
        public VM_FinishingMaterials AvailableFinishingMaterials { get; set; }
        public VM_Presses VM_Presses { get; set; }

        public PriceListViewModel PriceListViewModel { get; set; }

        // ReleaseToClientView from related Quote (via QuoteNumber/JobNumber)
        public bool? ReleaseToClientView { get; set; }
        public int? RelatedQuoteId { get; set; }

        // Client Cost Splits for multi-client allocation
        public VM_ClientCostSplitEditor ClientCostSplits { get; set; }
    }
}